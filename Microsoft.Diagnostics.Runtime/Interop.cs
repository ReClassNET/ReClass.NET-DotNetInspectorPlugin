using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
	// Enums

	public enum IMAGE_FILE_MACHINE : uint
	{
		UNKNOWN = 0,
		I386 = 0x014c, // Intel 386.
		R3000 = 0x0162, // MIPS little-endian, 0x160 big-endian
		R4000 = 0x0166, // MIPS little-endian
		R10000 = 0x0168, // MIPS little-endian
		WCEMIPSV2 = 0x0169, // MIPS little-endian WCE v2
		ALPHA = 0x0184, // Alpha_AXP
		SH3 = 0x01a2, // SH3 little-endian
		SH3DSP = 0x01a3,
		SH3E = 0x01a4, // SH3E little-endian
		SH4 = 0x01a6, // SH4 little-endian
		SH5 = 0x01a8, // SH5
		ARM = 0x01c0, // ARM Little-Endian
		THUMB = 0x01c2,
		THUMB2 = 0x1c4,
		AM33 = 0x01d3,
		POWERPC = 0x01F0, // IBM PowerPC Little-Endian
		POWERPCFP = 0x01f1,
		IA64 = 0x0200, // Intel 64
		MIPS16 = 0x0266, // MIPS
		ALPHA64 = 0x0284, // ALPHA64
		MIPSFPU = 0x0366, // MIPS
		MIPSFPU16 = 0x0466, // MIPS
		AXP64 = 0x0284,
		TRICORE = 0x0520, // Infineon
		CEF = 0x0CEF,
		EBC = 0x0EBC, // EFI Byte Code
		AMD64 = 0x8664, // AMD64 (K8)
		M32R = 0x9041, // M32R little-endian
		CEE = 0xC0EE,
	}

	// Structs

	[StructLayout(LayoutKind.Sequential)]
	public struct IMAGE_DATA_DIRECTORY
	{
		public UInt32 VirtualAddress;
		public UInt32 Size;
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct IMAGE_COR20_HEADER_ENTRYPOINT
	{
		[FieldOffset(0)]
		private UInt32 _token;
		[FieldOffset(0)]
		private UInt32 _RVA;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct IMAGE_COR20_HEADER
	{
		// Header versioning
		public UInt32 cb;
		public UInt16 MajorRuntimeVersion;
		public UInt16 MinorRuntimeVersion;

		// Symbol table and startup information
		public IMAGE_DATA_DIRECTORY MetaData;
		public UInt32 Flags;

		// The main program if it is an EXE (not used if a DLL?)
		// If COMIMAGE_FLAGS_NATIVE_ENTRYPOINT is not set, EntryPointToken represents a managed entrypoint.
		// If COMIMAGE_FLAGS_NATIVE_ENTRYPOINT is set, EntryPointRVA represents an RVA to a native entrypoint
		// (depricated for DLLs, use modules constructors intead).
		public IMAGE_COR20_HEADER_ENTRYPOINT EntryPoint;

		// This is the blob of managed resources. Fetched using code:AssemblyNative.GetResource and
		// code:PEFile.GetResource and accessible from managed code from
		// System.Assembly.GetManifestResourceStream.  The meta data has a table that maps names to offsets into
		// this blob, so logically the blob is a set of resources.
		public IMAGE_DATA_DIRECTORY Resources;
		// IL assemblies can be signed with a public-private key to validate who created it.  The signature goes
		// here if this feature is used.
		public IMAGE_DATA_DIRECTORY StrongNameSignature;

		public IMAGE_DATA_DIRECTORY CodeManagerTable; // Depricated, not used
													  // Used for manged codee that has unmaanaged code inside it (or exports methods as unmanaged entry points)
		public IMAGE_DATA_DIRECTORY VTableFixups;
		public IMAGE_DATA_DIRECTORY ExportAddressTableJumps;

		// null for ordinary IL images.  NGEN images it points at a code:CORCOMPILE_HEADER structure
		public IMAGE_DATA_DIRECTORY ManagedNativeHeader;
	}
}
