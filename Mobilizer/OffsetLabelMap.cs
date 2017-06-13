using System;
using System.Collections;
using System.Reflection.Emit;
using System.Reflection.ILReader;

namespace Mobilizer
{
	public class OffsetLabelMap
	{
		private readonly ILGenerator _g;
		private readonly IDictionary _map;

		public OffsetLabelMap(ILGenerator g, MethodBody m)
		{
			_g = g;
			_map = new Hashtable();

			foreach (Instruction i in m)
				_map.Add(i.Offset, _g.DefineLabel());
		}

		public Label this[int offset]
		{
			get
			{
				if (!_map.Contains(offset))
					throw new ArgumentOutOfRangeException("offset", offset, "No label for offset");

				return (Label) _map[offset];
			}
		}

		public void Mark(int offset)
		{
			_g.MarkLabel(this[offset]);
		}
	}
}
