using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime;
using ReClassNET.Memory;
using ReClassNET.Util;

namespace DotNetInspectorPlugin
{
	class ReClassNetDataReader : IDataReader
	{
		private readonly RemoteProcess process;

		public ReClassNetDataReader(RemoteProcess process)
		{
			Contract.Requires(process != null);

			this.process = process;
		}

		public IList<ModuleInfo> EnumerateModules()
		{
			var result = new List<ModuleInfo>();

			process.EnumerateRemoteSectionsAndModules(
				null,
				m =>
				{
					var module = new ModuleInfo(this)
					{
						FileName = m.Path,
						ImageBase = (ulong) m.Start,
						FileSize = (uint) m.End.Sub(m.Start)
					};
					result.Add(module);
				}
			);

			return result;
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

			return process.ReadRemoteMemoryIntoBuffer(new IntPtr((long)address), ref buffer, 0, bytesRequested);
		}

		public bool ReadMemory(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
		{
			bytesRead = bytesRequested;

			var temp = new byte[bytesRequested];
			if (!process.ReadRemoteMemoryIntoBuffer(new IntPtr((long)address), ref temp))
			{
				return false;
			}

			Marshal.Copy(temp, 0, buffer, bytesRequested);

			return true;
		}

		public ulong ReadPointerUnsafe(ulong address)
		{
			return process.ReadRemoteObject<ulong>(new IntPtr((long)address));
		}
	}
}
