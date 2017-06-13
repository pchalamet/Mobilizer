using System;

namespace MobilizerRt
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method)]
	public class AtomicAttribute : Attribute
	{
		public AtomicAttribute() {}
	}
}
