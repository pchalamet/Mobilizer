using Mobilizer;
using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Reflection;

namespace Mobilize
{
	class MainClass
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Mobilize - Static assembly preprocessor supporting mobile applications");

			string filename = null;

			for (int i = 0; i < args.Length; i++)
			{
				if (args[i] == "/?")
				{
					Console.WriteLine("Usage:   Mobilize <managed.exe>");
					return;
				}
				else
				{
					filename = args[i];
				}
			}

			if (filename == null)
			{
				return;
			}

			TempFileCollection tmp = new AssemblyMobilizer().Mobilize(Assembly.LoadFrom(filename));
			tmp.KeepFiles = true;
			string destFilename = null;

			foreach (string s in tmp)
				destFilename = Path.Combine(tmp.TempDir, s);

			File.Copy(destFilename, Path.Combine(Path.GetDirectoryName(filename), "m_" + Path.GetFileName(filename)));
		}
	}
}
