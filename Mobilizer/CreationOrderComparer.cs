using System;
using System.Collections;

namespace Mobilizer
{
	public class CreationOrderComparer : IComparer
	{
		public CreationOrderComparer() {}

		public int Compare(object x, object y)
		{
			Type tx = (Type) x;
			Type ty = (Type) y;
			Type t = tx;

			while ((t = t.DeclaringType) != null)
				if (t == ty)
					return 1;	// y declares x

			t = ty;

			while ((t = t.DeclaringType) != null)
				if (t == tx)
					return -1;	// x declares y

			return 0;
		}
	}
}
