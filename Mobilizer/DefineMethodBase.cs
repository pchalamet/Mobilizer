using System;
using System.Collections;
using System.Diagnostics.SymbolStore;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.ILReader;

namespace Mobilizer
{
	public class DefineMethodBase
	{
		internal static void Emit(ILGenerator g, Instruction i)
		{
			if (i.Operand == null)
				g.Emit(i.OpCode);

			else if (i.Operand is byte)
				g.Emit(i.OpCode, (byte) i.Operand);

			else if (i.Operand is ConstructorInfo)
				g.Emit(i.OpCode, (ConstructorInfo) i.Operand);

			else if (i.Operand is double)
				g.Emit(i.OpCode, (double) i.Operand);

			else if (i.Operand is FieldInfo)
				g.Emit(i.OpCode, (FieldInfo) i.Operand);

			else if (i.Operand is short)
				g.Emit(i.OpCode, (short) i.Operand);

			else if (i.Operand is int)
				g.Emit(i.OpCode, (int) i.Operand);

			else if (i.Operand is long)
				g.Emit(i.OpCode, (long) i.Operand);

			else if (i.Operand is Label)
				g.Emit(i.OpCode, (Label) i.Operand);

			else if (i.Operand is Label[])
				g.Emit(i.OpCode, (Label[]) i.Operand);

			else if (i.Operand is LocalBuilder)
				g.Emit(i.OpCode, (LocalBuilder) i.Operand);

			else if (i.Operand is MethodInfo)
				g.Emit(i.OpCode, (MethodInfo) i.Operand);

			else if (i.Operand is sbyte)
				g.Emit(i.OpCode, (sbyte) i.Operand);

			else if (i.Operand is SignatureHelper)
				g.Emit(i.OpCode, (SignatureHelper) i.Operand);

			else if (i.Operand is float)
				g.Emit(i.OpCode, (float) i.Operand);

			else if (i.Operand is string)
				g.Emit(i.OpCode, (string) i.Operand);

			else if (i.Operand is Type)
				g.Emit(i.OpCode, (Type) i.Operand);

			else if (i.Operand is ParameterInfo)
			{
				ParameterInfo p = (ParameterInfo) i.Operand;
				g.Emit(i.OpCode, p.Position + (((MethodBase) p.Member).IsStatic ? 0 : 1));
			}

			else
				throw new ArgumentOutOfRangeException("i", i, "Operand was of unknown type " + i.Operand.GetType());
		}

		internal static OpCode LongFormOf(OpCode op)
		{
			switch (op.Value)
			{
				case Op.beq_s:
					return OpCodes.Beq;

				case Op.bge_s:
					return OpCodes.Bge;

				case Op.bge_un_s:
					return OpCodes.Bge_Un;

				case Op.bgt_s:
					return OpCodes.Bgt;

				case Op.bgt_un_s:
					return OpCodes.Bgt_Un_S;

				case Op.ble_s:
					return OpCodes.Ble;

				case Op.ble_un_s:
					return OpCodes.Ble_Un;

				case Op.blt_s:
					return OpCodes.Blt;

				case Op.blt_un_s:
					return OpCodes.Blt_Un;

				case Op.bne_un_s:
					return OpCodes.Bne_Un;

				case Op.br_s:
					return OpCodes.Br;

				case Op.brfalse_s:
					return OpCodes.Brfalse;

				case Op.brtrue_s:
					return OpCodes.Brtrue;

				case Op.leave_s:
					return OpCodes.Leave;

				default:
					return op;
			}
		}

		protected readonly ISymbolDocumentWriter _doc;
		protected readonly NewOld _map;
		protected readonly SlotType _st;
		protected readonly OffsetLabelMap _lbl;
		protected readonly LocalCache _locs;
		protected readonly MethodBase _meth;
		protected readonly MethodBody _body;
		protected readonly ILGenerator _g;

		public DefineMethodBase(NewOld map, MethodBase meth, ReaderCache rc, ISymbolDocumentWriter doc)
		{
			_doc = doc;
			_map = map;
			_meth = meth;
			_body = rc.GetMethodBody(_meth);
			
			if (_meth is ConstructorInfo)
				_g = _map.Bld((ConstructorInfo) _meth).GetILGenerator();
			else
				_g = _map.Bld((MethodInfo) _meth).GetILGenerator();

			_st = new SlotType(_meth, _body);
			_lbl = new OffsetLabelMap(_g, _body);
			_locs = new LocalCache(_g);
		}

		public virtual void DefineMethod()
		{
			if (_body.Locals != null)
				foreach (Type t in _body.Locals)
					_g.DeclareLocal(_map.TypeFor(t));

			BeforeBody();
			
			for (int j = 0; j < _body.Count; j++)
			{
				// get the instruction for this offset
				Instruction i = _body[j];

				// start/end exception handling blocks

				foreach (ExceptionHandler seh in _body.Exceptions)
					if (seh.Handler.Offset + seh.Handler.Length == i.Offset)
						EndSeh();

				foreach (ExceptionHandler seh in _body.Exceptions)
					if (seh.Handler.Offset == i.Offset)
					{
						if (seh is Catch)
							BeginCatch((Catch) seh);
						else if (seh is Fault)
							BeginFault();
						else if (seh is Finally)
							BeginFinally();
						else if (seh is Filter)
							BeginFilter();
					}

				foreach (ExceptionHandler seh in _body.Exceptions)
					if (seh.Try.Offset == i.Offset)
						BeginSeh();

				// mark this offset in the IL
				_lbl.Mark(i.Offset);

				// fix up the opcode/operand 
				FixInstruction(ref i);

				// emit the actual instruction
				Emit(i);
			}

			AfterBody();
		}

		protected virtual void AfterBody() {}
		protected virtual void BeforeBody() {}

		protected virtual void BeginCatch(Catch seh)
		{
			_g.BeginCatchBlock(_map.TypeFor(seh.Type));
		}

		protected virtual void BeginFault()
		{
			_g.BeginFaultBlock();
		}

		protected virtual void BeginFilter()
		{
			_g.BeginExceptFilterBlock();
		}

		protected virtual void BeginFinally()
		{
			_g.BeginFinallyBlock();
		}

		protected virtual void BeginSeh()
		{
			_g.BeginExceptionBlock();
		}

		protected virtual void Emit(Instruction i) 
		{
			_g.MarkSequencePoint(_doc, i.Offset == 0 ? 1 : i.Offset, 0, i.Offset == 0 ? 1 : i.Offset, 0);
			DefineMethodBase.Emit(_g, i);
		}

		protected virtual void EndSeh() 
		{
			_g.EndExceptionBlock();
		}

		protected virtual void FixInstruction(ref Instruction i)
		{
			// fix up the opcode
			i.OpCode = DefineMethodBase.LongFormOf(i.OpCode);

			// fix up the operand

			if (i.Operand is Type)
				i.Operand = _map.TypeFor((Type) i.Operand);

			else if (i.OpCode.OperandType == OperandType.InlineBrTarget ||
				i.OpCode.OperandType == OperandType.ShortInlineBrTarget)
			{
				i.OperandData = BitConverter.GetBytes((int) i.Operand);
				i.Operand = _lbl[(int) i.Operand];
			}

			else if (i.Operand is FieldInfo)
				i.Operand = _map.FieldFor((FieldInfo) i.Operand);

			else if (i.Operand is MethodBase)
				i.Operand = _map.MethodBaseFor((MethodBase) i.Operand);

			else if (i.OpCode.Value == Op.@switch)
			{
				int[] offsets = (int[]) i.Operand;
				Label[] ls = new Label[offsets.Length];

				for (int k = 0; k < offsets.Length; k++)
					ls[k] = _lbl[offsets[k]];

				i.Operand = ls;
			}
		}
	}
}
