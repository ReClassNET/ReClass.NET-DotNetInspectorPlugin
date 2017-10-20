using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using Microsoft.Diagnostics.Runtime;

namespace DotNetInspectorPlugin
{
	[DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
	internal class DotNetObject : IComparable<DotNetObject>
	{
		public string DebuggerDisplay => $"{Type.Name} {Name}";

		public DotNetObject Parent { get; }

		public ulong Reference { get; }
		public string Name { get; }
		public ClrType Type { get; }
		public ClrInstanceField Field { get; }

		public bool IsValueType =>
			IsFloatingPointValueType ||
			IsIntegerValueType ||
			Type.ElementType == ClrElementType.Boolean ||
			Type.ElementType == ClrElementType.Char ||
			Type.ElementType == ClrElementType.FunctionPointer ||
			Type.ElementType == ClrElementType.String;

		public bool IsFloatingPointValueType =>
			Type.ElementType == ClrElementType.Double ||
			Type.ElementType == ClrElementType.Float;

		public bool IsIntegerValueType =>
			Type.ElementType == ClrElementType.Int8 ||
			Type.ElementType == ClrElementType.Int16 ||
			Type.ElementType == ClrElementType.Int32 ||
			Type.ElementType == ClrElementType.Int64 ||
			Type.ElementType == ClrElementType.UInt8 ||
			Type.ElementType == ClrElementType.UInt16 ||
			Type.ElementType == ClrElementType.UInt32 ||
			Type.ElementType == ClrElementType.UInt64 ||
			Type.ElementType == ClrElementType.NativeInt ||
			Type.ElementType == ClrElementType.NativeUInt;

		public List<DotNetObject> Children { get; } = new List<DotNetObject>();

		public DotNetObject(ulong reference, ClrType type, string name)
			: this(null, null, reference, type, name)
		{

		}

		public DotNetObject(DotNetObject parent, ClrInstanceField field, ulong reference, ClrType type, string name)
		{
			Contract.Requires(type != null);

			Parent = parent;
			Field = field;
			Reference = reference;
			Type = type;

			var rootName = GetRootName();
			if (!string.IsNullOrEmpty(rootName) && name.StartsWith(rootName))
			{
				name = name.Remove(0, rootName.Length);
			}

			Name = name;
		}

		public string GetFullName()
		{
			return GetFullNamespace();
		}

		private string GetRootName()
		{
			if (Parent == null)
			{
				return Name ?? string.Empty;
			}

			return Parent.GetRootName();
		}

		private string GetFullNamespace()
		{
			if (Parent == null)
			{
				return GetRootName();
			}

			return Parent.GetFullNamespace() + "." + Name;
		}

		public object GetValue()
		{
			return Field.GetValue(Reference);
		}

		public string GetFormattedValue(bool hex)
		{
			const string NullString = "null";

			if (Field != null && IsValueType)
			{
				var value = GetValue();

				if (Type.ElementType == ClrElementType.Boolean)
				{
					return (bool)value ? bool.TrueString : bool.FalseString;
				}
				if (Type.ElementType == ClrElementType.Char)
				{
					return $"'{value}'";
				}
				if (Type.ElementType == ClrElementType.String)
				{
					if (value == null)
					{
						return NullString;
					}
					return $"\"{value}\"";
				}
				if (IsFloatingPointValueType || IsIntegerValueType)
				{
					if (value is IFormattable formattable)
					{
						string format = null;
						if (IsIntegerValueType && hex)
						{
							format = "X";
						}
						return formattable.ToString(format, CultureInfo.InvariantCulture);
					}
					return value.ToString();
				}
				if (Type.ElementType == ClrElementType.FunctionPointer)
				{
					return $"0x{value:X}";
				}
			}

			return $"{{{Type}}}";
		}

		public void SetValue(object value)
		{

		}

		public int CompareTo(DotNetObject other)
		{
			return string.Compare(Name, other?.Name, StringComparison.Ordinal);
		}
	}
}
