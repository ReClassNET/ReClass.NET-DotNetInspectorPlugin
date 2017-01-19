// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Address = System.UInt64;
using System.Runtime.InteropServices;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
	internal abstract class DesktopBaseModule : ClrModule
    {
        protected DesktopRuntimeBase _runtime;

        public override ClrRuntime Runtime
        {
            get
            {
                return _runtime;
            }
        }

        internal abstract Address GetDomainModule(ClrAppDomain appDomain);

        internal Address ModuleId { get; set; }

        internal virtual ICorDebug.IMetadataImport GetMetadataImport()
        {
            return null;
        }

        public int Revision { get; set; }

        public DesktopBaseModule(DesktopRuntimeBase runtime)
        {
            _runtime = runtime;
    }
    }

    internal class DesktopModule : DesktopBaseModule
    {
        private bool _reflection, _isPE;
        private string _name, _assemblyName;
        private ICorDebug.IMetadataImport _metadata;
        private Dictionary<ClrAppDomain, ulong> _mapping = new Dictionary<ClrAppDomain, ulong>();
        private Address _imageBase, _size;
        private Address _metadataStart;
        private Address _metadataLength;
        private DebuggableAttribute.DebuggingModes? _debugMode;
        private Address _assemblyAddress;
        private bool _typesLoaded;
        ClrAppDomain[] _appDomainList;

        public DesktopModule(DesktopRuntimeBase runtime, IModuleData data, string name, string assemblyName, ulong size)
            : base(runtime)
        {
            Revision = runtime.Revision;
            _imageBase = data.ImageBase;
            _assemblyName = assemblyName;
            _isPE = data.IsPEFile;
            _reflection = data.IsReflection || string.IsNullOrEmpty(name) || !name.Contains("\\");
            _name = name;
            ModuleId = data.ModuleId;
            ModuleIndex = data.ModuleIndex;
            _metadataStart = data.MetdataStart;
            _metadataLength = data.MetadataLength;
            _assemblyAddress = data.Assembly;
            _size = size;

			_metadata = data.LegacyMetaDataImport as ICorDebug.IMetadataImport;
		}

        internal ulong GetMTForDomain(ClrAppDomain domain, DesktopHeapType type)
        {
            DesktopGCHeap heap = null;
            var mtList = _runtime.GetMethodTableList(_mapping[domain]);

            bool hasToken = type.MetadataToken != 0 && type.MetadataToken != uint.MaxValue;

            uint token = ~0xff000000 & type.MetadataToken;

            foreach (MethodTableTokenPair pair in mtList)
            {
                if (hasToken)
                {
                    if (pair.Token == token)
                        return pair.MethodTable;
                }
                else
                {
                    if (heap == null)
                        heap = (DesktopGCHeap)_runtime.GetHeap();

                    if (heap.GetTypeByMethodTable(pair.MethodTable, 0) == type)
                        return pair.MethodTable;
                }
            }

            return 0;
        }

        public override IEnumerable<ClrType> EnumerateTypes()
        {
            var heap = (DesktopGCHeap)_runtime.GetHeap();
            var mtList = _runtime.GetMethodTableList(_mapping.First().Value);
            if (_typesLoaded)
            {
                foreach (var type in heap.EnumerateTypes())
                    if (type.Module == this)
                        yield return type;
            }
            else
            {
                if (mtList != null)
                {
                    foreach (var pair in mtList)
                    {
                        ulong mt = pair.MethodTable;
                        if (mt != _runtime.ArrayMethodTable)
                        {
                            // prefetch element type, as this also can load types
                            var type = heap.GetTypeByMethodTable(mt, 0, 0);
                            if (type != null)
                                yield return type;
                        }
                    }
                }

                _typesLoaded = true;
            }
        }

        public override string AssemblyName
        {
            get { return _assemblyName; }
        }

        public override string Name
        {
            get { return _name; }
        }

        public override bool IsDynamic
        {
            get { return _reflection; }
        }

        public override bool IsFile
        {
            get { return _isPE; }
        }

        public override string FileName
        {
            get { return _isPE ? _name : null; }
        }

        internal ulong ModuleIndex { get; private set; }

        internal void AddMapping(ClrAppDomain domain, ulong domainModule)
        {
            _mapping[domain] = domainModule;
        }

        public override IList<ClrAppDomain> AppDomains
        {
            get
            {
                if (_appDomainList == null)
                {
                    _appDomainList = new ClrAppDomain[_mapping.Keys.Count];
                    _appDomainList = _mapping.Keys.ToArray();
                    Array.Sort(_appDomainList, (d, d2) => d.Id.CompareTo(d2.Id));
                }

                return _appDomainList;
            }
        }

        internal override ulong GetDomainModule(ClrAppDomain domain)
        {
            _runtime.InitDomains();
            if (domain == null)
            {
                foreach (ulong addr in _mapping.Values)
                    return addr;

                return 0;
            }

            ulong value;
            if (_mapping.TryGetValue(domain, out value))
                return value;

            return 0;
        }

        internal override ICorDebug.IMetadataImport GetMetadataImport()
        {
            if (Revision != _runtime.Revision)
                ClrDiagnosticsException.ThrowRevisionError(Revision, _runtime.Revision);

            if (_metadata != null)
                return _metadata;

            ulong module = GetDomainModule(null);
            if (module == 0)
                return null;

            _metadata = _runtime.GetMetadataImport(module);
            return _metadata;
        }

        public override Address ImageBase
        {
            get { return _imageBase; }
        }


        public override Address Size
        {
            get
            {
                return _size;
            }
        }


        public override Address MetadataAddress
        {
            get { return _metadataStart; }
        }

        public override Address MetadataLength
        {
            get { return _metadataLength; }
        }

        public override object MetadataImport
        {
            get { return GetMetadataImport(); }
        }

        public override DebuggableAttribute.DebuggingModes DebuggingMode
        {
            get
            {
                if (_debugMode == null)
                    InitDebugAttributes();

                Debug.Assert(_debugMode != null);
                return _debugMode.Value;
            }
        }

        private void InitDebugAttributes()
        {
            ICorDebug.IMetadataImport metadata = GetMetadataImport();
            if (metadata == null)
            {
                _debugMode = DebuggableAttribute.DebuggingModes.None;
                return;
            }

            try
            {
                IntPtr data;
                uint cbData;
                int hr = metadata.GetCustomAttributeByName(0x20000001, "System.Diagnostics.DebuggableAttribute", out data, out cbData);
                if (hr != 0 || cbData <= 4)
                {
                    _debugMode = DebuggableAttribute.DebuggingModes.None;
                    return;
                }

                unsafe
                {
                    byte* b = (byte*)data.ToPointer();
                    UInt16 opt = b[2];
                    UInt16 dbg = b[3];

                    _debugMode = (System.Diagnostics.DebuggableAttribute.DebuggingModes)((dbg << 8) | opt);
                }
            }
            catch (SEHException)
            {
                _debugMode = DebuggableAttribute.DebuggingModes.None;
            }
        }

        public override ClrType GetTypeByName(string name)
        {
            foreach (ClrType type in EnumerateTypes())
                if (type.Name == name)
                    return type;

            return null;
        }

        public override Address AssemblyId
        {
            get { return _assemblyAddress; }
        }
    }

    internal class ErrorModule : DesktopBaseModule
    {
        private static uint s_id = 0;
        private uint _id = s_id++;

        public ErrorModule(DesktopRuntimeBase runtime)
            : base(runtime)
        {
        }

        public override IList<ClrAppDomain> AppDomains
        {
            get
            {
                return new ClrAppDomain[0];
            }
        }

        public override string AssemblyName
        {
            get { return "<error>"; }
        }

        public override string Name
        {
            get { return "<error>"; }
        }

        public override bool IsDynamic
        {
            get { return false; }
        }

        public override bool IsFile
        {
            get { return false; }
        }

        public override string FileName
        {
            get { return "<error>"; }
        }

        public override Address ImageBase
        {
            get { return 0; }
        }

        public override Address Size
        {
            get { return 0; }
        }

        public override IEnumerable<ClrType> EnumerateTypes()
        {
            return new ClrType[0];
        }

        public override Address MetadataAddress
        {
            get { return 0; }
        }

        public override Address MetadataLength
        {
            get { return 0; }
        }

        public override object MetadataImport
        {
            get { return null; }
        }

        internal override Address GetDomainModule(ClrAppDomain appDomain)
        {
            return 0;
        }

        public override DebuggableAttribute.DebuggingModes DebuggingMode
        {
            get { return DebuggableAttribute.DebuggingModes.None; }
        }

        public override ClrType GetTypeByName(string name)
        {
            return null;
        }

        public override Address AssemblyId
        {
            get { return _id; }
        }
    }
}
