using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.ILReader;

namespace Mobilizer
{
	public class SlotType
	{
		internal static VerifyType[] MergeStack(VerifyType[] xs, VerifyType[] ys)
		{
			if (xs == null)
				throw new ArgumentNullException("xs");

			if (ys == null)
				throw new ArgumentNullException("ys");

			if (xs.Length != ys.Length)
				throw new InvalidOperationException("Stacks must have same number of slots");

			VerifyType[] zs = new VerifyType[xs.Length];

			for (int i = 0; i < zs.Length; i++)
				zs[i] = VerifyType.Merge(xs[i], ys[i]);

			return zs;
		}

		internal static int[] Next(MethodBody body, Instruction i)
		{
			if (i.OpCode.Equals(OpCodes.Switch))
			{
				int[] targets = (int[]) i.Operand;
				int[] nexts = new int[targets.Length + 1];
				Array.Copy(targets, 0, nexts, 0, targets.Length);
				nexts[targets.Length] = ReaderCache.NextOffset(body, i.Offset);
				return nexts;
			}
//			else if (i.OpCode.Equals(OpCodes.Leave) ||
//				i.OpCode.Equals(OpCodes.Leave_S))
			
//				return new int[] {};

			else if (i.OpCode.FlowControl == FlowControl.Call &&
				TailPrefixed(body, i))
			
				return new int[] {};

			else if (i.OpCode.FlowControl == FlowControl.Call ||
				i.OpCode.FlowControl == FlowControl.Next ||
				i.OpCode.FlowControl == FlowControl.Meta ||
				i.OpCode.FlowControl == FlowControl.Phi) // never encounter phi?

				return new int[] { ReaderCache.NextOffset(body, i.Offset) };

			else if (i.OpCode.FlowControl == FlowControl.Branch)
				return new int[] { (int) i.Operand };

			else if (i.OpCode.FlowControl == FlowControl.Cond_Branch)
				return new int[] { (int) i.Operand, ReaderCache.NextOffset(body, i.Offset) };

			else if (i.OpCode.FlowControl == FlowControl.Return ||
				i.OpCode.FlowControl == FlowControl.Throw)

				return new int[] {};

			else
				throw new ArgumentOutOfRangeException();
		}

		internal static bool StackSame(VerifyType[] a, VerifyType[] b)
		{
			if (a == null)
				throw new ArgumentNullException("a");

			if (b == null)
				throw new ArgumentNullException("b");

			if (a.Length != b.Length)
				throw new InvalidOperationException("Number of slots vary");

			for (int i = 0; i < a.Length; i++)
				if (!(a[i].Equals(b[i])))
					return false;

			return true;
		}

		internal static bool TailPrefixed(MethodBody body, Instruction i)
		{
			return ReaderCache.IndexOf(body, i.Offset) > 0 &&
				body[ReaderCache.IndexOf(body, i.Offset) - 1].OpCode.Equals(OpCodes.Tailcall);
		}

		private readonly MethodBase _meth;
		private readonly MethodBody _body;
		private readonly IDictionary _type;

		public SlotType(MethodBase meth, MethodBody body)
		{
			if (meth == null)
				throw new ArgumentNullException("meth");

			if (body == null)
				throw new ArgumentNullException("body");

			_meth = meth;
			_body = body;
			_type = new Hashtable();

			IDictionary known = _type;

			// stack empty on method entry
			known.Add(0, new VerifyType[] {});

			foreach (ExceptionHandler h in _body.Exceptions)
			{
				// stack empty on try entry
				if (!known.Contains(h.Try.Offset))
					known.Add(h.Try.Offset, new VerifyType[] {});

				if (h is Catch)
					known.Add(h.Handler.Offset, new VerifyType[] { ((Catch) h).Type });
				else if (h is Fault || h is Finally)
					known.Add(h.Handler.Offset, new VerifyType[] {});
				else if (h is Filter)
					known.Add(h.Handler.Offset, new VerifyType[] { typeof(Exception) });

				if (h is Finally)
					known.Add(h.Handler.Offset + h.Handler.Length, new VerifyType[] {});
			}

			IList follow = new ArrayList(known.Keys);

			// now start the work proper
			while (follow.Count > 0)
			{
				Instruction i = ReaderCache.Offset(_body, (int) follow[0]);
				follow.RemoveAt(0);
				int[] next = SlotType.Next(_body, i);

				if (next.Length > 0)
				{
					// retrieve the known type
					VerifyType[] ts = (VerifyType[]) ((ICloneable) known[i.Offset]).Clone();

					// apply this instruction
					ts = Mutate(i, ts);

					// copy new type to follow set
					for (int j = 0; j < next.Length; j++) 
						// not seen before; so record and follow
						if (!known.Contains(next[j]))
						{
							known.Add(next[j], ts);
							follow.Add(next[j]);
						}
						else
						{
							// merge old stacks

							VerifyType[] oldTs = (VerifyType[]) known[next[j]];
							ts = SlotType.MergeStack(ts, oldTs);

							// new type determined?
							if (!SlotType.StackSame(ts, oldTs))
							{
								// record
								known[next[j]] = ts;

								// follow if not already in the queue
								if (!follow.Contains(next[j]))
									follow.Add(next[j]);
							}
						}
				}
			}
		}

		public bool Contains(int offset)
		{
			return _type.Contains(offset);
		}

		public VerifyType[] this[int offset]
		{
			get
			{
				if (offset == -1)
					return new VerifyType[] {};
				else if (!_type.Contains(offset))
					throw new ArgumentOutOfRangeException("offset", offset, "No slot types for offset");

				return (VerifyType[]) ((ICloneable) _type[offset]).Clone();
			}
		}

		public Type Local(int n)
		{
			return _body.Locals[n];
		}

		public Type Param(int n)
		{
			if (!_meth.IsStatic)
				if (n == 0)
					return _meth.DeclaringType;
				else
					return _meth.GetParameters()[n - 1].ParameterType;
			else
				return _meth.GetParameters()[n].ParameterType;
		}

		internal VerifyType[] Mutate(Instruction i, VerifyType[] ts)
		{
			ArrayList tl = new ArrayList(ts);

			switch (i.OpCode.Value)
			{
				case Op.add:
				case Op.add_ovf:
				case Op.add_ovf_un:
				case Op.and:
				case Op.brfalse:
				case Op.brfalse_s:
				case Op.brtrue:
				case Op.brtrue_s:
				case Op.div:
				case Op.div_un:
				case Op.initobj:
				case Op.mul:
				case Op.mul_ovf:
				case Op.mul_ovf_un:
				case Op.or:
				case Op.pop:
				case Op.rem:
				case Op.rem_un:
				case Op.shl:
				case Op.shr:
				case Op.shr_un:
				case Op.starg:
				case Op.starg_s:
				case Op.stloc:
				case Op.stloc_0:
				case Op.stloc_1:
				case Op.stloc_2:
				case Op.stloc_3:
				case Op.stloc_s:
				case Op.stsfld:
				case Op.sub:
				case Op.sub_ovf:
				case Op.sub_ovf_un:
				case Op.xor:
					tl.RemoveAt(0);
					break;
					
				case Op.arglist:
					tl.Insert(0, new VerifyType(typeof(RuntimeArgumentHandle)));
					break;

				case Op.beq:
				case Op.beq_s:
				case Op.bge:
				case Op.bge_s:
				case Op.bge_un:
				case Op.bge_un_s:
				case Op.bgt:
				case Op.bgt_s:
				case Op.bgt_un:
				case Op.bgt_un_s:
				case Op.ble:
				case Op.ble_s:
				case Op.ble_un:
				case Op.ble_un_s:
				case Op.blt:
				case Op.blt_s:
				case Op.blt_un:
				case Op.blt_un_s:
				case Op.bne_un:
				case Op.bne_un_s:
				case Op.cpobj:
				case Op.stfld:
				case Op.stind_i:
				case Op.stind_i1:
				case Op.stind_i2:
				case Op.stind_i4:
				case Op.stind_i8:
				case Op.stind_r4:
				case Op.stind_r8:
				case Op.stind_ref:
				case Op.stobj:
				case Op.@switch:
					tl.RemoveRange(0, 2);
					break;

				case Op.box:
					tl[0] = new VerifyType(((VerifyType) tl[0]).Type, true);
					break;

					// invariant
				case Op.br:
				case Op.br_s:
				case Op.@break:
				case Op.endfilter:
				case Op.endfinally:
				case Op.jmp:
				case Op.leave:
				case Op.leave_s:
				case Op.neg:
				case Op.nop:
				case Op.not:
				case Op.ret:
				case Op.rethrow:
				case Op.tail_:
				case Op.@throw:
				case Op.unaligned_:
				case Op.volatile_:
					break;

				case Op.call:
				case Op.callvirt:
					MethodBase m = (MethodBase) i.Operand;
					tl.RemoveRange(0, m.GetParameters().Length + (m.IsStatic ? 0 : 1));
					
					if (m is MethodInfo && ((MethodInfo) m).ReturnType != typeof(void))
						tl.Insert(0, new VerifyType(((MethodInfo) m).ReturnType));

					break;

				case Op.castclass:
				case Op.isinst:
				case Op.ldobj:
					tl[0] = new VerifyType((Type) i.Operand);
					break;

				case Op.ceq:
				case Op.cgt:
				case Op.cgt_un:
				case Op.clt:
				case Op.clt_un:
					tl.RemoveAt(0);
					tl[0] = new VerifyType(typeof(int));
					break;

				case Op.ckfinite:
				case Op.conv_r4:
				case Op.conv_r_un:
				case Op.ldind_r4:
					tl[0] = new VerifyType(typeof(float));
					break;

				case Op.conv_i1:
				case Op.conv_ovf_i1:
					tl[0] = new VerifyType(typeof(SByte));
					break;

				case Op.conv_i2:
				case Op.conv_ovf_i2:
					tl[0] = new VerifyType(typeof(short));
					break;

				case Op.conv_i:
				case Op.conv_i4:
				case Op.conv_ovf_i:
				case Op.conv_ovf_i4:
				case Op.ldlen:
				case Op.ldind_i:
				case Op.ldind_i1:
				case Op.ldind_i2:
				case Op.ldind_i4:
				case Op.ldind_u1:
				case Op.ldind_u2:
				case Op.ldind_u4:
					tl[0] = new VerifyType(typeof(int));
					break;

				case Op.conv_i8:
				case Op.conv_ovf_i8:
				case Op.ldind_i8:
					tl[0] = new VerifyType(typeof(long));
					break;

				case Op.conv_ovf_i1_un:
				case Op.conv_u1:
					tl[0] = new VerifyType(typeof(byte));
					break;

				case Op.conv_ovf_i2_un:
				case Op.conv_u2:
					tl[0] = new VerifyType(typeof(ushort));
					break;

				case Op.conv_ovf_i4_un:
				case Op.conv_ovf_i_un:
				case Op.conv_u:
				case Op.conv_u4:
					tl[0] = new VerifyType(typeof(uint));
					break;

				case Op.conv_ovf_i8_un:
				case Op.conv_ovf_u_un:
				case Op.conv_u8:
					tl[0] = new VerifyType(typeof(ulong));
					break;

				case Op.conv_r8:
				case Op.ldind_r8:
					tl[0] = new VerifyType(typeof(double));
					break;

				case Op.cpblk:
				case Op.initblk:
				case Op.stelem_i:
				case Op.stelem_i1:
				case Op.stelem_i2:
				case Op.stelem_i4:
				case Op.stelem_i8:
				case Op.stelem_r4:
				case Op.stelem_r8:
				case Op.stelem_ref:
					tl.RemoveRange(0, 3);
					break;

				case Op.dup:
					tl.Insert(0, tl[0]);
					break;

				case Op.ldarg:
				case Op.ldarg_s:
					if (i.Operand is ParameterInfo)
						tl.Insert(0, new VerifyType(((ParameterInfo) i.Operand).ParameterType));
					else
						tl.Insert(0, new VerifyType(Param((int) i.Operand)));
					break;

				case Op.ldarg_0:
					tl.Insert(0, new VerifyType(Param(0)));
					break;

				case Op.ldarg_1:
					tl.Insert(0, new VerifyType(Param(1)));
					break;

				case Op.ldarg_2:
					tl.Insert(0, new VerifyType(Param(2)));
					break;

				case Op.ldarg_3:
					tl.Insert(0, new VerifyType(Param(3)));
					break;

				case Op.ldarga:
				case Op.ldarga_s:
				case Op.ldftn:
				case Op.ldloca:
				case Op.ldloca_s:
				case Op.ldsflda:
				case Op.ldtoken:
					tl.Insert(0, new VerifyType(typeof(IntPtr)));
					break;

				case Op.ldc_i4:
				case Op.ldc_i4_0:
				case Op.ldc_i4_1:
				case Op.ldc_i4_2:
				case Op.ldc_i4_3:
				case Op.ldc_i4_4:
				case Op.ldc_i4_5:
				case Op.ldc_i4_6:
				case Op.ldc_i4_7:
				case Op.ldc_i4_8:
				case Op.ldc_i4_m1:
				case Op.ldc_i4_s:
				case Op.@sizeof:
					tl.Insert(0, new VerifyType(typeof(int)));
					break;

				case Op.ldc_i8:
					tl.Insert(0, new VerifyType(typeof(long)));
					break;

				case Op.ldc_r4:
					tl.Insert(0, new VerifyType(typeof(float)));
					break;

				case Op.ldc_r8:
					tl.Insert(0, new VerifyType(typeof(double)));
					break;

				case Op.ldelem_i:
				case Op.ldelem_i4:
					tl.RemoveAt(0);
					tl[0] = new VerifyType(typeof(int));
					break;

				case Op.ldelem_i1:
					tl.RemoveAt(0);
					tl[0] = new VerifyType(typeof(SByte));
					break;

				case Op.ldelem_i2:
					tl.RemoveAt(0);
					tl[0] = new VerifyType(typeof(short));
					break;

				case Op.ldelem_i8:
					tl.RemoveAt(0);
					tl[0] = new VerifyType(typeof(long));
					break;

				case Op.ldelem_r4:
					tl.RemoveAt(0);
					tl[0] = new VerifyType(typeof(float));
					break;

				case Op.ldelem_r8:
					tl.RemoveAt(0);
					tl[0] = new VerifyType(typeof(double));
					break;

				case Op.ldelem_ref:
					tl.RemoveAt(0);
					tl[0] = new VerifyType(((VerifyType) tl[0]).Type.GetElementType());
					break;

				case Op.ldelem_u1:
					tl.RemoveAt(0);
					tl[0] = new VerifyType(typeof(byte));
					break;

				case Op.ldelem_u2:
					tl.RemoveAt(0);
					tl[0] = new VerifyType(typeof(ushort));
					break;

				case Op.ldelem_u4:
					tl.RemoveAt(0);
					tl[0] = new VerifyType(typeof(uint));
					break;

				case Op.ldelema:
					tl.RemoveAt(0);
					tl[0] = new VerifyType(typeof(IntPtr));
					break;

				case Op.ldflda:
				case Op.ldvirtftn:
				case Op.localloc:
				case Op.refanytype:
				case Op.refanyval:
				case Op.unbox:
					tl[0] = new VerifyType(typeof(IntPtr));
					break;

				case Op.ldfld:
					tl[0] = new VerifyType(((FieldInfo) i.Operand).FieldType);
					break;

				case Op.ldloc:
				case Op.ldloc_s:
					tl.Insert(0, new VerifyType(Local((int) i.Operand)));
					break;

				case Op.ldloc_0:
					tl.Insert(0, new VerifyType(Local(0)));
					break;

				case Op.ldloc_1:
					tl.Insert(0, new VerifyType(Local(1)));
					break;

				case Op.ldloc_2:
					tl.Insert(0, new VerifyType(Local(2)));
					break;

				case Op.ldloc_3:
					tl.Insert(0, new VerifyType(Local(3)));
					break;

				case Op.ldnull:
					tl.Insert(0, null); // null is special, for "any type"
					break;

				case Op.ldsfld:
					tl.Insert(0, new VerifyType(((FieldInfo) i.Operand).FieldType));
					break;

				case Op.ldstr:
					tl.Insert(0, new VerifyType(typeof(string)));
					break;

				case Op.mkrefany:
					tl[0] = new VerifyType(typeof(TypedReference));
					break;

				case Op.newarr:
					tl[0] = new VerifyType(Array.CreateInstance((Type) i.Operand, 0).GetType());
					break;

				case Op.newobj:
					tl.RemoveRange(0, ((ConstructorInfo) i.Operand).GetParameters().Length);
					tl.Insert(0, new VerifyType(((ConstructorInfo) i.Operand).DeclaringType));
					break;

					// known, but unhandled, opcodes
				case Op.calli:
					throw new ArgumentOutOfRangeException("i", i, "don't know how to handle " + i.OpCode.Name);

				default:
					// complete "unknowns" (i.e. prefix_; future opcodes)
					throw new ArgumentOutOfRangeException("i", i, "unknown opcode: " + i.OpCode.Name);
			}

			return (VerifyType[]) tl.ToArray(typeof(VerifyType));
		}
	}
}
