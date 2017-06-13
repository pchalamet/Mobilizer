using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.ILReader;

namespace Mobilizer
{
	public class ReaderCache : IAssemblyLoader
	{
		public static int IndexOf(MethodBody m, int offset)
		{
			for (int i = 0; i < m.Count; i++)
				if (m[i].Offset == offset)
					return i;

			return -1;
		}

		public static Instruction Offset(MethodBody m, int offset)
		{
			foreach (Instruction i in m)
				if (i.Offset == offset)
					return i;

			throw new ArgumentOutOfRangeException("offset", offset, "No such offset");
		}

		public static int NextOffset(MethodBody m, int offset)
		{
			for (int i = 0; i < m.Count - 1; i++)
				if (m[i].Offset == offset)
					return m[i + 1].Offset;

			throw new ArgumentOutOfRangeException("offset", offset, "No instruction after offset");
		}

		private IDictionary _moduleReaderMap;

		public ReaderCache()
		{
			_moduleReaderMap = new Hashtable();
		}

		public MethodBody GetMethodBody(MethodBase m)
		{
			if (!_moduleReaderMap.Contains(m.DeclaringType.Module))
				_moduleReaderMap.Add(m.DeclaringType.Module, new ILReader(m.DeclaringType.Module, this));

			return ((ILReader) _moduleReaderMap[m.DeclaringType.Module]).GetMethodBody(m);
		}

		public Assembly Load(string assemblyName) 
		{
			return Assembly.Load(assemblyName);
		}

		public Assembly LoadFrom(string filename)
		{
			return Assembly.LoadFrom(filename);
		}
	}
}
