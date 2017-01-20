// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime.Native
{
    internal class NativeAppDomain : ClrAppDomain
    {
	    public override Address Address
        {
            get { return 0; }
        }

        public override int Id
        {
            get { return 0; }
        }

        public override string Name
        {
            get { return "default domain"; }
        }
    }


    internal class NativeModule : ClrModule
    {
        private NativeRuntime _runtime;
        private string _name;
        private string _filename;
        private Address _imageBase;
        private Address _size;

        public NativeModule(NativeRuntime runtime, ModuleInfo module)
        {
            _runtime = runtime;
            _name = string.IsNullOrEmpty(module.FileName) ? "" : Path.GetFileNameWithoutExtension(module.FileName);
            _filename = module.FileName;
            _imageBase = module.ImageBase;
            _size = module.FileSize;
        }

        public override IList<ClrAppDomain> AppDomains
        {
            get
            {
                return new[] { _runtime.AppDomains[0] };
            }
        }

        public override string AssemblyName
        {
            get { return _name; }
        }

        public override string Name
        {
            get { return _name; }
        }

        public override bool IsDynamic
        {
            get { return false; }
        }

        public override string FileName
        {
            get { return _filename; }
        }

        public override Address ImageBase
        {
            get { return _imageBase; }
        }

        public override Address Size
        {
            get { return _size; }
        }

        public override IEnumerable<ClrType> EnumerateTypes()
        {
            foreach (var type in _runtime.GetHeap().EnumerateTypes())
                if (type.Module == this)
                    yield return type;
        }

        internal int ComparePointer(Address eetype)
        {
            if (eetype < ImageBase)
                return -1;

            if (eetype >= ImageBase + Size)
                return 1;

            return 0;
        }

        public override ClrType GetTypeByName(string name)
        {
            throw new NotImplementedException();
        }
    }
}