using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.ILReader;

namespace Mobilizer
{
	public class RestorePoint
	{
		public MethodInfo Call;
		public Label Label;
		public int Offset;
		public Label RestoreHandlerLabel;

		public RestorePoint(MethodInfo call, Instruction i, ILGenerator g)
		{
			Call = call;
			Offset = i.Offset;
			Label = g.DefineLabel();
			RestoreHandlerLabel = Label; // will be overwritten in DefineMobileMethod::DefineMethod
		}

		public RestorePoint(Instruction i, ILGenerator g) : this(null, i, g) {}

		// A nop-before-.try restore point
		public RestorePoint(ILGenerator g) 
		{
			Call = null;
			Offset = -1;
			Label = g.DefineLabel();
			RestoreHandlerLabel = Label;
		}
	}
}
