// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopAppDomain : ClrAppDomain
    {
        /// <summary>
        /// Address of the AppDomain.
        /// </summary>
        public override Address Address { get { return _address; } }

        /// <summary>
        /// The AppDomain's ID.
        /// </summary>
        public override int Id { get { return _id; } }

        /// <summary>
        /// The name of the AppDomain, as specified when the domain was created.
        /// </summary>
        public override string Name { get { return _name; } }

        internal DesktopAppDomain(DesktopRuntimeBase runtime, IAppDomainData data, string name)
        {
            _address = data.Address;
            _id = data.Id;
            _name = name;
            _runtime = runtime;
        }

        #region Private

        private Address _address;
        private string _name;
        private int _id;
        private DesktopRuntimeBase _runtime;

        #endregion
    }
}
