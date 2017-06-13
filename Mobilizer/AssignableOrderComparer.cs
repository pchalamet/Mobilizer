using System;
using System.Collections;

namespace Mobilizer
{
	public class AssignableOrderComparer : IComparer
	{
		public AssignableOrderComparer() {}

		public int Compare(object x, object y)
		{
			Type tx = (Type) x;
			Type ty = (Type) y;

			if (tx == ty)
				return 0;
			else if (tx.IsAssignableFrom(ty))
				return 1;
			else if (ty.IsAssignableFrom(tx))
				return -1;

			return 0;
		}
	}
}
