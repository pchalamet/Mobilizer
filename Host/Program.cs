using MobilizerRt;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;

namespace Host
{
	class Program
	{
		static void Main(string[] args)
		{
			try
			{
				if (args.Length != 2 && args.Length != 3)
				{
					Console.Error.WriteLine(
@"Usage: host assembly-filename type-name method-name
       host -listen port");
					return;
				}

				if (args.Length == 2 && args[0] == "-listen")
				{
					TcpListener listener = new TcpListener(int.Parse(args[1]));
					listener.Start();

					while (true)
					{
						TcpClient client = listener.AcceptTcpClient();
						Stream input = client.GetStream();
						BinaryFormatter formatter = new BinaryFormatter();
						
						byte[] rawAssembly = (byte[])formatter.Deserialize(input);
						Assembly assembly = Assembly.Load(rawAssembly);

						ContextCollection owner =
							(ContextCollection)formatter.Deserialize(input);
						client.Close();

						foreach (MobileContext ctx in owner.Contexts)
						{
							ctx.Start(true);
						}

						WaitAndTransfer(owner, rawAssembly);
					}
				}
				else
				{
					byte[] rawAssembly;

					using (Stream s = File.OpenRead(args[0]))
					{
						rawAssembly = new byte[s.Length];
						s.Read(rawAssembly, 0, rawAssembly.Length);
						s.Close();
					}

					Assembly assembly = Assembly.Load(rawAssembly);
					Type type = assembly.GetType(args[1], true, true);
					MethodInfo method = type.GetMethod(
						args[2],
						BindingFlags.Public | BindingFlags.Static,
						null,
						Type.EmptyTypes,
						null);

					ContextCollection owner = new ContextCollection();
					MobileContext ctx = new MobileContext(owner, null, method);
					ctx.Start(true);

					WaitAndTransfer(owner, rawAssembly);
				}
			}
			catch (Exception exception)
			{
				Console.Error.WriteLine(exception.ToString());
			}
		}

		static void WaitAndTransfer(ContextCollection context, byte[] rawAssembly)
		{
			IPEndPoint target = (IPEndPoint) context.WaitForAll();

			if (target != null)
			{
				Console.WriteLine("Transferring to: {0}", target);
				TcpClient client = new TcpClient();
				client.Connect(target);

				BinaryFormatter formatter = new BinaryFormatter();
				formatter.AssemblyFormat = FormatterAssemblyStyle.Simple;
				formatter.TypeFormat = FormatterTypeStyle.TypesAlways;
				Stream output = client.GetStream();
				formatter.Serialize(output, rawAssembly);
				formatter.Serialize(output, context);
				client.Close();
			}
		}
	}
}
