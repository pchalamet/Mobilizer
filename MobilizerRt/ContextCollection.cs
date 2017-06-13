using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MobilizerRt
{
	[Serializable]
	public class ContextCollection
	{
		internal readonly ArrayList _ctx = ArrayList.Synchronized(new ArrayList());
		[NonSerialized] private ArrayList _finished;
		[NonSerialized] private object _host;
		[NonSerialized] internal object _target;
		internal readonly IDictionary _threadCtx = Hashtable.Synchronized(new Hashtable());

		public ContextCollection() : this(null) {}

		public ContextCollection(object host)
		{
			_host = host;
		}

		public MobileContext[] FinishedContexts
		{
			get
			{
				return _finished == null
					? new MobileContext[] {}
					: _finished.ToArray(typeof(MobileContext)) as MobileContext[];
			}
		}

		public MobileContext[] Contexts
		{
			get
			{
				return _ctx.ToArray(typeof(MobileContext)) as MobileContext[];
			}
		}

		public object Host
		{
			get { return _host; }
			set { _host = value; }
		}

		public object WaitForAll()
		{
			while (_threadCtx.Count > 0)
				Thread.Sleep(0);

			object target = _target;
			_target = null;
			return target;
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		internal void AddFinishedContext(MobileContext context)
		{
			if (_finished == null)
			{
				_finished = ArrayList.Synchronized(new ArrayList());
			}

			_finished.Add(context);
		}
	}
}
