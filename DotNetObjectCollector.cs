using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Diagnostics.Runtime;

namespace DotNetInspectorPlugin
{
	class DotNetObjectCollector
	{
		private ClrHeap heap;

		public DotNetObjectCollector(ClrHeap heap)
		{
			Contract.Requires(heap != null);

			this.heap = heap;
		}

		public IEnumerable<DotNetObject> EnumerateObjects()
		{
			var roots = new List<DotNetObject>();
			var visited = new HashSet<ulong>();

			foreach (var root in heap.EnumerateRoots())
			{
				heap.EnumerateObjectAddresses();
				if (root == null || root.Type == null || root.Name == null)
				{
					continue;
				}

				var name = root.Name;
				string prefix;
				if ((prefix = ClrGlobals.ExcludePrefix.FirstOrDefault(p => name.StartsWith(p, StringComparison.InvariantCultureIgnoreCase))) != null)
				{
					name = name.Substring(prefix.Length);
				}
				if (ClrGlobals.ExcludeRootNamespaces.Any(n => name.StartsWith(n, StringComparison.InvariantCultureIgnoreCase)))
				{
					continue;
				}

				if (ClrGlobals.ExcludeNames.Any(n => name.StartsWith(n, StringComparison.InvariantCultureIgnoreCase)))
				{
					continue;
				}

				var dot = name.LastIndexOf('.');
				if (dot != -1)
				{
					name = name.Substring(dot + 1);
				}

				if (!visited.Add(root.Object))
				{
					continue;
				}

				var obj = new DotNetObject(root.Object, root.Type, name);
				roots.Add(obj);

				RecursiveEnumerateObjects(visited, obj, obj.Reference, obj.Type);
			}

			return roots;
		}

		private void RecursiveEnumerateObjects(HashSet<ulong> visited, DotNetObject parent, ulong reference, ClrType clrType)
		{
			Contract.Requires(visited != null);
			Contract.Requires(parent != null);

			foreach (var field in clrType.Fields)
			{
				if (ClrGlobals.ExcludeNames.Any(n => field.Name.StartsWith(n, StringComparison.InvariantCultureIgnoreCase)))
				{
					continue;
				}

				var fieldObj = new DotNetObject(parent, field, reference + (ulong)field.Offset, field.Type, field.Name);

				parent.Children.Add(fieldObj);

				if (field.ElementType == ClrElementType.Object || field.ElementType == ClrElementType.Array || field.ElementType == ClrElementType.Struct)
				{
					try
					{
						var refObjectAddress = (ulong)field.GetValue(reference);
						if (refObjectAddress == 0)
						{
							continue;
						}

						if (!visited.Add(refObjectAddress))
						{
							continue;
						}

						var refObjectClrType = heap.GetObjectType(refObjectAddress);
						if (refObjectClrType == null)
						{
							continue;
						}

						RecursiveEnumerateObjects(visited, fieldObj, refObjectAddress, refObjectClrType);
					}
					catch (Exception ex)
					{
						// field.GetValue may throw
					}
				}
			}

			parent.Children.Sort();
		}
	}

	public class ClrGlobals
	{
		public static readonly string[] ExcludeRootNamespaces = new string[]
		{
			"System.",
			"Microsoft.",
			"MS.",
			"<CppImplementationDetails>.",
			"<CrtImplementationDetails>.",
			"Newtonsoft.",
		};

		public static readonly string[] ExcludeNames = new string[]
		{
			"finalization handle",
			"strong handle",
			"pinned handle",
			"RefCount handle",
			"local var"
		};

		public static readonly string[] ExcludePrefix = new string[]
		{
			"static var "
		};
	}
}
