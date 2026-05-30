using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Hi3Helper.EncTool.Hashes
{
    /// <summary>
    /// MhyMurmurHash264B is a MurmurHash2 64-bit variant, optimized for 32-bit sized data (up to 4 GiB (4,294,967,295 bytes)).
    /// This hash instance has been rewritten from miHoYo's original code to make it compatible with <see cref="NonCryptographicHashAlgorithm"/>.
    /// <br/><br/>
    /// This implementation is based on miHoYo's <c>MurmurHash64B</c> implementation found in Honkai Impact 3rd game (Starting from v8.2 update).
    /// </summary>
    public sealed class MhyMurmurHash264B() : NonCryptographicHashAlgorithm(8)
    {
        private const uint Bm = 0x5bd1e995;
        private const int  Br = 24;

        private readonly ulong _seed;
        private readonly ulong _readLengthTarget;
        private readonly bool  _skipAppendLengthAssert;

        private uint _highBytes;
        private uint _lowBytes;

        private readonly byte[] _tail = new byte[8];
        private          int    _tailLength;
        private          ulong  _appendedLength;

        /// <summary>
        /// Create a new instance with targeted data size, optionally with seed.
        /// </summary>
        /// <param name="readLengthTarget">The length in which the target <see cref="Stream"/> define.</param>
        /// <param name="seed">Optional seed for hashing.</param>
        /// <param name="skipAppendLengthAssert">Whether to skip the append length assertion.</param>
        public MhyMurmurHash264B(ulong readLengthTarget, ulong seed = 0, bool skipAppendLengthAssert = false) : this()
        {
            _readLengthTarget       = readLengthTarget;
            _seed                   = seed;
            _skipAppendLengthAssert = skipAppendLengthAssert;
            Reset();
        }

        /// <summary>
        /// Create a new instance for a <see cref="Stream"/> instance.
        /// </summary>
        /// <param name="stream">A <see cref="Stream"/> instance to use. The <see cref="Stream"/> must have length defined. If not, <paramref name="streamLengthExplicit"/> must be defined.</param>
        /// <param name="seed">Optional seed for hashing.</param>
        /// <param name="streamLengthExplicit">Explicitly use a specific length of the <see cref="Stream"/></param>
        /// <returns>A <see cref="MhyMurmurHash264B"/> instance, with <see cref="NonCryptographicHashAlgorithm"/> derived.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If the <paramref name="streamLengthExplicit"/> argument is being set with a negative number.</exception>
        /// <exception cref="NotSupportedException">If the length of the <see cref="Stream"/> cannot be gathered while <paramref name="streamLengthExplicit"/> is not defined.</exception>
        /// <exception cref="InvalidOperationException">If the length of the <see cref="Stream"/> is more than 4 GiB (4,294,967,295 bytes)</exception>
        public static MhyMurmurHash264B CreateForStream(Stream stream, ulong seed = 0, long? streamLengthExplicit = null)
        {
            ArgumentNullException.ThrowIfNull(stream);

            if (streamLengthExplicit is < 0)
                throw new ArgumentOutOfRangeException(nameof(streamLengthExplicit),
                                                      "Stream length must be non-negative.");

            long streamLength = 0;

            if (streamLengthExplicit == null && !TryGetStreamLength(stream, out streamLength))
            {
                throw new NotSupportedException($"Cannot get the length of the stream. Please explicitly define the stream length using {nameof(streamLengthExplicit)} argument.");
            }

            long readLengthTarget = streamLengthExplicit ?? streamLength;
            return new MhyMurmurHash264B((ulong)readLengthTarget, seed);
        }

        private static bool TryGetStreamLength(Stream stream, out long length)
        {
            try
            {
                length = stream.Length;
                return true;
            }
            catch (NotSupportedException)
            {
                length = 0;
                return false;
            }
        }

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">
        ///    If the length of the <paramref name="source"/> is not 8 bytes aligned (cannot be divided by 8) when
        ///    the remained data to compute is more than 8 bytes left.
        /// </exception>
        public override unsafe void Append(ReadOnlySpan<byte> source)
        {
            if (source.IsEmpty)
                return;

            _appendedLength += (ulong)source.Length;

            if (_tailLength != 0)
            {
                int needed = 8 - _tailLength;

                if (source.Length < needed)
                {
                    source.CopyTo(_tail.AsSpan(_tailLength));
                    _tailLength += source.Length;
                    return;
                }

                source[..needed].CopyTo(_tail.AsSpan(_tailLength));

                fixed (byte* tailPtr = _tail)
                {
                    MixHigh(ref _highBytes, ReadUInt32LittleEndian(tailPtr));
                    MixLow(ref _lowBytes, ReadUInt32LittleEndian(tailPtr + 4));
                }

                _tailLength = 0;
                source      = source[needed..];
            }

            fixed (byte* bytePtr = &MemoryMarshal.GetReference(source))
            {
                byte* start = bytePtr;
                byte* end   = bytePtr + source.Length;

                start = ProcessBlocks(ref _highBytes, ref _lowBytes, start, end);

                int remaining = (int)(end - start);

                if (remaining <= 0)
                    return;

                new ReadOnlySpan<byte>(start, remaining).CopyTo(_tail);
                _tailLength = remaining;
            }
        }

        /// <inheritdoc/>
        protected override void GetCurrentHashCore(Span<byte> destination)
        {
            uint highBytes = _highBytes;
            uint lowBytes  = _lowBytes;

            FinalizeTail(ref highBytes, ref lowBytes);
            FinalizeHash(ref highBytes, ref lowBytes);

            ulong hash = ((ulong)highBytes << 32) | lowBytes;
            MemoryMarshal.Write(destination, in hash);
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            _highBytes = (uint)_seed ^ (uint)_readLengthTarget;

            // This is the 64-bit length extension.
            // For <= uint.MaxValue, this stays identical to old behavior.
            _lowBytes = (uint)(_seed >> 32) ^ (uint)(_readLengthTarget >> 32);

            _tailLength     = 0;
            _appendedLength = 0;
        }

        /// <inheritdoc/>
        protected override void GetHashAndResetCore(Span<byte> destination)
        {
            if (_appendedLength != _readLengthTarget &&
                !_skipAppendLengthAssert)
            {
                throw new InvalidOperationException($"Appended length does not match target length. Expected {_readLengthTarget} bytes, got {_appendedLength} bytes.");
            }

            GetCurrentHashCore(destination);
            Reset();
        }

        private void FinalizeTail(ref uint highBytes, ref uint lowBytes)
        {
            ReadOnlySpan<byte> tail = _tail.AsSpan(0, _tailLength);

            if (tail.Length >= 4)
            {
                MixHigh(ref highBytes, BinaryPrimitives.ReadUInt32LittleEndian(tail));
                tail = tail[4..];
            }

            if (tail.Length == 0)
                return;

            switch (tail.Length)
            {
                case 3:
                    lowBytes ^= (uint)tail[2] << 16;
                    lowBytes ^= (uint)tail[1] << 8;
                    lowBytes ^= tail[0];
                    break;

                case 2:
                    lowBytes ^= (uint)tail[1] << 8;
                    lowBytes ^= tail[0];
                    break;

                case 1:
                    lowBytes ^= tail[0];
                    break;
            }

            lowBytes *= Bm;
        }

        private static unsafe byte* ProcessBlocks(
            ref uint highBytes,
            ref uint lowBytes,
            byte*    start,
            byte*    end)
        {
            uint high = highBytes;
            uint low  = lowBytes;

            if (Avx2.IsSupported && BitConverter.IsLittleEndian)
            {
                Vector256<uint> bmVector = Vector256.Create(Bm);
                uint* mixed = stackalloc uint[8];

                while (start + 32 <= end)
                {
                    Vector256<uint> k = Unsafe.ReadUnaligned<Vector256<uint>>(start);
                    k = MixKAvx2(k, bmVector);

                    Avx.Store(mixed, k);

                    high = (high * Bm) ^ mixed[0];
                    low  = (low * Bm) ^ mixed[1];

                    high = (high * Bm) ^ mixed[2];
                    low  = (low * Bm) ^ mixed[3];

                    high = (high * Bm) ^ mixed[4];
                    low  = (low * Bm) ^ mixed[5];

                    high = (high * Bm) ^ mixed[6];
                    low  = (low * Bm) ^ mixed[7];

                    start += 32;
                }
            }
            else
            {
                while (start + 32 <= end)
                {
                    uint k0 = MixK(ReadUInt32LittleEndian(start + 0));
                    uint k1 = MixK(ReadUInt32LittleEndian(start + 4));
                    uint k2 = MixK(ReadUInt32LittleEndian(start + 8));
                    uint k3 = MixK(ReadUInt32LittleEndian(start + 12));
                    uint k4 = MixK(ReadUInt32LittleEndian(start + 16));
                    uint k5 = MixK(ReadUInt32LittleEndian(start + 20));
                    uint k6 = MixK(ReadUInt32LittleEndian(start + 24));
                    uint k7 = MixK(ReadUInt32LittleEndian(start + 28));

                    high = (high * Bm) ^ k0;
                    low  = (low * Bm) ^ k1;

                    high = (high * Bm) ^ k2;
                    low  = (low * Bm) ^ k3;

                    high = (high * Bm) ^ k4;
                    low  = (low * Bm) ^ k5;

                    high = (high * Bm) ^ k6;
                    low  = (low * Bm) ^ k7;

                    start += 32;
                }
            }

            while (start + 8 <= end)
            {
                high = (high * Bm) ^ MixK(ReadUInt32LittleEndian(start));
                low  = (low * Bm) ^ MixK(ReadUInt32LittleEndian(start + 4));

                start += 8;
            }

            highBytes = high;
            lowBytes  = low;

            return start;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<uint> MixKAvx2(Vector256<uint> k, Vector256<uint> bm)
        {
            k = Avx2.MultiplyLow(k.AsInt32(), bm.AsInt32()).AsUInt32();
            k = Avx2.Xor(k, Avx2.ShiftRightLogical(k, Br));
            k = Avx2.MultiplyLow(k.AsInt32(), bm.AsInt32()).AsUInt32();

            return k;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixK(uint k)
        {
            k *= Bm;
            k ^= k >> Br;
            k *= Bm;

            return k;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MixHigh(ref uint highBytes, uint k)
        {
            highBytes *= Bm;
            highBytes ^= MixK(k);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MixLow(ref uint lowBytes, uint k)
        {
            lowBytes *= Bm;
            lowBytes ^= MixK(k);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void FinalizeHash(ref uint highBytes, ref uint lowBytes)
        {
            highBytes =  (highBytes ^ (lowBytes >> 18)) * Bm;
            lowBytes  ^= highBytes >> 22;

            lowBytes  *= Bm;
            highBytes ^= lowBytes >> 17;

            highBytes *= Bm;
            lowBytes  ^= highBytes >> 19;

            lowBytes *= Bm;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint ReadUInt32LittleEndian(byte* ptr)
        {
            uint value = Unsafe.ReadUnaligned<uint>(ptr);

            return BitConverter.IsLittleEndian
                ? value
                : BinaryPrimitives.ReverseEndianness(value);
        }
    }
}
