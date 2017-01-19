using System.Collections.Generic;
using System.Diagnostics;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime
{

	/// <summary>
	/// Represents a managed module in the target process.
	/// </summary>
	public abstract class ClrModule
    {
        /// <summary>
        /// Returns a list of all AppDomains this module is loaded into.  Please note that unlike
        /// ClrRuntime.AppDomains, this list may include the shared AppDomain.
        /// </summary>
        public abstract IList<ClrAppDomain> AppDomains { get; }

        /// <summary>
        /// Returns the name of the assembly that this module is defined in.
        /// </summary>
        public abstract string AssemblyName { get; }

        /// <summary>
        /// Returns the name of the module.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Returns true if this module was created through Reflection.Emit (and thus has no associated
        /// file).
        /// </summary>
        public abstract bool IsDynamic { get; }

        /// <summary>
        /// Returns the filename of where the module was loaded from on disk.  Undefined results if
        /// IsPEFile returns false.
        /// </summary>
        public abstract string FileName { get; }

        /// <summary>
        /// Returns the base of the image loaded into memory.  This may be 0 if there is not a physical
        /// file backing it.
        /// </summary>
        public abstract Address ImageBase { get; }

        /// <summary>
        /// Returns the size of the image in memory.
        /// </summary>
        public abstract ulong Size { get; }

        /// <summary>
        /// Enumerate all types defined by this module.
        /// </summary>
        public abstract IEnumerable<ClrType> EnumerateTypes();

        /// <summary>
        /// Attempts to obtain a ClrType based on the name of the type.  Note this is a "best effort" due to
        /// the way that the dac handles types.  This function will fail for Generics, and types which have
        /// never been constructed in the target process.  Please be sure to null-check the return value of
        /// this function.
        /// </summary>
        /// <param name="name">The name of the type.  (This would be the EXACT value returned by ClrType.Name.</param>
        /// <returns>The requested ClrType, or null if the type doesn't exist or couldn't be constructed.</returns>
        public abstract ClrType GetTypeByName(string name);

        /// <summary>
        /// Returns a name for the assembly.
        /// </summary>
        /// <returns>A name for the assembly.</returns>
        public override string ToString()
        {
            if (string.IsNullOrEmpty(Name))
            {
                if (!string.IsNullOrEmpty(AssemblyName))
                    return AssemblyName;

                if (IsDynamic)
                    return "dynamic";
            }

            return Name;
        }
    }
}
