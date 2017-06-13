using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

namespace Mobilizer
{
	public class NewOld
	{
		private IDictionary _map;

		public NewOld()	{ _map = new Hashtable(); }

		public ConstructorBuilder Bld(ConstructorInfo c)
		{
			if (!_map.Contains(c) || !(_map[c] is ConstructorBuilder))
				throw new InvalidOperationException("No ConstructorBuilder registered");

			return (ConstructorBuilder) _map[c];
		}

		public MethodBuilder Bld(MethodInfo m) 
		{
			if (!_map.Contains(m) || !(_map[m] is MethodBuilder))
				throw new InvalidOperationException("No MethodBuilder registered");

			return (MethodBuilder) _map[m];
		}

		public TypeBuilder Bld(Type t)
		{
			if (!_map.Contains(t) || !(_map[t] is TypeBuilder))
				throw new InvalidOperationException("No TypeBuilder registered");

			return (TypeBuilder) _map[t];
		}

        public ConstructorInfo CtorFor(ConstructorInfo c) 
		{
			return _map.Contains(c) ? (ConstructorInfo) _map[c] : c;
		}

		public ConstructorInfo[] Ctors
		{
			get
			{
				ArrayList cs = new ArrayList();

				foreach (object o in _map.Keys)
					if (o is ConstructorInfo)
						cs.Add(o);

				return (ConstructorInfo[]) cs.ToArray(typeof(ConstructorInfo));
			}
		}

		public EnumBuilder EnumBld(Type t) 
		{
			if (!_map.Contains(t) || !(_map[t] is EnumBuilder))
				throw new InvalidOperationException("No EnumBuilder registered");

			return (EnumBuilder) _map[t];
		}

		public EnumBuilder[] EnumBuilders
		{
			get
			{
				ArrayList ebs = new ArrayList();

				foreach (object o in _map.Values)
					if (o is EnumBuilder)
						ebs.Add(o);

				return (EnumBuilder[]) ebs.ToArray(typeof(EnumBuilder));
			}
		}

		public Type EnumFor(Type e)
		{
			return _map.Contains(e) ? (Type) _map[e] : e;
		}

		public EventInfo EventFor(EventInfo e)
		{
			return _map.Contains(e) ? (EventInfo) _map[e] : e;
		}

		public FieldInfo FieldFor(FieldInfo f)
		{
			return _map.Contains(f) ? (FieldInfo) _map[f] : f;
		}

		public MethodInfo MethodFor(MethodInfo m) 
		{
			return _map.Contains(m) ? (MethodInfo) _map[m] : m;
		}

		public MethodBase MethodBaseFor(MethodBase m)
		{
			return _map.Contains(m) ? (MethodBase) _map[m] : m;
		}

		public MethodInfo[] Methods
		{
			get
			{
				ArrayList ms = new ArrayList();

				foreach (object o in _map.Keys)
					if (o is MethodInfo)
						ms.Add(o);

				return (MethodInfo[]) ms.ToArray(typeof(MethodInfo));
			}
		}

		public PropertyInfo PropertyFor(PropertyInfo p)
		{
			return _map.Contains(p) ? (PropertyInfo) _map[p] : p;
		}

		public void Register(object o, object cpyO)
		{
			if (_map.Contains(o))
				throw new InvalidOperationException("Already registered");
			else if (o == null)
				throw new ArgumentNullException("o");
			else if (cpyO == null)
				throw new ArgumentNullException("cpyO");
			else if (o.GetType().IsAssignableFrom(cpyO.GetType()))
				throw new ArgumentOutOfRangeException("cpyO", cpyO, "Should be assignable to " + o.GetType());

			_map[o] = cpyO;
		}

		public Type TypeFor(Type t) 
		{
			if (t.IsArray)
			{
				// FIXME: This doesn't work for type builders
				// return Array.CreateInstance(TypeFor(t.GetElementType()), 0).GetType();

				Type elementType = TypeFor(t.GetElementType());
				return elementType.Module.GetType(elementType.FullName + "[]");
			}
			else
				return _map.Contains(t) ? (Type) _map[t] : t;
		}

		public Type[] TypesFor(Type[] t)
		{
			Type[] ts = new Type[t.Length];

			for (int i = 0; i < ts.Length; i++)
				ts[i] = TypeFor(t[i]);

			return ts;
		}

		public TypeBuilder[] TypeBuilders
		{
			get 
			{
				ArrayList tbs = new ArrayList();

				foreach (object o in _map.Keys)
					if (o is Type)
						tbs.Add(o);

				tbs.Sort(new CreationOrderComparer());

				for (int i = 0; i < tbs.Count; i++)
					tbs[i] = Bld((Type) tbs[i]);

				return (TypeBuilder[]) tbs.ToArray(typeof(TypeBuilder));
			}
		}
	}
}
