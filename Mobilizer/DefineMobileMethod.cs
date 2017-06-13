using MobilizerRt;
using System;
using System.Collections;
using System.Diagnostics.SymbolStore;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.ILReader;

// Problems:
// 1. Any time lock/unlock is called should be wrapped in try/finally handlers!

namespace Mobilizer
{
	public class DefineMobileMethod : DefineMethodBase
	{
		private static readonly MethodInfo _isRestoring = typeof(MobileContext).GetMethod("Restoring");
		private static readonly MethodInfo _isUnwinding = typeof(MobileContext).GetMethod("IsUnwinding");
		private static readonly MethodInfo _isUnwindPending = typeof(MobileContext).GetMethod("IsUnwindPending");
		private static readonly MethodInfo _lock = typeof(MobileContext).GetMethod("Lock");
		private static readonly MethodInfo _pop = typeof(MobileContext).GetMethod("Pop");
		private static readonly MethodInfo _popInt = typeof(MobileContext).GetMethod("PopInt");
		private static readonly MethodInfo _push = typeof(MobileContext).GetMethod("Push");
		private static readonly MethodInfo _pushInt = typeof(MobileContext).GetMethod("PushInt");
		private static readonly MethodInfo _unlock = typeof(MobileContext).GetMethod("Unlock");

		private bool _prevWasCall = false;
		private int _inHandler;
		private readonly Stack _unwindHandlerLbl;
		private readonly Stack _restoreHandlerLbl;
		private readonly Stack _restorePoints;

		public DefineMobileMethod(NewOld map, MethodBase meth, ReaderCache rc, ISymbolDocumentWriter doc) : base(map, meth, rc, doc)
		{
			_unwindHandlerLbl = new Stack();
			_restoreHandlerLbl = new Stack();
			_restorePoints = new Stack();
		}

		protected override void AfterBody()
		{
			// *** emit restoring support ***

			_g.MarkLabel(PopRestoreLabel());

			// restore locals
			if (_body.Locals != null)
				for (int i = _body.Locals.Length - 1; i >= 0; i--) 
				{
					_g.Emit(OpCodes.Call, _pop);

					if (_body.Locals[i].IsValueType)
					{
						_g.Emit(OpCodes.Unbox, _map.TypeFor(_body.Locals[i]));
						_g.Emit(OpCodes.Ldobj, _map.TypeFor(_body.Locals[i]));
					}
					else
						_g.Emit(OpCodes.Castclass, _map.TypeFor(_body.Locals[i]));

					_g.Emit(OpCodes.Stloc, i);
				}

			// restore arguments

			for (int i = _meth.GetParameters().Length - 1; i >= 0; i--)
			{
				_g.Emit(OpCodes.Call, _pop);

				if (_meth.GetParameters()[i].ParameterType.IsValueType)
				{
					_g.Emit(OpCodes.Unbox, _map.TypeFor(_meth.GetParameters()[i].ParameterType));
					_g.Emit(OpCodes.Ldobj, _map.TypeFor(_meth.GetParameters()[i].ParameterType));
				}
				else
					_g.Emit(OpCodes.Castclass, _map.TypeFor(_meth.GetParameters()[i].ParameterType));

				_g.Emit(OpCodes.Starg, _meth.GetParameters()[i].Position + (_meth.IsStatic ? 0 : 1));
			}

			EmitRestoreSwitch();

			// *** emit unwinding support ***

			_g.MarkLabel(PopUnwindLabel());

			// save arguments (IP last thing already saved)

			foreach (ParameterInfo pi in _meth.GetParameters())
			{
				_g.Emit(OpCodes.Ldarg, pi.Position + (_meth.IsStatic ? 0 : 1));

				if (pi.ParameterType.IsValueType)
					_g.Emit(OpCodes.Box, _map.TypeFor(pi.ParameterType));

				_g.Emit(OpCodes.Call, _push);
			}

			// save locals

			if (_body.Locals != null)
				for (int i = 0; i < _body.Locals.Length; i++)
				{
					_g.Emit(OpCodes.Ldloc, i);

					if (_body.Locals[i].IsValueType)
						_g.Emit(OpCodes.Box, _map.TypeFor(_body.Locals[i]));

					_g.Emit(OpCodes.Call, _push);
				}

			// premature ret

			Type t = _map.TypeFor(((MethodInfo) _meth).ReturnType);

			if (t != typeof(void))
			{
				if (t.IsValueType)
				{
					LocalBuilder loc = _locs[t];
					_g.Emit(OpCodes.Ldloca, loc);
					_g.Emit(OpCodes.Initobj, t);
					_g.Emit(OpCodes.Ldloc, loc);
					_locs.Free(loc);
				}
				else
					_g.Emit(OpCodes.Ldnull);
			}

			_g.Emit(OpCodes.Ret);
		}

		protected override void BeforeBody()
		{
			_unwindHandlerLbl.Push(_g.DefineLabel());
			_restoreHandlerLbl.Push(_g.DefineLabel());
			_restorePoints.Push(new ArrayList());
			_g.Emit(OpCodes.Call, _isRestoring);
			_g.Emit(OpCodes.Brtrue, RestoreLabel);
		}

		protected override void BeginCatch(Catch seh) 
		{
			EndOfBlock();
			base.BeginCatch(seh);
			_g.Emit(OpCodes.Call, _lock);
			_inHandler++;
		}

		protected override void BeginFault()
		{
			EndOfBlock();
			base.BeginFault();
			_g.Emit(OpCodes.Call, _lock);
			_inHandler++;
		}

		protected override void BeginFilter()
		{
			EndOfBlock();
			base.BeginFilter();
			_g.Emit(OpCodes.Call, _lock);
			_inHandler++;
		}

		protected override void BeginFinally()
		{
			EndOfBlock();
			base.BeginFinally();
			_g.Emit(OpCodes.Call, _lock);
			_inHandler++;
		}

		protected override void BeginSeh()
		{
			if (_inHandler == 0)
			{
				// create restore point for the entry to this block

				RestorePoint p = new RestorePoint(_g); // block entry restore point
				_g.MarkLabel(p.Label);
				_g.Emit(OpCodes.Nop);
				AddRestorePoint(p);

				// start the .try proper
				base.BeginSeh();
			
				// keep a fresh list of restore points for this block
				_restorePoints.Push(new ArrayList());
			
				// create labels for restore/unwind handlers

				_unwindHandlerLbl.Push(_g.DefineLabel());
				_restoreHandlerLbl.Push(_g.DefineLabel());

				// if restoring, branch to handler

				_g.Emit(OpCodes.Call, _isRestoring);
				_g.Emit(OpCodes.Brtrue, RestoreLabel);
			}
			else
			{
				base.BeginSeh();
			}
		}

		protected override void Emit(Instruction i)
		{
			if (_inHandler > 0 &&
				(i.OpCode.Value == Op.leave ||
				 i.OpCode.Value == Op.endfilter ||
				 i.OpCode.Value == Op.endfinally))
			{
				_g.Emit(OpCodes.Call, _unlock);
			}

			// if not statically locked, and doing a backwards branch or ret,
			// and no pointers on the evaluation stack-- insert a restore
			// point
			if (_inHandler == 0 &&
				(i.OpCode.Equals(OpCodes.Ret) || _prevWasCall || ((i.OpCode.FlowControl == FlowControl.Branch || i.OpCode.FlowControl == FlowControl.Cond_Branch) && BitConverter.ToInt32(i.OperandData, 0) <= i.Offset)) &&
				!StackDirty(_st[i.Offset]))
			{
				// create a restore point for this instruction

				RestorePoint p = new RestorePoint(null, i, _g);
				AddRestorePoint(p);

				// if no unwind is pending, skip to instruction

				_g.Emit(OpCodes.Call, _isUnwindPending);
				_g.Emit(OpCodes.Brfalse, p.Label);

				// save evaluation stack
				PushFromEval(_st[i.Offset]);

				// save IP

				_g.Emit(OpCodes.Ldc_I4, RestorePointId);
				_g.Emit(OpCodes.Call, _pushInt);

				// branch to unwind handler
				_g.Emit(OpCodes.Br, UnwindLabel);

				_g.MarkLabel(p.Label);
				base.Emit(i);
			}
			else if (_inHandler == 0 && i.OpCode.FlowControl == FlowControl.Call && i.OpCode.Value != Op.newobj && !StackDirty(_st[i.Offset]))
			{
				// create restore point for this call

				RestorePoint p = new RestorePoint((MethodInfo) i.Operand, i, _g);
				AddRestorePoint(p);

				// mark to restore to here
				_g.MarkLabel(p.Label);

				// save evaluation stack in locals
				
				int nargs = NumParams(p.Call) + (p.Call.IsStatic ? 0 : 1); // for "this"
				ArrayList vts = new ArrayList(_st[i.Offset]);
				vts.RemoveRange(nargs, vts.Count - nargs);
				ArrayList tmpLocs = new ArrayList();

				foreach (VerifyType vt in vts)
				{
					if (vt == null || (vt.Boxed && vt.Type.IsValueType))
						tmpLocs.Add(_locs[typeof(object)]);
					else
						tmpLocs.Add(_locs[_map.TypeFor(vt.Type)]);

					_g.Emit(OpCodes.Stloc, (LocalBuilder) tmpLocs[tmpLocs.Count - 1]);
				}

				// restore evaluation stack

				tmpLocs.Reverse();

				foreach (LocalBuilder loc in tmpLocs)
					_g.Emit(OpCodes.Ldloc, loc);

				// do the call
				base.Emit(i);

				// if unwinding from *within* call...

				_g.Emit(OpCodes.Call, _isUnwinding);
				Label cont = _g.DefineLabel();
				_g.Emit(OpCodes.Brfalse, cont);

				// discard dummy return value
				if (p.Call.ReturnType != typeof(void))
					_g.Emit(OpCodes.Pop);

				// save eval stack pre-call

				foreach (LocalBuilder loc in tmpLocs)
				{
					_g.Emit(OpCodes.Ldloc, loc);
					
					if (loc.LocalType.IsValueType)
						_g.Emit(OpCodes.Box, loc.LocalType);

					_g.Emit(OpCodes.Call, _push);
					_locs.Free(loc);
				}

				vts = new ArrayList(_st[i.Offset]);
				vts.RemoveRange(0, nargs);
				PushFromEval(vts);

				// save IP of this call

				_g.Emit(OpCodes.Ldc_I4, RestorePointId);
				_g.Emit(OpCodes.Call, _pushInt);

				// jump to unwind handler
				_g.Emit(OpCodes.Br, UnwindLabel);

				_g.MarkLabel(cont);
			}
			else if (_inHandler == 0 && i.OpCode.FlowControl == FlowControl.Call && StackDirty(_st[i.Offset]))
			{
				// calling when stack is dirty, but context not locked

				// FIXME should use try/finally
				//_g.Emit(OpCodes.Call, _lock);
				base.Emit(i);
				//_g.Emit(OpCodes.Call, _unlock);
			}
			else
			{
				base.Emit(i);
			}

			// FIXME: This complex conditional is copied-n-pasted from above;
			// refactor this into a function
			if (_inHandler == 0 && i.OpCode.FlowControl == FlowControl.Call && i.OpCode.Value != Op.newobj && !StackDirty(_st[i.Offset]))
			{
				// set flag to force the next instruction to insert a restore
				// point, even if it is not a backwards branch
				_prevWasCall = true;
			}
			else
			{
				_prevWasCall = false;
			}
		}

		protected override void EndSeh()
		{
			_inHandler--;
			base.EndSeh();
			
			// if just exited a block and may be unwinding, branch to containing
			// block's unwind-handler
			if (_inHandler == 0)
			{
				_g.Emit(OpCodes.Call, _isUnwinding);
				_g.Emit(OpCodes.Brtrue, (Label) _unwindHandlerLbl.Peek());
			}
		}

		private void AddRestorePoint(RestorePoint p)
		{
			((IList) _restorePoints.Peek()).Add(p);
		}

		private void EmitRestoreSwitch()
		{
			// pop IP
			_g.Emit(OpCodes.Call, _popInt);

			// switch on IP and branch to restore point
			
			ArrayList restoreLabels = new ArrayList();

			foreach (RestorePoint p in (IList) _restorePoints.Peek())
			{
				p.RestoreHandlerLabel = _g.DefineLabel();
				restoreLabels.Add(p.RestoreHandlerLabel);
			}

			_g.Emit(OpCodes.Switch, (Label[]) restoreLabels.ToArray(typeof(Label)));
			_g.Emit(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor(Type.EmptyTypes));
			_g.Emit(OpCodes.Throw);

			foreach (RestorePoint p in (IList) _restorePoints.Peek())
			{
				_g.MarkLabel(p.RestoreHandlerLabel);
				VerifyType[] vts = _st[p.Offset];
				Array.Reverse(vts);

				foreach (VerifyType vt in vts)
				{
					_g.Emit(OpCodes.Call, _pop);

					if (vt != null && !vt.Boxed)
					{
						_g.Emit(OpCodes.Unbox, _map.TypeFor(vt.Type));
						_g.Emit(OpCodes.Ldobj, _map.TypeFor(vt.Type));
					}
					else if (vt != null)
						_g.Emit(OpCodes.Castclass, _map.TypeFor(vt.Type));
				}

				_g.Emit(OpCodes.Br, p.Label);
			}
		}

		private void EndOfBlock()
		{
			if (_inHandler == 0)
			{
				// emit block restore handler

				_g.MarkLabel((Label) _restoreHandlerLbl.Pop());
				EmitRestoreSwitch();

				// mark block unwind handler
				_g.MarkLabel(PopUnwindLabel());

				// record the IP of *this* block

				_restorePoints.Pop();
				_g.Emit(OpCodes.Ldc_I4, RestorePointId);
				_g.Emit(OpCodes.Call, _pushInt);

				// NB: will fall through to leave instruction
			}
		}

		private int NumParams(MethodInfo call)
		{
			if (call is MethodBuilder)
			{
				foreach (MethodInfo m in _map.Methods)
					if (_map.MethodFor(m) == call)
						return m.GetParameters().Length;

				throw new InvalidOperationException("MethodBuilder not registered");
			}
			else	
				return call.GetParameters().Length;
		}

		private Label PopRestoreLabel()
		{
			return (Label) _restoreHandlerLbl.Pop();
		}

		private Label PopUnwindLabel()
		{
			return (Label) _unwindHandlerLbl.Pop();
		}

		private void PushFromEval(IList vts)
		{
			foreach (VerifyType vt in vts)
			{
				if (vt != null && !vt.Boxed)
					_g.Emit(OpCodes.Box, _map.TypeFor(vt.Type));

				_g.Emit(OpCodes.Call, _push);
			}
		}

		private Label RestoreLabel
		{
			get { return (Label) _restoreHandlerLbl.Peek(); }
		}

		private int RestorePointId
		{
			get { return ((IList) _restorePoints.Peek()).Count - 1; }
		}

		private bool StackDirty(IList vts)
		{
			foreach (VerifyType vt in vts)
			{
				if (vt != null)
				{
					if (vt.Type.IsPointer)
					{
						return true;
					}
				}
			}

			return false;
		}

		private Label UnwindLabel
		{
			get { return (Label) _unwindHandlerLbl.Peek(); }
		}
	}
}
