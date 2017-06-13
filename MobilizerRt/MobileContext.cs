using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MobilizerRt
{
	[Serializable]
	public class MobileContext
	{
		private static readonly LocalDataStoreSlot ThreadOwner = Thread.AllocateDataSlot();

		public static object Host
		{
			get { return Instance.Owner.Host; }
		}

		public static bool IsUnwinding()
		{
			return Instance.Owner._target != null && !Instance.IsEmpty;
		}

		public static bool IsUnwindPending()
		{
			return Instance.Owner._target != null && Instance.IsEmpty && Instance._nlocks == 0;
		}

		public static void Lock()
		{
			if (Instance != null)
				Instance._nlocks++;
		}

		public static object Pop()
		{			
			return Instance._stack.Pop();
		}

		public static int PopInt()
		{
			return (int) Pop();
		}

		public static void Push(object o)
		{
			Instance._stack.Push(o);
		}

		public static void PushInt(int i)
		{
			Push(i);
		}

		public static void RequestUnwind(object target)
		{
			if (target == null)
				throw new ArgumentNullException("target");

			// ensure no threads are still restoring
			outer:	foreach (MobileContext m in Instance.Owner._ctx)
						if (m.IsRestoring) 
						{
							Thread.Sleep(0);
							goto outer;
						}

			Instance.Owner._target = target;
		}

		public static bool Restoring()
		{
			return Instance.IsRestoring;
		}

		public static void Unlock()
		{
			if (Instance != null)
				Instance._nlocks--;
		}

		private static MobileContext Instance
		{
			get
			{
				ContextCollection owner = (ContextCollection) Thread.GetData(ThreadOwner);

				if (owner == null)
				{
					// this thread is not in a mobile context
					return null;
				}
				else
				{
					return (MobileContext) owner._threadCtx[Thread.CurrentThread];
				}
			}
		}

		[NonSerialized] private int _nlocks;	/* not serialized */
		[NonSerialized] private Thread _t;		/* not serialized */

		private ContextCollection owner;
		private object[] args;
		private Stack _stack;
		private object target;
		private MethodInfo method;
		private IDictionary properties;

		public MobileContext(ContextCollection owner, object target, MethodInfo method)
			: this(owner, target, method, new object[] {}) {}

		public MobileContext(ContextCollection owner, object target, MethodInfo method, object[] args)
		{
			this.owner = owner;
			this.target = target;
			this.method = method;
			this.args = args;
			_stack = new Stack();
			properties = new Hashtable();
		}

		public object this[object key]
		{
			get { return properties[key]; }
			set { properties[key] = value; }
		}
		
		public MethodInfo Method
		{
			get
			{
				return method;
			}
		}

		public ContextCollection Owner
		{
			get
			{
				return owner;
			}
		}

		public void Start(bool background)
		{
			if (_t != null)
				throw new InvalidOperationException("Already started");

			if (background)
			{
				_t = new Thread(new ThreadStart(Run));
			}
			else
			{
				_t = Thread.CurrentThread;
			}

			Owner._threadCtx.Add(_t, this);
			Owner._ctx.Remove(this);

			if (background)
			{
				_t.Start();
			}
			else
			{
				Run();
			}
		}

		internal bool IsEmpty
		{
			get { return _stack.Count == 0; }
		}

		internal bool IsRestoring
		{
			get { return Owner._target == null && !IsEmpty; }
		}

		private void Run()
		{
			Thread.SetData(ThreadOwner, owner);

			try
			{
				object result = method.Invoke(target, args);

				if (IsEmpty)
				{
					this["ReturnValue"] = result;
				}
			}
			catch (Exception e)
			{
				// empty stack; this thread will not be saved because of it
				Console.Error.WriteLine("{0}", e);
				_stack = new Stack();
			}
			finally
			{
				if (!IsEmpty)
				{
					Owner._ctx.Add(this);
				}
				else
				{
					Owner.AddFinishedContext(this);
				}

				Owner._threadCtx.Remove(_t);
				_t = null;
				_nlocks = 0;
			}
		}
	}
}
