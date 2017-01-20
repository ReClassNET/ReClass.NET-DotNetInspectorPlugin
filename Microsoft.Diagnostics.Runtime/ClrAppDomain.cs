using System.Collections.Generic;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime
{
	/// <summary>
	/// Represents an AppDomain in the target runtime.
	/// </summary>
	public abstract class ClrAppDomain
    {
        /// <summary>
        /// Address of the AppDomain.
        /// </summary>
        public abstract Address Address { get; }

        /// <summary>
        /// The AppDomain's ID.
        /// </summary>
        public abstract int Id { get; }

        /// <summary>
        /// The name of the AppDomain, as specified when the domain was created.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// To string override.
        /// </summary>
        /// <returns>The name of this AppDomain.</returns>
        public override string ToString()
        {
            return Name;
        }
    }
}
