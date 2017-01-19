// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Runtime.Desktop;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
	/// <summary>
	/// Represents the version of a DLL.
	/// </summary>
	[Serializable]
    public struct VersionInfo
    {
        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'A'.
        /// </summary>
        public int Major;

        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'B'.
        /// </summary>
        public int Minor;

        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'C'.
        /// </summary>
        public int Revision;

        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'D'.
        /// </summary>
        public int Patch;

        internal VersionInfo(int major, int minor, int revision, int patch)
        {
            Major = major;
            Minor = minor;
            Revision = revision;
            Patch = patch;
        }

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>The A.B.C.D version prepended with 'v'.</returns>
        public override string ToString()
        {
            return string.Format("v{0}.{1}.{2}.{3:D2}", Major, Minor, Revision, Patch);
        }
    }

    /// <summary>
    /// Returns the "flavor" of CLR this module represents.
    /// </summary>
    public enum ClrFlavor
    {
        /// <summary>
        /// This is the full version of CLR included with windows.
        /// </summary>
        Desktop = 0,

        /// <summary>
        /// This is a reduced CLR used in other projects.
        /// </summary>
        CoreCLR = 1,
        
        /// <summary>
        /// Used for .Net Native.
        /// </summary>
        Native = 2
    }

    /// <summary>
    /// Represents information about a single Clr runtime in a process.
    /// </summary>
    [Serializable]
    public class ClrInfo : IComparable
    {
        /// <summary>
        /// The version number of this runtime.
        /// </summary>
        public VersionInfo Version { get { return ModuleInfo.Version; } }

        /// <summary>
        /// The type of CLR this module represents.
        /// </summary>
        public ClrFlavor Flavor { get; private set; }

        /// <summary>
        /// Returns module information about the Dac needed create a ClrRuntime instance for this runtime.
        /// </summary>
        public DacInfo DacInfo { get; private set; }

        /// <summary>
        /// Returns module information about the ClrInstance.
        /// </summary>
        public ModuleInfo ModuleInfo { get; private set; }

        /// <summary>
        /// Creates a runtime from the given Dac file on disk.
        /// </summary>
        public ClrRuntime CreateRuntime()
        {
            string dac = _dacLocation;
            if (dac != null && !File.Exists(dac))
                dac = null;

            if (!File.Exists(dac))
                throw new FileNotFoundException(DacInfo.FileName);

            if (IntPtr.Size != (int)_dataTarget.DataReader.GetPointerSize())
                throw new InvalidOperationException("Mismatched architecture between this process and the dac.");

            return ConstructRuntime(dac);
        }

        private ClrRuntime ConstructRuntime(string dac)
        {
            if (IntPtr.Size != (int)_dataTarget.DataReader.GetPointerSize())
                throw new InvalidOperationException("Mismatched architecture between this process and the dac.");

            DacLibrary lib = new DacLibrary(_dataTarget, dac);

            Desktop.DesktopVersion ver;
            if (Flavor == ClrFlavor.CoreCLR)
            {
                return new Desktop.V45Runtime(this, _dataTarget, lib);
            }
            else if (Flavor == ClrFlavor.Native)
            {
                return new Native.NativeRuntime(this, _dataTarget, lib);
            }
            else if (Version.Major == 2)
            {
                ver = Desktop.DesktopVersion.v2;
            }
            else if (Version.Major == 4 && Version.Minor == 0 && Version.Patch < 10000)
            {
                ver = Desktop.DesktopVersion.v4;
            }
            else
            {
                // Assume future versions will all work on the newest runtime version.
                return new Desktop.V45Runtime(this, _dataTarget, lib);
            }

            return new Desktop.LegacyRuntime(this, _dataTarget, lib, ver, Version.Patch);
        }

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>A version string for this Clr runtime.</returns>
        public override string ToString()
        {
            return Version.ToString();
        }

        internal ClrInfo(DataTargetImpl dt, ClrFlavor flavor, ModuleInfo module, DacInfo dacInfo, string dacLocation)
        {
            Debug.Assert(dacInfo != null);

            Flavor = flavor;
            DacInfo = dacInfo;
            ModuleInfo = module;
            module.IsRuntime = true;
            _dataTarget = dt;
            _dacLocation = dacLocation;
        }

        private string _dacLocation;
        private DataTargetImpl _dataTarget;

        /// <summary>
        /// IComparable.  Sorts the object by version.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>-1 if less, 0 if equal, 1 if greater.</returns>
        public int CompareTo(object obj)
        {
            if (obj == null)
                return 1;

            if (!(obj is ClrInfo))
                throw new InvalidOperationException("Object not ClrInfo.");

            ClrFlavor flv = ((ClrInfo)obj).Flavor;
            if (flv != Flavor)
                return flv.CompareTo(Flavor);  // Intentionally reversed.

            VersionInfo rhs = ((ClrInfo)obj).Version;
            if (Version.Major != rhs.Major)
                return Version.Major.CompareTo(rhs.Major);


            if (Version.Minor != rhs.Minor)
                return Version.Minor.CompareTo(rhs.Minor);


            if (Version.Revision != rhs.Revision)
                return Version.Revision.CompareTo(rhs.Revision);

            return Version.Patch.CompareTo(rhs.Patch);
        }
    }

    /// <summary>
    /// Specifies how to attach to a live process.
    /// </summary>
    public enum AttachFlag
    {
        /// <summary>
        /// Performs an invasive debugger attach.  Allows the consumer of this API to control the target
        /// process through normal IDebug function calls.  The process will be paused.
        /// </summary>
        Invasive,

        /// <summary>
        /// Performs a non-invasive debugger attach.  The process will be paused by this attached (and
        /// for the duration of the attach) but the caller cannot control the target process.  This is
        /// useful when there's already a debugger attached to the process.
        /// </summary>
        NonInvasive,

        /// <summary>
        /// Performs a "passive" attach, meaning no debugger is actually attached to the target process.
        /// The process is not paused, so queries for quickly changing data (such as the contents of the
        /// GC heap or callstacks) will be highly inconsistent unless the user pauses the process through
        /// other means.  Useful when attaching with ICorDebug (managed debugger), as you cannot use a
        /// non-invasive attach with ICorDebug.
        /// </summary>
        Passive
    }

    /// <summary>
    /// Provides information about loaded modules in a DataTarget
    /// </summary>
    [Serializable]
    public class ModuleInfo
    {
        /// <summary>
        /// The base address of the object.
        /// </summary>
        public virtual ulong ImageBase { get; set; }

        /// <summary>
        /// The filesize of the image.
        /// </summary>
        public virtual uint FileSize { get; set; }

        /// <summary>
        /// The filename of the module on disk.
        /// </summary>
        public virtual string FileName { get; set; }

        /// <summary>
        /// Returns true if this module is a native (non-managed) .Net runtime module.
        /// </summary>
        public bool IsRuntime { get; internal set; }

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>The filename of the module.</returns>
        public override string ToString()
        {
            return FileName;
        }

        /// <summary>
        /// The version information for this file.
        /// </summary>
        public VersionInfo Version
        {
            get
            {
                if (_versionInit || _dataReader == null)
                    return _version;

				int major, minor, revision, patch;
				if (NativeMethods.GetFileVersion(FileName, out major, out minor, out revision, out patch))
					_version = new VersionInfo(major, minor, revision, patch);
				else
					_version = new VersionInfo();

				_versionInit = true;
                return _version;
            }

            set
            {
                _version = value;
                _versionInit = true;
            }
        }

        /// <summary>
        /// Creates a ModuleInfo object with an IDataReader instance.  This is used when
        /// lazily evaluating VersionInfo. 
        /// </summary>
        /// <param name="reader"></param>
        public ModuleInfo(IDataReader reader)
        {
            _dataReader = reader;
        }
        [NonSerialized]

        private IDataReader _dataReader;
        private VersionInfo _version;
        private bool _versionInit;
    }

    /// <summary>
    /// Represents the dac dll
    /// </summary>
    [Serializable]
    public class DacInfo : ModuleInfo
    {
        /// <summary>
        /// Returns the filename of the dac dll according to the specified parameters
        /// </summary>
        public static string GetDacRequestFileName(ClrFlavor flavor, Runtime.Architecture currentArchitecture, Runtime.Architecture targetArchitecture, VersionInfo clrVersion)
        {
            if (flavor == ClrFlavor.Native)
                return targetArchitecture == Runtime.Architecture.Amd64 ? "mrt100dac_winamd64.dll" : "mrt100dac_winx86.dll";

            string dacName = flavor == ClrFlavor.CoreCLR ? "mscordaccore" : "mscordacwks";
            return string.Format("{0}_{1}_{2}_{3}.{4}.{5}.{6:D2}.dll", dacName, currentArchitecture, targetArchitecture, clrVersion.Major, clrVersion.Minor, clrVersion.Revision, clrVersion.Patch);
        }

        /// <summary>
        /// The platform-agnostice filename of the dac dll
        /// </summary>
        public string PlatformAgnosticFileName { get; set; }

        /// <summary>
        /// The architecture (x86 or amd64) being targeted
        /// </summary>
        public Architecture TargetArchitecture { get; set; }

        /// <summary>
        /// Constructs a DacInfo object with the appropriate properties initialized
        /// </summary>
        public DacInfo(IDataReader reader, string agnosticName, Architecture targetArch)
            : base(reader)
        {
            PlatformAgnosticFileName = agnosticName;
            TargetArchitecture = targetArch;
        }
    }

    /// <summary>
    /// An interface for reading data out of the target process.
    /// </summary>
    public interface IDataReader
    {
        /// <summary>
        /// Gets the architecture of the target.
        /// </summary>
        /// <returns>The architecture of the target.</returns>
        Architecture GetArchitecture();

        /// <summary>
        /// Gets the size of a pointer in the target process.
        /// </summary>
        /// <returns>The pointer size of the target process.</returns>
        uint GetPointerSize();

        /// <summary>
        /// Enumerates modules in the target process.
        /// </summary>
        /// <returns>A list of the modules in the target process.</returns>
        IList<ModuleInfo> EnumerateModules();

        /// <summary>
        /// Read memory out of the target process.
        /// </summary>
        /// <param name="address">The address of memory to read.</param>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="bytesRequested">The number of bytes to read.</param>
        /// <param name="bytesRead">The number of bytes actually read out of the target process.</param>
        /// <returns>True if any bytes were read at all, false if the read failed (and no bytes were read).</returns>
        bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead);

        /// <summary>
        /// Read memory out of the target process.
        /// </summary>
        /// <param name="address">The address of memory to read.</param>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="bytesRequested">The number of bytes to read.</param>
        /// <param name="bytesRead">The number of bytes actually read out of the target process.</param>
        /// <returns>True if any bytes were read at all, false if the read failed (and no bytes were read).</returns>
        bool ReadMemory(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead);

        /// <summary>
        /// Read a pointer out of the target process.
        /// </summary>
        /// <returns>The pointer at the give address, or 0 if that pointer doesn't exist in
        /// the data target.</returns>
        ulong ReadPointerUnsafe(ulong addr);
    }

    /// <summary>
    /// A crash dump or live process to read out of.
    /// </summary>
    public abstract class DataTarget
    {
        public static DataTarget CreateFromReader(IDataReader reader)
        {
#if _TRACING
            reader = new TraceDataReader(reader);
#endif
            return new DataTargetImpl(reader);
        }

        /// <summary>
        /// The data reader for this instance.
        /// </summary>
        public abstract IDataReader DataReader { get; }

        /// <summary>
        /// Returns the architecture of the target process or crash dump.
        /// </summary>
        public abstract Architecture Architecture { get; }

        /// <summary>
        /// Returns the list of Clr versions loaded into the process.
        /// </summary>
        public abstract IList<ClrInfo> ClrVersions { get; }

        /// <summary>
        /// Returns the pointer size for the target process.
        /// </summary>
        public abstract uint PointerSize { get; }

        /// <summary>
        /// Reads memory from the target.
        /// </summary>
        /// <param name="address">The address to read from.</param>
        /// <param name="buffer">The buffer to store the data in.  Size must be greator or equal to
        /// bytesRequested.</param>
        /// <param name="bytesRequested">The amount of bytes to read from the target process.</param>
        /// <param name="bytesRead">The actual number of bytes read.</param>
        /// <returns>True if any bytes were read out of the process (including a partial read).  False
        /// if no bytes could be read from the address.</returns>
        public abstract bool ReadProcessMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead);

        /// <summary>
        /// Enumerates information about the loaded modules in the process (both managed and unmanaged).
        /// </summary>
        public abstract IEnumerable<ModuleInfo> EnumerateModules();
    }

    internal class DataTargetImpl : DataTarget
    {
        private readonly IDataReader _dataReader;
        private ClrInfo[] _versions;
        private readonly Architecture _architecture;
        private ModuleInfo[] _modules;

        public DataTargetImpl(IDataReader dataReader)
        {
            if (dataReader == null)
                throw new ArgumentNullException(nameof(dataReader));

            _dataReader = dataReader;
            _architecture = _dataReader.GetArchitecture();
        }

        public override IDataReader DataReader
        {
            get
            {
                return _dataReader;
            }
        }

        public override Architecture Architecture
        {
            get { return _architecture; }
        }

        public override uint PointerSize
        {
            get { return _dataReader.GetPointerSize(); }
        }

        public override IList<ClrInfo> ClrVersions
        {
            get
            {
                if (_versions != null)
                    return _versions;

                List<ClrInfo> versions = new List<ClrInfo>();
                foreach (ModuleInfo module in EnumerateModules())
                {
                    string clrName = Path.GetFileNameWithoutExtension(module.FileName).ToLower();

                    if (clrName != "clr" && clrName != "mscorwks" && clrName != "coreclr" && clrName != "mrt100_app")
                        continue;

                    string dacLocation = Path.Combine(Path.GetDirectoryName(module.FileName), "mscordacwks.dll");
                    if (!File.Exists(dacLocation) || !NativeMethods.IsEqualFileVersion(dacLocation, module.Version))
                        dacLocation = null;

                    ClrFlavor flavor;
                    switch (clrName)
                    {
                        case "mrt100_app":
                            flavor = ClrFlavor.Native;
                            break;

                        case "coreclr":
                            flavor = ClrFlavor.CoreCLR;
                            break;

                        default:
                            flavor = ClrFlavor.Desktop;
                            break;
                    }

                    VersionInfo version = module.Version;
                    string dacAgnosticName = DacInfo.GetDacRequestFileName(flavor, Architecture, Architecture, version);
                    string dacFileName = DacInfo.GetDacRequestFileName(flavor, IntPtr.Size == 4 ? Architecture.X86 : Architecture.Amd64, Architecture, version);

                    DacInfo dacInfo = new DacInfo(_dataReader, dacAgnosticName, Architecture);
                    dacInfo.FileSize = module.FileSize;
                    dacInfo.FileName = dacFileName;
                    dacInfo.Version = module.Version;

                    versions.Add(new ClrInfo(this, flavor, module, dacInfo, dacLocation));
                }

                _versions = versions.ToArray();

                Array.Sort(_versions);
                return _versions;
            }
        }

        public override bool ReadProcessMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            return _dataReader.ReadMemory(address, buffer, bytesRequested, out bytesRead);
        }

        public override IEnumerable<ModuleInfo> EnumerateModules()
        {
            if (_modules == null)
                InitModules();

            return _modules;
        }

        private void InitModules()
        {
            if (_modules == null)
            {
                var sortedModules = new List<ModuleInfo>(_dataReader.EnumerateModules());
                sortedModules.Sort((a, b) => a.ImageBase.CompareTo(b.ImageBase));
                _modules = sortedModules.ToArray();
            }
        }
    }

    internal class DacLibrary
    {
        #region Variables
        private IntPtr _library;
	    private IXCLRDataProcess _dac;
        private ISOSDac _sos;
        private HashSet<object> _release = new HashSet<object>();
        #endregion

        public IXCLRDataProcess DacInterface { get { return _dac; } }

        public ISOSDac SOSInterface
        {
            get
            {
                if (_sos == null)
                    _sos = (ISOSDac)_dac;

                return _sos;
            }
        }

        public DacLibrary(DataTargetImpl dataTarget, string dacDll)
        {
	        if (dataTarget.ClrVersions.Count == 0)
                throw new ClrDiagnosticsException(string.Format("Process is not a CLR process!"));

            _library = NativeMethods.LoadLibrary(dacDll);
            if (_library == IntPtr.Zero)
                throw new ClrDiagnosticsException("Failed to load dac: " + dacDll);

            IntPtr addr = NativeMethods.GetProcAddress(_library, "CLRDataCreateInstance");
            var dacDataTarget = new DacDataTarget(dataTarget);

            object obj;
            NativeMethods.CreateDacInstance func = (NativeMethods.CreateDacInstance)Marshal.GetDelegateForFunctionPointer(addr, typeof(NativeMethods.CreateDacInstance));
            Guid guid = new Guid("5c552ab6-fc09-4cb3-8e36-22fa03c798b7");
            int res = func(ref guid, dacDataTarget, out obj);

            if (res == 0)
                _dac = obj as IXCLRDataProcess;

            if (_dac == null)
                throw new ClrDiagnosticsException("Failure loading DAC: CreateDacInstance failed 0x" + res.ToString("x"), ClrDiagnosticsException.HR.DacError);
        }

        ~DacLibrary()
        {
            foreach (object obj in _release)
                Marshal.FinalReleaseComObject(obj);

            if (_dac != null)
                Marshal.FinalReleaseComObject(_dac);

            if (_library != IntPtr.Zero)
                NativeMethods.FreeLibrary(_library);
        }

        internal void AddToReleaseList(object obj)
        {
            Debug.Assert(Marshal.IsComObject(obj));
            _release.Add(obj);
        }
    }

    internal class DacDataTarget : IDacDataTarget, IMetadataLocator
    {
	    private IDataReader _dataReader;
        private ModuleInfo[] _modules;

        public DacDataTarget(DataTargetImpl dataTarget)
        {
	        _dataReader = dataTarget.DataReader;
            _modules = dataTarget.EnumerateModules().ToArray();
            Array.Sort(_modules, delegate (ModuleInfo a, ModuleInfo b) { return a.ImageBase.CompareTo(b.ImageBase); });
        }

        public uint ReadVirtual(ulong address, IntPtr buffer, uint bytesRequested)
        {
			int read;
			if (ReadVirtual(address, buffer, (int)bytesRequested, out read) >= 0)
				return (uint)read;

			throw new Exception();
		}

        public void GetMachineType(out IMAGE_FILE_MACHINE machineType)
        {
            var arch = _dataReader.GetArchitecture();

            switch (arch)
            {
                case Architecture.Amd64:
                    machineType = IMAGE_FILE_MACHINE.AMD64;
                    break;

                case Architecture.X86:
                    machineType = IMAGE_FILE_MACHINE.I386;
                    break;

                case Architecture.Arm:
                    machineType = IMAGE_FILE_MACHINE.THUMB2;
                    break;

                default:
                    machineType = IMAGE_FILE_MACHINE.UNKNOWN;
                    break;
            }
        }

        public void GetPointerSize(out uint pointerSize)
        {
            pointerSize = _dataReader.GetPointerSize();
        }

        public void GetImageBase(string imagePath, out ulong baseAddress)
        {
            imagePath = Path.GetFileNameWithoutExtension(imagePath);

            foreach (ModuleInfo module in _modules)
            {
                string moduleName = Path.GetFileNameWithoutExtension(module.FileName);
                if (imagePath.Equals(moduleName, StringComparison.CurrentCultureIgnoreCase))
                {
                    baseAddress = module.ImageBase;
                    return;
                }
            }

            throw new Exception();
        }

        public int ReadMemory(ulong address, byte[] buffer, uint bytesRequested, out uint bytesRead)
        {
            int read = 0;
            if (_dataReader.ReadMemory(address, buffer, (int)bytesRequested, out read))
            {
                bytesRead = (uint)read;
                return 0;
            }

            bytesRead = 0;
            return -1;
        }

        public void WriteVirtual(ulong address, byte[] buffer, uint bytesRequested, out uint bytesWritten)
        {
            // This gets used by MemoryBarrier() calls in the dac, which really shouldn't matter what we do here.
            bytesWritten = bytesRequested;
        }

        public void GetTLSValue(uint threadID, uint index, out ulong value)
        {
            // TODO:  Validate this is not used?
            value = 0;
        }

        public void SetTLSValue(uint threadID, uint index, ulong value)
        {
            throw new NotImplementedException();
        }

        public void GetCurrentThreadID(out uint threadID)
        {
            threadID = 0;
        }

		public void GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
		{
			throw new NotImplementedException();
		}

		public void SetThreadContext(uint threadID, uint contextSize, IntPtr context)
        {
            throw new NotImplementedException();
        }

        public void Request(uint reqCode, uint inBufferSize, IntPtr inBuffer, IntPtr outBufferSize, out IntPtr outBuffer)
        {
            throw new NotImplementedException();
        }

        public int GetMetadata(string filename, uint imageTimestamp, uint imageSize, IntPtr mvid, uint mdRva, uint flags, uint bufferSize, byte[] buffer, IntPtr dataSize)
        {
			return -1;
        }

		public int ReadVirtual(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
		{
			int read = 0;
			if (_dataReader.ReadMemory(address, buffer, bytesRequested, out read))
			{
				bytesRead = read;
				return 0;
			}

			bytesRead = 0;
			return -1;
		}
	}
}