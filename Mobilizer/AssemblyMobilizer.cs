using MobilizerRt;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Diagnostics.SymbolStore;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.ILReader;
using System.Resources;

namespace Mobilizer
{
	public class AssemblyMobilizer
	{
		public const BindingFlags All = BindingFlags.DeclaredOnly |
										BindingFlags.Instance |
										BindingFlags.NonPublic |
										BindingFlags.Public |
										BindingFlags.Static;

		internal static Type[] MapTypeV(NewOld map, Type[] t)
		{
			return map.TypesFor(t);
		}

		internal static Type[] NewTypeVOfParameterInfoV(NewOld map, ParameterInfo[] ps)
		{
			return MapTypeV(map, TypeVOfParameterInfoV(ps));
		}

		internal static Type[] TypeVOfParameterInfoV(ParameterInfo[] ps)
		{
			Type[] ts = new Type[ps.Length];

			for (int i = 0; i < ps.Length; i++)
				ts[i] = ps[i].ParameterType;

			return ts;
		}

		private ISymbolDocumentWriter _doc;
		private readonly ReaderCache _rc;

		public AssemblyMobilizer()
		{
			_rc = new ReaderCache();
		}

		public TempFileCollection Mobilize(Assembly a)
		{
			// create assembly builder, module builder

			AssemblyName an = new AssemblyName();
			an.Name = a.GetName().Name;

			TempFileCollection tmp = new TempFileCollection(Environment.GetEnvironmentVariable("TEMP"));

			AssemblyBuilder cpyA = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.Save, tmp.TempDir);

			string filename = an.Name + (a.EntryPoint != null ? ".exe" : ".dll");
			ModuleBuilder mod = cpyA.DefineDynamicModule(an.Name, filename, true);
			_doc = mod.DefineDocument("", SymDocumentType.Text, SymLanguageType.ILAssembly, SymLanguageVendor.Microsoft);
			tmp.AddFile(filename, false);

			// create skeleton types

			NewOld map = new NewOld();

			Type[] ts = a.GetTypes();
			Array.Sort(ts, new CreationOrderComparer());	// we sort the types so container types are created first

			foreach (Type t in ts)
			{
				object cpyT;

				if (t.IsEnum)
					cpyT = mod.DefineEnum(t.FullName, t.Attributes & TypeAttributes.VisibilityMask, t.UnderlyingSystemType);
				else if (t.DeclaringType == null)
					cpyT = mod.DefineType(t.FullName, t.Attributes);
				else
					cpyT = map.Bld(t.DeclaringType).DefineNestedType(t.FullName.Replace("+", @"\+"), t.Attributes);

				map.Register(t, cpyT);
			}

			// fix up inheritance graph

			foreach (Type t in ts)
				if (!t.IsEnum && t.BaseType != null)
					map.Bld(t).SetParent(map.TypeFor(t.BaseType));

			// fix up interface implementation graph

			foreach (Type t in ts)
			{
				if (!t.IsEnum)
				{
					TypeBuilder cpyT = map.Bld(t);
					
					foreach (Type it in t.GetInterfaces())
					{
						if (it.IsAssignableFrom(t.BaseType))
						{
							// base type handles interface
							continue;
						}

						bool found = false;

						foreach (Type it2 in t.GetInterfaces())
						{
							if (it.IsAssignableFrom(it2) && it != it2)
							{
								found = true;
							}
						}

						if (found)
						{
							continue;
						}

						cpyT.AddInterfaceImplementation(map.TypeFor(it));
					}
				}
			}

			// create members

			foreach (Type t in ts)
				if (t.IsEnum)
				{
					EnumBuilder cpyT = map.EnumBld(t);

					foreach (string name in Enum.GetNames(t))
						cpyT.DefineLiteral(name, Enum.Parse(t, name, false));
				}
				else
				{
					TypeBuilder cpyT = map.Bld(t);

					// create fields

					foreach (FieldInfo f in t.GetFields(AssemblyMobilizer.All))
					{
						FieldBuilder fb = cpyT.DefineField(f.Name, map.TypeFor(f.FieldType), f.Attributes);
						// FIXME: set constant value of field
						map.Register(f, fb);
					}

					// create constructors

					foreach (ConstructorInfo c in t.GetConstructors(AssemblyMobilizer.All & ~BindingFlags.Static))
					{
						ConstructorBuilder cb = cpyT.DefineConstructor(c.Attributes, c.CallingConvention, NewTypeVOfParameterInfoV(map, c.GetParameters()));

						foreach (ParameterInfo pi in c.GetParameters())
						{
							ParameterBuilder pb = cb.DefineParameter(pi.Position + (c.IsStatic ? 0 : 1), pi.Attributes, pi.Name);

							if (pi.DefaultValue != DBNull.Value)
								pb.SetConstant(pi.DefaultValue);
						}

						map.Register(c, cb);
					}

					// create type initializer
					if (t.TypeInitializer != null)
						map.Register(t.TypeInitializer, cpyT.DefineTypeInitializer());

					// create methods

					foreach (MethodInfo m in t.GetMethods(AssemblyMobilizer.All))
					{
						// FIXME: define method overrides, not just methods
						MethodBuilder mb = cpyT.DefineMethod(m.Name, m.Attributes, m.CallingConvention, m.ReturnType == null ? null : map.TypeFor(m.ReturnType), NewTypeVOfParameterInfoV(map, m.GetParameters()));

						foreach (ParameterInfo pi in m.GetParameters())
						{
							ParameterBuilder pb = mb.DefineParameter(pi.Position + 1, pi.Attributes, pi.Name);

							if (pi.DefaultValue != DBNull.Value)
								pb.SetConstant(pi.DefaultValue);
						}

						map.Register(m, mb);
					}

					// create events

					foreach (EventInfo e in t.GetEvents(AssemblyMobilizer.All))
					{
						EventBuilder eb = cpyT.DefineEvent(e.Name, e.Attributes, map.TypeFor(e.EventHandlerType));
						map.Register(e, eb);

						if (e.GetAddMethod(true) != null)
							eb.SetAddOnMethod(map.Bld(e.GetAddMethod(true)));

						if (e.GetRemoveMethod(true) != null)
							eb.SetRemoveOnMethod(map.Bld(e.GetRemoveMethod(true)));

						if (e.GetRaiseMethod(true) != null)
							eb.SetRaiseMethod(map.Bld(e.GetRaiseMethod(true)));
					}

					// create properties

					foreach (PropertyInfo pi in t.GetProperties(AssemblyMobilizer.All))
					{
						PropertyBuilder pb = cpyT.DefineProperty(pi.Name, pi.Attributes, map.TypeFor(pi.PropertyType), NewTypeVOfParameterInfoV(map, pi.GetIndexParameters()));
						map.Register(pi, pb);

						if (pi.GetGetMethod(true) != null)
							pb.SetGetMethod(map.Bld(pi.GetGetMethod(true)));

						if (pi.GetSetMethod(true) != null)
							pb.SetSetMethod(map.Bld(pi.GetSetMethod(true)));
					}
				}

			// define members

			foreach (ConstructorInfo c in map.Ctors)
				DefineCtor(map, c);

			foreach (MethodInfo m in map.Methods)
				if (!m.IsAbstract)
					DefineMethod(map, m);

			// set the entry point (if any)
			if (a.EntryPoint != null)
				cpyA.SetEntryPoint(map.MethodFor(a.EntryPoint)); // FIXME set file kind

			// bake and return assembly
			
			foreach (EnumBuilder eb in map.EnumBuilders)
				eb.CreateType();

			foreach (TypeBuilder tb in map.TypeBuilders)
				tb.CreateType();

			cpyA.Save(filename);

			return tmp;
		}

		internal void DefineCtor(NewOld map, ConstructorInfo c)
		{
			new DefineAtomicMethod(map, c, _rc, _doc).DefineMethod();
		}

		internal void DefineMethod(NewOld map, MethodInfo m)
		{
			if (IsAtomic(m))
				new DefineAtomicMethod(map, m, _rc, _doc).DefineMethod();
			else
				new DefineMobileMethod(map, m, _rc, _doc).DefineMethod();
		}

		internal bool IsAtomic(MethodInfo m)
		{
			return m.GetCustomAttributes(typeof(AtomicAttribute), false).Length > 0 ||
					   m.DeclaringType.GetCustomAttributes(typeof(AtomicAttribute), false).Length > 0;
		}
	}
}
