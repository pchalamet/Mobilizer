using System;
using System.Collections;
using System.Reflection.Emit;

namespace Mobilizer
{
	public class LocalCache
	{
		private ILGenerator _g;
		private IDictionary _typeListMap;

		public LocalCache(ILGenerator g)
		{
			_g = g;
			_typeListMap = new Hashtable();
		}

		public void Free(LocalBuilder loc)
		{
			GetList(loc.LocalType).Add(loc);
		}

		public LocalBuilder this[Type t]
		{
			get
			{
				if (GetList(t).Count == 0)
					return _g.DeclareLocal(t);
				else 
				{
					LocalBuilder loc = (LocalBuilder) GetList(t)[0];
					GetList(t).RemoveAt(0);
					return loc;
				}
			}
		}

		private IList GetList(Type t)
		{
			if (!_typeListMap.Contains(t))
				_typeListMap.Add(t, new ArrayList());

			return (IList) _typeListMap[t];
		}
	}
}
