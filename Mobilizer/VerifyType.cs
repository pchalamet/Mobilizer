using System;
using System.Collections;

namespace Mobilizer
{
	public class VerifyType
	{
		public static VerifyType Merge(VerifyType a, VerifyType b)
		{
			// if either is null, use the other (possible more specific) type;
			// if one is compatible with the other, use the more general type.

			// since the method is verifiable, we will never have to compare
			// reference and unboxed types

			if (a == null)
				return b;

			if (b == null || a.Equals(b))
				return a;

			// if both are int32 or smaller, make it int32

			if (!a.Boxed &&
				(a.Type == typeof(byte) ||
				 a.Type == typeof(SByte) ||
				 a.Type == typeof(short) ||
				 a.Type == typeof(ushort) ||
				 a.Type == typeof(int) ||
				 a.Type == typeof(uint)) &&
				!b.Boxed &&
				(b.Type == typeof(byte) ||
				 b.Type == typeof(SByte) ||
				 b.Type == typeof(short) ||
				 b.Type == typeof(ushort) ||
				 b.Type == typeof(int) ||
				 b.Type == typeof(uint)))
			
				return new VerifyType(typeof(int));

			// if both are int64 of some kind, make it int64

			if (!a.Boxed &&
				(a.Type == typeof(long) ||
				 a.Type == typeof(ulong)) &&
				!b.Boxed &&
				(b.Type == typeof(long) ||
				 b.Type == typeof(ulong)))

				return new VerifyType(typeof(long));

			// find the common type

			for (Type t = a.Type; t != null; t = t.DeclaringType)
				if (t.IsAssignableFrom(b.Type) && t != typeof(object))
					return new VerifyType(t);

			for (Type t = b.Type; t != null; t = t.DeclaringType)
				if (t.IsAssignableFrom(a.Type) && t != typeof(object))
					return new VerifyType(t);

			// find a common interface

			ArrayList ais = new ArrayList(a.Type.GetInterfaces());
			ArrayList bis = new ArrayList(b.Type.GetInterfaces());
			ArrayList cis = new ArrayList();

			foreach (Type t in ais)
				if (bis.Contains(t))
					cis.Add(t);

			cis.Sort(new AssignableOrderComparer());

			if (cis.Count > 0)
				return new VerifyType((Type) cis[0]);

			// can't reconcile, save that they are reference types, hence
			// object
			return new VerifyType(typeof(object));
		}

		public static implicit operator VerifyType(Type t)
		{
			return new VerifyType(t);
		}

		public readonly bool Boxed;
		public readonly Type Type;

		public VerifyType(Type t)
		{
			Boxed = !t.IsValueType;
			Type = t;
		}

		public VerifyType(Type t, bool boxed)
		{
			Boxed = boxed;
			Type = t;
		}

		public override bool Equals(object o)
		{
			return	o is VerifyType &&
					((VerifyType) o).Boxed == Boxed &&
					((VerifyType) o).Type == Type;
		}

		public override int GetHashCode()
		{
			return Boxed.GetHashCode() * 31 + Type.GetHashCode();
		}

		public override string ToString()
		{
			return string.Format(@"{{{0} {1}}}", Boxed ? "Boxed" : "Value", Type);
		}
	}
}
