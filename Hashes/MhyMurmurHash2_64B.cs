using System;
using System.Buffers;
using System.IO;
using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace Hi3Helper.EncTool.Hashes
{
    public class MhyMurmurHash2_64B : NonCryptographicHashAlgorithm
    {
        private const uint  M_32 = 0x5bd1e995;
        private const int   R_32 = 24;
        private const ulong A_M  = 0xc6a4a7935bd1e995;
        private const int   A_R  = 47;
        private const uint  B_M  = 0x5bd1e995;
        private const int   B_R  = 24;

        private readonly ulong Seed;
        private readonly uint  ReadLengthTarget;
        private          uint  HighBytes = 0;
        private          uint  LowBytes  = 0;
        private          uint  Remained  = 0;

        public MhyMurmurHash2_64B(uint readLengthTarget, ulong seed = 0) : base(8)
        {
            ReadLengthTarget = readLengthTarget;
            Seed             = seed;
            Reset();
        }

        public static MhyMurmurHash2_64B CreateForStream(Stream stream, ulong seed = 0, long? streamLengthExplicit = null)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (streamLengthExplicit != null && streamLengthExplicit < 0)
                throw new ArgumentOutOfRangeException(nameof(streamLengthExplicit), "Stream length must be non-negative.");

            long streamLength = 0;
            if (streamLengthExplicit == null && !TryGetStreamLength(stream, out streamLength))
                throw new NotSupportedException("Cannot get the length of the stream. Please explicitly define the stream length using streamLengthExplicit argument.");

            long readLengthTarget = streamLengthExplicit ?? streamLength;
            if (readLengthTarget > uint.MaxValue)
                throw new ArgumentOutOfRangeException("Stream length cannot be more than 4 GiB (4,294,967,295 bytes)");

            return new MhyMurmurHash2_64B((uint)readLengthTarget, seed);
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

        public override unsafe void Append(ReadOnlySpan<byte> source)
        {
            int len = source.Length;
            Remained -= (uint)len;

            if (Remained > 8 && len % 8 != 0)
                throw new InvalidOperationException(
                    "While appending the buffer, the source buffer length must be 8 bytes aligned " +
                    "(can be divided by 8) while remained data is still more than 8 bytes left! (It's MurmurHash2 design flaws). " + 
                    "If you're getting the data from a Stream, Please use Stream.ReadAtLeast() to " +
                    "ensure that your buffer is always filled!");

            fixed (byte* bytePtr = &MemoryMarshal.GetReference(source))
            {
                byte* start = bytePtr;
                byte* end   = bytePtr + len;

                while (start + 8 <= end)
                {
                    uint k1   =  *(uint*)start;
                    k1        *= B_M;
                    k1        ^= k1 >> B_R;
                    k1        *= B_M;
                    HighBytes *= B_M;
                    HighBytes ^= k1;

                    start += 4;

                    uint k2  =  *(uint*)start;
                    k2       *= B_M;
                    k2       ^= k2 >> B_R;
                    k2       *= B_M;
                    LowBytes *= B_M;
                    LowBytes ^= k2;

                    start += 4;
                }

                if (end >= start + 4)
                {
                    uint k1   =  *(uint*)start;
                    k1        *= B_M;
                    k1        ^= k1 >> B_R;
                    k1        *= B_M;
                    HighBytes *= B_M;
                    HighBytes ^= k1;
                    start     += 4;
                }

                uint remain = (uint)(end - start);
                if (remain > 0)
                {
                    switch (remain)
                    {
                        case 3:
                            LowBytes ^= (uint)*(start + 2) << 16;
                            LowBytes ^= (uint)*(start + 1) << 8;
                            LowBytes ^= *start;
                            break;
                        case 2:
                            LowBytes ^= (uint)*(start + 1) << 8;
                            LowBytes ^= *start;
                            break;
                        case 1:
                            LowBytes ^= *start;
                            break;
                    }
                    LowBytes *= B_M;
                }
            }
        }

        public override void Reset()
        {
            HighBytes = ((uint)Seed) ^ ReadLengthTarget;
            LowBytes  = (uint)(Seed >> 32);
            Remained  = ReadLengthTarget;
        }

        protected override void GetCurrentHashCore(Span<byte> destination)
        {
            uint highBytes = (HighBytes ^ LowBytes >> 18) * B_M;
            uint lowBytes  = LowBytes ^ highBytes >> 22;

            lowBytes  *= B_M;
            highBytes ^= lowBytes >> 17;
            highBytes *= B_M;
            lowBytes  ^= highBytes >> 19;
            lowBytes  *= B_M;

            ulong hash = ((ulong)highBytes << 32) | lowBytes;
            MemoryMarshal.Write(destination, in hash);
        }

        protected override void GetHashAndResetCore(Span<byte> destination)
        {
            GetCurrentHashCore(destination);
            Reset();
        }
    }
}
