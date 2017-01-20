// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Address = System.UInt64;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime
{
	/// <summary>
	/// A ClrHeap is a abstraction for the whole GC Heap.   Subclasses allow you to implement this for 
	/// a particular kind of heap (whether live,
	/// </summary>
	public abstract class ClrHeap
    {
        /// <summary>
        /// And the ability to take an address of an object and fetch its type (The type alows further exploration)
        /// </summary>
        public abstract ClrType GetObjectType(Address objRef);

        /// <summary>
        /// Returns a  wrapper around a System.Exception object (or one of its subclasses).
        /// </summary>
        public virtual ClrException GetExceptionObject(Address objRef) { return null; }

        /// <summary>
        /// Returns the runtime associated with this heap.
        /// </summary>
        public abstract ClrRuntime Runtime { get; }

        /// <summary>
        /// A heap is has a list of contiguous memory regions called segments.  This list is returned in order of
        /// of increasing object addresses.  
        /// </summary>
        public abstract IList<ClrSegment> Segments { get; }

        /// <summary>
        /// Enumerate the roots of the process.  (That is, all objects which keep other objects alive.)
        /// Equivalent to EnumerateRoots(true).
        /// </summary>
        public abstract IEnumerable<ClrRoot> EnumerateRoots();

        /// <summary>
        /// Retrieves the given type by its MethodTable/ComponentMethodTable pair.
        /// </summary>
        /// <param name="methodTable">The ClrType.MethodTable for the requested type.</param>
        /// <param name="componentMethodTable">The ClrType's component MethodTable for the requested type.</param>
        /// <returns>A ClrType object, or null if no such type exists.</returns>
        public abstract ClrType GetTypeByMethodTable(ulong methodTable, ulong componentMethodTable);

        /// <summary>
        /// Retrieves the given type by its MethodTable/ComponentMethodTable pair.  Note this is only valid if
        /// the given type's component MethodTable is 0.
        /// </summary>
        /// <param name="methodTable">The ClrType.MethodTable for the requested type.</param>
        /// <returns>A ClrType object, or null if no such type exists.</returns>
        public virtual ClrType GetTypeByMethodTable(ulong methodTable)
        {
            return GetTypeByMethodTable(methodTable, 0);
        }

        /// <summary>
        /// Enumerate the roots in the process.
        /// </summary>
        /// <param name="enumerateStatics">True if we should enumerate static variables.  Enumerating with statics 
        /// can take much longer than enumerating without them.  Additionally these will be be "double reported",
        /// since all static variables are pinned by handles on the HandleTable (which is also enumerated with 
        /// EnumerateRoots).  You would want to enumerate statics with roots if you care about what exact statics
        /// root what objects, but not if you care about performance.</param>
        public abstract IEnumerable<ClrRoot> EnumerateRoots(bool enumerateStatics);

        /// <summary>
        /// Enumerates all types in the runtime.
        /// </summary>
        /// <returns>An enumeration of all types in the target process.  May return null if it's unsupported for
        /// that version of CLR.</returns>
        public virtual IEnumerable<ClrType> EnumerateTypes() { return null; }

        /// <summary>
        /// Enumerates all objects on the heap.  This is equivalent to enumerating all segments then walking
        /// each object with ClrSegment.FirstObject, ClrSegment.NextObject, but in a simple enumerator
        /// for easier use in linq queries.
        /// </summary>
        /// <returns>An enumerator for all objects on the heap.</returns>
        public abstract IEnumerable<Address> EnumerateObjectAddresses();

        /// <summary>
        /// TotalHeapSize is defined as the sum of the length of all segments.  
        /// </summary>
        public abstract ulong TotalHeapSize { get; }

        /// <summary>
        /// Returns the GC segment for the given object.
        /// </summary>
        public abstract ClrSegment GetSegmentByAddress(Address objRef);

        /// <summary>
        /// Returns true if the given address resides somewhere on the managed heap.
        /// </summary>
        public bool IsInHeap(Address address) { return GetSegmentByAddress(address) != null; }

        /// <summary>
        /// Pointer size of on the machine (4 or 8 bytes).  
        /// </summary>
        public abstract int PointerSize { get; }

        /// <summary>
        /// Returns a string representation of this heap, including the size and number of segments.
        /// </summary>
        /// <returns>The string representation of this heap.</returns>
        public override string ToString()
        {
            var sizeMB = TotalHeapSize / 1000000.0;
            int segCount = Segments != null ? Segments.Count : 0;
            return $"ClrHeap {sizeMB}mb {segCount} segments";
        }

        /// <summary>
        /// Attempts to efficiently read a pointer from memory.  This acts exactly like ClrRuntime.ReadPointer, but
        /// there is a greater chance you will hit a chache for a more efficient memory read.
        /// </summary>
        /// <param name="addr">The address to read.</param>
        /// <param name="value">The pointer value.</param>
        /// <returns>True if we successfully read the value, false if addr is not mapped into the process space.</returns>
        public abstract bool ReadPointer(Address addr, out Address value);
    }

    /// <summary>
    /// Represents a managed lock within the runtime.
    /// </summary>
    public abstract class BlockingObject
    {
        /// <summary>
        /// The object associated with the lock.
        /// </summary>
        public abstract Address Object { get; }

        /// <summary>
        /// The reason why it's blocking.
        /// </summary>
        public abstract BlockingReason Reason { get; internal set; }
    }

    /// <summary>
    /// The type of GCRoot that a ClrRoot represnts.
    /// </summary>
    public enum GCRootKind
    {
        /// <summary>
        /// The root is a static variable.
        /// </summary>
        StaticVar,

        /// <summary>
        /// The root is a thread static.
        /// </summary>
        ThreadStaticVar,

        /// <summary>
        /// The root is a local variable (or compiler generated temporary variable).
        /// </summary>
        LocalVar,

        /// <summary>
        /// The root is a strong handle.
        /// </summary>
        Strong,

        /// <summary>
        /// The root is a weak handle.
        /// </summary>
        Weak,

        /// <summary>
        /// The root is a strong pinning handle.
        /// </summary>
        Pinning,

        /// <summary>
        /// The root comes from the finalizer queue.
        /// </summary>
        Finalizer,

        /// <summary>
        /// The root is an async IO (strong) pinning handle.
        /// </summary>
        AsyncPinning,

        /// <summary>
        /// The max value of this enum.
        /// </summary>
        Max = AsyncPinning
    }

    /// <summary>
    /// Represents a root in the target process.  A root is the base entry to the GC's mark and sweep algorithm.
    /// </summary>
    public abstract class ClrRoot
    {
        /// <summary>
        /// A GC Root also has a Kind, which says if it is a strong or weak root
        /// </summary>
        public abstract GCRootKind Kind { get; }

        /// <summary>
        /// The name of the root. 
        /// </summary>
        public virtual string Name { get { return ""; } }

        /// <summary>
        /// The type of the object this root points to.  That is, ClrHeap.GetObjectType(ClrRoot.Object).
        /// </summary>
        public abstract ClrType Type { get; }

        /// <summary>
        /// The object on the GC heap that this root keeps alive.
        /// </summary>
        public virtual Address Object { get; protected set; }

        /// <summary>
        /// The address of the root in the target process.
        /// </summary>
        public virtual Address Address { get; protected set; }

        /// <summary>
        /// If the root can be identified as belonging to a particular AppDomain this is that AppDomain.
        /// It an be null if there is no AppDomain associated with the root.  
        /// </summary>
        public virtual ClrAppDomain AppDomain { get { return null; } }

        /// <summary>
        /// If the root has a thread associated with it, this will return that thread.
        /// </summary>
        public virtual ClrThread Thread { get { return null; } }

        /// <summary>
        /// Returns true if Object is an "interior" pointer.  This means that the pointer may actually
        /// point inside an object instead of to the start of the object.
        /// </summary>
        public virtual bool IsInterior { get { return false; } }

        /// <summary>
        /// Returns true if the root "pins" the object, preventing the GC from relocating it.
        /// </summary>
        public virtual bool IsPinned { get { return false; } }

        /// <summary>
        /// Unfortunately some versions of the APIs we consume do not give us perfect information.  If
        /// this property is true it means we used a heuristic to find the value, and it might not
        /// actually be considered a root by the GC.
        /// </summary>
        public virtual bool IsPossibleFalsePositive { get { return false; } }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            return string.Format("GCRoot {0:X8}->{1:X8} {2}", Address, Object, Name);
        }
    }

    /// <summary>
    /// A GCHeapSegment represents a contiguous region of memory that is devoted to the GC heap. 
    /// Segments.  It has a start and end and knows what heap it belongs to.   Segments can
    /// optional have regions for Gen 0, 1 and 2, and Large properties.  
    /// </summary>
    public abstract class ClrSegment
    {
        /// <summary>
        /// The start address of the segment.  All objects in this segment fall within Start &lt;= object &lt; End.
        /// </summary>
        public abstract Address Start { get; }

        /// <summary>
        /// The end address of the segment.  All objects in this segment fall within Start &lt;= object &lt; End.
        /// </summary>
        public abstract Address End { get; }

        /// <summary>
        /// The number of bytes in the segment.
        /// </summary>
        public ulong Length { get { return (End - Start); } }

        /// <summary>
        /// The GC heap associated with this segment.  There's only one GCHeap per process, so this is
        /// only a convenience method to keep from having to pass the heap along with a segment.
        /// </summary>
        public abstract ClrHeap Heap { get; }

        /// <summary>
        /// The processor that this heap is affinitized with.  In a workstation GC, there is no processor
        /// affinity (and the return value of this property is undefined).  In a server GC each segment
        /// has a logical processor in the PC associated with it.  This property returns that logical
        /// processor number (starting at 0).
        /// </summary>
        public abstract int ProcessorAffinity { get; }

        /// <summary>
        /// The address of the end of memory reserved for the segment, but not committed.
        /// </summary>
        public virtual Address ReservedEnd { get { return 0; } }

        /// <summary>
        /// The address of the end of memory committed for the segment (this may be longer than Length).
        /// </summary>
        public virtual Address CommittedEnd { get { return 0; } }

        /// <summary>
        /// If it is possible to move from one object to the 'next' object in the segment. 
        /// Then FirstObject returns the first object in the heap (or null if it is not
        /// possible to walk the heap.
        /// </summary>
        public virtual Address FirstObject { get { return 0; } }

        /// <summary>
        /// Given an object on the segment, return the 'next' object in the segment.  Returns
        /// 0 when there are no more objects.   (Or enumeration is not possible)  
        /// </summary>
        public virtual Address NextObject(Address objRef) { return 0; }

        /// <summary>
        /// Returns true if this is a segment for the Large Object Heap.  False otherwise.
        /// Large objects (greater than 85,000 bytes in size), are stored in their own segments and
        /// only collected on full (gen 2) collections. 
        /// </summary>
        public virtual bool IsLarge { get { return false; } }

        /// <summary>
        /// Returns true if this segment is the ephemeral segment (meaning it contains gen0 and gen1
        /// objects).
        /// </summary>
        public virtual bool IsEphemeral { get { return false; } }

        /// <summary>
        /// Ephemeral heap sements have geneation 0 and 1 in them.  Gen 1 is always above Gen 2 and
        /// Gen 0 is above Gen 1.  This property tell where Gen 0 start in memory.   Note that
        /// if this is not an Ephemeral segment, then this will return End (which makes Gen 0 empty
        /// for this segment)
        /// </summary>
        public virtual Address Gen0Start { get { return Start; } }

        /// <summary>
        /// The length of the gen0 portion of this segment.
        /// </summary>
        public virtual ulong Gen0Length { get { return Length; } }

        /// <summary>
        /// The start of the gen1 portion of this segment.
        /// </summary>
        public virtual Address Gen1Start { get { return End; } }

        /// <summary>
        /// The length of the gen1 portion of this segment.
        /// </summary>
        public virtual ulong Gen1Length { get { return 0; } }

        /// <summary>
        /// The start of the gen2 portion of this segment.
        /// </summary>
        public virtual Address Gen2Start { get { return End; } }

        /// <summary>
        /// The length of the gen2 portion of this segment.
        /// </summary>
        public virtual ulong Gen2Length { get { return 0; } }

        /// <summary>
        /// Enumerates all objects on the segment.
        /// </summary>
        public abstract IEnumerable<ulong> EnumerateObjectAddresses();

        /// <summary>
        /// Returns the generation of an object in this segment.
        /// </summary>
        /// <param name="obj">An object in this segment.</param>
        /// <returns>The generation of the given object if that object lies in this segment.  The return
        ///          value is undefined if the object does not lie in this segment.
        /// </returns>
        public virtual int GetGeneration(Address obj)
        {
            if (Gen0Start <= obj && obj < (Gen0Start + Gen0Length))
            {
                return 0;
            }

            if (Gen1Start <= obj && obj < (Gen1Start + Gen1Length))
            {
                return 1;
            }

            if (Gen2Start <= obj && obj < (Gen2Start + Gen2Length))
            {
                return 2;
            }

            return -1;
        }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            return string.Format("HeapSegment {0:n2}mb [{1:X8}, {2:X8}]", Length / 1000000.0, Start, End);
        }
    }

    /// <summary>
    /// Every thread which is blocking on an object specifies why the object is waiting.
    /// </summary>
    public enum BlockingReason
    {
        /// <summary>
        /// Object is not locked.
        /// </summary>
        None,

        /// <summary>
        /// Not able to determine why the object is blocking.
        /// </summary>
        Unknown,

        /// <summary>
        /// The thread is waiting for a Mutex or Semaphore (such as Monitor.Enter, lock(obj), etc).
        /// </summary>
        Monitor,

        /// <summary>
        /// The thread is waiting for a mutex with Monitor.Wait.
        /// </summary>
        MonitorWait,

        /// <summary>
        /// The thread is waiting for an event (ManualResetEvent.WaitOne, AutoResetEvent.WaitOne).
        /// </summary>
        WaitOne,

        /// <summary>
        /// The thread is waiting in WaitHandle.WaitAll.
        /// </summary>
        WaitAll,

        /// <summary>
        /// The thread is waiting in WaitHandle.WaitAny.
        /// </summary>
        WaitAny,

        /// <summary>
        /// The thread is blocked on a call to Thread.Join.
        /// </summary>
        ThreadJoin,

        /// <summary>
        /// ReaderWriterLock, reader lock is taken.
        /// </summary>
        ReaderAcquired,


        /// <summary>
        /// ReaderWriterLock, writer lock is taken.
        /// </summary>
        WriterAcquired
    }
    /// <summary>
    /// Types of GC segments.
    /// </summary>
    public enum GCSegmentType
    {
        /// <summary>
        /// Ephemeral segments are the only segments to contain Gen0 and Gen1 objects.
        /// It may also contain Gen2 objects, but not always.  Objects are only allocated
        /// on the ephemeral segment.  There is one ephemeral segment per logical GC heap.
        /// It is important to not have too many pinned objects in the ephemeral segment,
        /// or you will run into a performance problem where the runtime runs too many GCs.
        /// </summary>
        Ephemeral,

        /// <summary>
        /// Regular GC segments only contain Gen2 objects.
        /// </summary>
        Regular,

        /// <summary>
        /// The large object heap contains objects greater than a certain threshold.  Large
        /// object segments are never compacted.  Large objects are directly allocated
        /// onto LargeObject segments, and all large objects are considered gen2.
        /// </summary>
        LargeObject
    }

    /// <summary>
    /// Defines the state of the thread from the runtime's perspective.
    /// </summary>
    public enum GcMode
    {
        /// <summary>
        /// In Cooperative mode the thread must cooperate before a GC may proceed.  This means when a GC
        /// starts, the runtime will attempt to suspend the thread at a safepoint but cannot immediately
        /// stop the thread until it synchronizes.
        /// </summary>
        Cooperative,
        /// <summary>
        /// In Preemptive mode the runtime is free to suspend the thread at any time for a GC to occur.
        /// </summary>
        Preemptive
    }

    internal abstract class HeapBase : ClrHeap
    {
        private Address _minAddr;          // Smallest and largest segment in the GC heap.  Used to make SegmentForObject faster.  
        private Address _maxAddr;
        private ClrSegment[] _segments;
        private ulong[] _sizeByGen = new Address[4];
        private ulong _totalHeapSize;
        private int _lastSegmentIdx;       // The last segment we looked at.
        private bool _canWalkHeap;
        private int _pointerSize;

        public HeapBase(RuntimeBase runtime)
        {
            _canWalkHeap = runtime.CanWalkHeap;
            MemoryReader = new MemoryReader(runtime.DataReader, 0x10000);
            _pointerSize = runtime.PointerSize;
        }

        public override bool ReadPointer(Address addr, out Address value)
        {
            if (MemoryReader.Contains(addr))
                return MemoryReader.ReadPtr(addr, out value);

            return Runtime.ReadPointer(addr, out value);
        }

        internal int Revision { get; set; }

        protected abstract int GetRuntimeRevision();

        public override int PointerSize
        {
            get
            {
                return _pointerSize;
            }
        }

        public override IList<ClrSegment> Segments
        {
            get
            {
                if (Revision != GetRuntimeRevision())
                    ClrDiagnosticsException.ThrowRevisionError(Revision, GetRuntimeRevision());
                return _segments;
            }
        }
        public override Address TotalHeapSize
        {
            get { return _totalHeapSize; }
        }

        internal MemoryReader MemoryReader { get; private set; }

        protected void UpdateSegmentData(HeapSegment segment)
        {
            _totalHeapSize += segment.Length;
            _sizeByGen[0] += segment.Gen0Length;
            _sizeByGen[1] += segment.Gen1Length;
            if (!segment.IsLarge)
                _sizeByGen[2] += segment.Gen2Length;
            else
                _sizeByGen[3] += segment.Gen2Length;
        }

        protected void InitSegments(RuntimeBase runtime)
        {
            // Populate segments
            SubHeap[] heaps;
            if (runtime.GetHeaps(out heaps))
            {
                var segments = new List<HeapSegment>();
                foreach (var heap in heaps)
                {
                    if (heap != null)
                    {
                        ISegmentData seg = runtime.GetSegmentData(heap.FirstLargeSegment);
                        while (seg != null)
                        {
                            var segment = new HeapSegment(runtime, seg, heap, true, this);
                            segments.Add(segment);

                            UpdateSegmentData(segment);
                            seg = runtime.GetSegmentData(seg.Next);
                        }

                        seg = runtime.GetSegmentData(heap.FirstSegment);
                        while (seg != null)
                        {
                            var segment = new HeapSegment(runtime, seg, heap, false, this);
                            segments.Add(segment);

                            UpdateSegmentData(segment);
                            seg = runtime.GetSegmentData(seg.Next);
                        }
                    }
                }

                UpdateSegments(segments.ToArray());
            }
            else
            {
                _segments = new ClrSegment[0];
            }
        }

        private void UpdateSegments(ClrSegment[] segments)
        {
            // sort the segments.  
            Array.Sort(segments, delegate (ClrSegment x, ClrSegment y) { return x.Start.CompareTo(y.Start); });
            _segments = segments;

            _minAddr = Address.MaxValue;
            _maxAddr = Address.MinValue;
            _totalHeapSize = 0;
            _sizeByGen = new ulong[4];
            foreach (var gcSegment in _segments)
            {
                if (gcSegment.Start < _minAddr)
                    _minAddr = gcSegment.Start;
                if (_maxAddr < gcSegment.End)
                    _maxAddr = gcSegment.End;

                _totalHeapSize += gcSegment.Length;
                if (gcSegment.IsLarge)
                    _sizeByGen[3] += gcSegment.Length;
                else
                {
                    _sizeByGen[2] += gcSegment.Gen2Length;
                    _sizeByGen[1] += gcSegment.Gen1Length;
                    _sizeByGen[0] += gcSegment.Gen0Length;
                }
            }
        }


        public override IEnumerable<Address> EnumerateObjectAddresses()
        {
            if (Revision != GetRuntimeRevision())
                ClrDiagnosticsException.ThrowRevisionError(Revision, GetRuntimeRevision());

            for (int i = 0; i < _segments.Length; ++i)
            {
                var seg = _segments[i];
                for (ulong obj = seg.FirstObject; obj != 0; obj = seg.NextObject(obj))
                {
                    _lastSegmentIdx = i;
                    yield return obj;
                }
            }
        }

        public override ClrSegment GetSegmentByAddress(Address objRef)
        {
            if (_minAddr <= objRef && objRef < _maxAddr)
            {
                // Start the segment search where you where last
                int curIdx = _lastSegmentIdx;
                for (;;)
                {
                    var segment = _segments[curIdx];
                    var offsetInSegment = (long)(objRef - segment.Start);
                    if (0 <= offsetInSegment)
                    {
                        var intOffsetInSegment = (long)offsetInSegment;
                        if (intOffsetInSegment < (long)segment.Length)
                        {
                            _lastSegmentIdx = curIdx;
                            return segment;
                        }
                    }

                    // Get the next segment loop until you come back to where you started.  
                    curIdx++;
                    if (curIdx >= Segments.Count)
                        curIdx = 0;
                    if (curIdx == _lastSegmentIdx)
                        break;
                }
            }
            return null;
        }
    }


    internal class HeapSegment : ClrSegment
    {
        public override int ProcessorAffinity
        {
            get { return _subHeap.HeapNum; }
        }
        public override Address Start { get { return _segment.Start; } }
        public override Address End { get { return _subHeap.EphemeralSegment == _segment.Address ? _subHeap.EphemeralEnd : _segment.End; } }
        public override ClrHeap Heap { get { return _heap; } }

        public override bool IsLarge { get { return _large; } }

        public override Address ReservedEnd { get { return _segment.Reserved; } }
        public override Address CommittedEnd { get { return _segment.Committed; } }

        public override Address Gen0Start
        {
            get
            {
                if (IsEphemeral)
                    return _subHeap.Gen0Start;
                else
                    return End;
            }
        }
        public override Address Gen0Length { get { return End - Gen0Start; } }
        public override Address Gen1Start
        {
            get
            {
                if (IsEphemeral)
                    return _subHeap.Gen1Start;
                else
                    return End;
            }
        }
        public override Address Gen1Length { get { return Gen0Start - Gen1Start; } }
        public override Address Gen2Start { get { return Start; } }
        public override Address Gen2Length { get { return Gen1Start - Start; } }


        public override IEnumerable<Address> EnumerateObjectAddresses()
        {
            for (ulong obj = FirstObject; obj != 0; obj = NextObject(obj))
                yield return obj;
        }

        public override Address FirstObject
        {
            get
            {
                if (Gen2Start == End)
                    return 0;
                _heap.MemoryReader.EnsureRangeInCache(Gen2Start);
                return Gen2Start;
            }
        }

        public override Address NextObject(Address addr)
        {
            if (addr >= CommittedEnd)
                return 0;

            uint minObjSize = (uint)_clr.PointerSize * 3;

            ClrType type = _heap.GetObjectType(addr);
            if (type == null)
                return 0;

            ulong size = type.GetSize(addr);
            size = Align(size, _large);
            if (size < minObjSize)
                size = minObjSize;

            // Move to the next object
            addr += size;

            // Check to make sure a GC didn't cause "count" to be invalid, leading to too large
            // of an object
            if (addr >= End)
                return 0;

            // Ensure we aren't at the start of an alloc context
            ulong tmp;
            while (!IsLarge && _subHeap.AllocPointers.TryGetValue(addr, out tmp))
            {
                tmp += Align(minObjSize, _large);

                // Only if there's data corruption:
                if (addr >= tmp)
                    return 0;

                // Otherwise:
                addr = tmp;

                if (addr >= End)
                    return 0;
            }

            return addr;
        }

        #region private
        internal static Address Align(ulong size, bool large)
        {
            Address AlignConst;
            Address AlignLargeConst = 7;

            if (IntPtr.Size == 4)
                AlignConst = 3;
            else
                AlignConst = 7;

            if (large)
                return (size + AlignLargeConst) & ~(AlignLargeConst);

            return (size + AlignConst) & ~(AlignConst);
        }

        public override bool IsEphemeral { get { return _segment.Address == _subHeap.EphemeralSegment; ; } }
        internal HeapSegment(RuntimeBase clr, ISegmentData segment, SubHeap subHeap, bool large, HeapBase heap)
        {
            _clr = clr;
            _large = large;
            _segment = segment;
            _heap = heap;
            _subHeap = subHeap;
        }

        private bool _large;
        private RuntimeBase _clr;
        private ISegmentData _segment;
        private SubHeap _subHeap;
        private HeapBase _heap;
        #endregion
    }

}