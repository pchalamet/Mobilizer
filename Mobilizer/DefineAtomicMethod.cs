using MobilizerRt;
using System;
using System.Diagnostics.SymbolStore;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.ILReader;

namespace Mobilizer
{
	public class DefineAtomicMethod : DefineMethodBase
	{
		private Label _leaveLabel;
		private LocalBuilder _retLoc;

		public DefineAtomicMethod(NewOld map, MethodBase meth, ReaderCache rc, ISymbolDocumentWriter doc) : base(map, meth, rc, doc) {}

		protected override void AfterBody()
		{
			_g.BeginFinallyBlock();
			_g.Emit(OpCodes.Call, typeof(MobileContext).GetMethod("Unlock"));
			_g.EndExceptionBlock();

			if (_retLoc != null)
				_g.Emit(OpCodes.Ldloc, _retLoc);

			_g.Emit(OpCodes.Ret);
		}

		protected override void BeforeBody()
		{
			_g.Emit(OpCodes.Call, typeof(MobileContext).GetMethod("Lock"));

			_leaveLabel = _g.BeginExceptionBlock();

			if (_meth is MethodInfo && ((MethodInfo) _meth).ReturnType != typeof(void))
				_retLoc = _g.DeclareLocal(_map.TypeFor(((MethodInfo) _meth).ReturnType));
		}

		protected override void Emit(Instruction i)
		{
			if (i.OpCode.Value == Op.ret)
			{
				if (_retLoc != null)
					_g.Emit(OpCodes.Stloc, _retLoc);

				i.OpCode = OpCodes.Br;
				i.Operand = _leaveLabel;
			}

			base.Emit(i);
		}
	}
}
