using System;
using System.IO;
using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace Hi3Helper.EncTool.Hashes
{
    /// <summary>
    /// MhyMurmurHash264B is a MurmurHash2 64-bit variant, optimized for 32-bit sized data (up to 4 GiB (4,294,967,295 bytes)).
    /// This hash instance was ported to make it compatible with <see cref="NonCryptographicHashAlgorithm"/>.
    /// <br/><br/>
    /// This implementation was based on miHoYo own <c>MurmurHash64B</c> function found on Honkai Impact 3rd v8.2 game.
    /// </summary>
    public sealed class MhyMurmurHash264B() : NonCryptographicHashAlgorithm(8)
    {
        private const uint Bm = 0x5bd1e995;
        private const int  Br = 24;

        private readonly ulong _seed;
        private readonly uint  _readLengthTarget;
        private          uint  _highBytes;
        private          uint  _lowBytes;
        private          uint  _remained;

        /// <summary>
        /// Create a new instance with targeted data size, optionally with seed.
        /// </summary>
        /// <param name="readLengthTarget">The length in which the target <see cref="Stream"/> define.</param>
        /// <param name="seed">Optional seed for hashing.</param>
        public MhyMurmurHash264B(uint readLengthTarget, ulong seed = 0) : this()
        {
            _readLengthTarget = readLengthTarget;
            _seed             = seed;
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
                throw new ArgumentOutOfRangeException(nameof(streamLengthExplicit), "Stream length must be non-negative.");

            long streamLength = 0;
            if (streamLengthExplicit == null && !TryGetStreamLength(stream, out streamLength))
                throw new NotSupportedException("Cannot get the length of the stream. Please explicitly define the stream length using streamLengthExplicit argument.");

            long readLengthTarget = streamLengthExplicit ?? streamLength;
            if (readLengthTarget > uint.MaxValue)
                throw new InvalidOperationException("Stream length cannot be more than 4 GiB (4,294,967,295 bytes)");

            return new MhyMurmurHash264B((uint)readLengthTarget, seed);
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
            int len = source.Length;
            AssertEnsureBufferLengthEven(len);

            fixed (byte* bytePtr = &MemoryMarshal.GetReference(source))
            {
                byte* start = bytePtr;
                byte* end   = bytePtr + len;

                start = ComputeRemainedBy8(start, end);
                start = ComputeRemainedBy4(start, end);
                ComputeRemainedBytes(start, end);
            }
        }

        private void AssertEnsureBufferLengthEven(int len)
        {
            _remained -= (uint)len;

            if (_remained > 8 && len % 8 != 0)
                throw new InvalidOperationException("While appending the buffer, the source buffer length must be 8 bytes aligned " +
                                                    "(divided by 8) while remained data is still more than 8 bytes left! (due to MurmurHash2-64B design flaws). " +
                                                    "If you're getting the data from a Stream, Please use Stream.ReadAtLeast() or Stream.ReadExactly() to " +
                                                    "ensure that your buffer is always filled!");
        }

        private unsafe void ComputeRemainedBytes(byte* start, byte* end)
        {
            uint remain = (uint)(end - start);
            if (remain <= 0)
            {
                return;
            }

            switch (remain)
            {
                case 3:
                    _lowBytes ^= (uint)*(start + 2) << 16;
                    _lowBytes ^= (uint)*(start + 1) << 8;
                    _lowBytes ^= *start;
                    break;
                case 2:
                    _lowBytes ^= (uint)*(start + 1) << 8;
                    _lowBytes ^= *start;
                    break;
                case 1:
                    _lowBytes ^= *start;
                    break;
            }
            _lowBytes *= Bm;
        }

        private unsafe byte* ComputeRemainedBy4(byte* start, byte* end)
        {
            if (end < start + 4)
            {
                return start;
            }

            uint k1 = *(uint*)start;
            k1         *= Bm;
            k1         ^= k1 >> Br;
            k1         *= Bm;
            _highBytes *= Bm;
            _highBytes ^= k1;
            start      += 4;

            return start;
        }

        private unsafe byte* ComputeRemainedBy8(byte* start, byte* end)
        {
            while (start + 8 <= end)
            {
                uint k1 = *(uint*)start;
                k1         *= Bm;
                k1         ^= k1 >> Br;
                k1         *= Bm;
                _highBytes *= Bm;
                _highBytes ^= k1;

                start += 4;

                uint k2 = *(uint*)start;
                k2        *= Bm;
                k2        ^= k2 >> Br;
                k2        *= Bm;
                _lowBytes *= Bm;
                _lowBytes ^= k2;

                start += 4;
            }

            return start;
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            _highBytes = (uint)_seed ^ _readLengthTarget;
            _lowBytes  = (uint)(_seed >> 32);
            _remained  = _readLengthTarget;
        }

        /// <inheritdoc/>
        protected override void GetCurrentHashCore(Span<byte> destination)
        {
            uint highBytes = (_highBytes ^ _lowBytes >> 18) * Bm;
            uint lowBytes  = _lowBytes ^ highBytes >> 22;

            lowBytes  *= Bm;
            highBytes ^= lowBytes >> 17;
            highBytes *= Bm;
            lowBytes  ^= highBytes >> 19;
            lowBytes  *= Bm;

            ulong hash = ((ulong)highBytes << 32) | lowBytes;
            MemoryMarshal.Write(destination, in hash);
        }

        /// <inheritdoc/>
        protected override void GetHashAndResetCore(Span<byte> destination)
        {
            GetCurrentHashCore(destination);
            Reset();
        }
    }
}
