using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Diagnostics.Runtime;
using ReClassNET.Extensions;
using ReClassNET.Memory;

namespace DotNetInspectorPlugin
{
	internal class ReClassNetDataReader : IDataReader
	{
		private readonly RemoteProcess process;

		public ReClassNetDataReader(RemoteProcess process)
		{
			Contract.Requires(process != null);

			this.process = process;
		}

		public IList<ModuleInfo> EnumerateModules()
		{
			process.EnumerateRemoteSectionsAndModules(out _, out var modules);

			return modules
				.Select(m => new ModuleInfo(this)
				{
					FileName = m.Path,
					ImageBase = (ulong)m.Start,
					FileSize = (uint)m.End.Sub(m.Start)
				})
				.ToList();
		}

		public Architecture GetArchitecture()
		{
			return IntPtr.Size == 4 ? Architecture.X86 : Architecture.Amd64;
		}

		public uint GetPointerSize()
		{
			return (uint)IntPtr.Size;
		}

		public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
		{
			Contract.Requires(buffer != null);

			bytesRead = bytesRequested;

			return process.ReadRemoteMemoryIntoBuffer((IntPtr)address, ref buffer, 0, bytesRequested);
		}

		public bool ReadMemory(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
		{
			bytesRead = bytesRequested;

			var temp = new byte[bytesRequested];
			if (!process.ReadRemoteMemoryIntoBuffer((IntPtr)address, ref temp))
			{
				return false;
			}

			System.Runtime.InteropServices.Marshal.Copy(temp, 0, buffer, bytesRequested);

			return true;
		}

		public ulong ReadPointerUnsafe(ulong address)
		{
			return process.ReadRemoteUInt64((IntPtr)address);
		}
	}
}
