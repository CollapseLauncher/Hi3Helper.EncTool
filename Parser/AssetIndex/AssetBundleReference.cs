using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using ZstdCompressionStream = ZstdNet.CompressionStream;
using ZstdCompressionOptions = ZstdNet.CompressionOptions;
using ZstdDecompressionStream = ZstdNet.DecompressionStream;

namespace Hi3Helper.EncTool.Parser.AssetIndex
{
    [Flags]
    public enum AssetBundleReferenceHashFlag : uint
    {
        CRC32    = 0b_00000000_00000000_00000000_00000001,
        CRC64    = 0b_00000000_00000000_00000000_00000010,
        XXH32    = 0b_00000000_00000000_00000000_00000100,
        XXH64    = 0b_00000000_00000000_00000000_00001000,
        XXH3_64  = 0b_00000000_00000000_00000000_00010000,
        XXH3_128 = 0b_00000000_00000000_00000000_00100000,
        Murmur   = 0b_00000000_00000000_00000000_01000000,
        Murmur64 = 0b_00000000_00000000_00000000_10000000,
        MD5      = 0b_00000000_00000000_00000000_00000001,
        SHA1     = 0b_00000000_00000000_00000000_00000010,
        SHA256   = 0b_00000000_00000000_00000000_00000100,

        HasHMAC  = 0b_00000000_00000001_00000000_00000000,
        HasSeed  = 0b_00000000_00000010_00000000_00000000,
        IsCrypto = 0b_00000001_00000000_00000000_00000000
    }

    [Flags]
    public enum AssetBundleReferenceHeaderFlag : ushort
    {
        IsCompressed = 0b_00000000_00000001,
        IsEncrypted  = 0b_00000000_00000010,

        Compression_Brotli  = 0b_00000001_00000000,
        Compression_Zstd    = 0b_00000010_00000000,
        Compression_Deflate = 0b_00000100_00000000,
        Compression_Gzip    = 0b_00001000_00000000
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct AssetBundleReferenceData
    {
        public fixed char                   Name[64];
        public long                         Size;
        public int                          HashSize;
        public AssetBundleReferenceHashFlag HashFlag;
        public fixed byte                   Hash[16];
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct AssetBundleReferenceKVPData
    {
        public fixed char Keys[24];
        public int        DataSize;
        public int        DataCount;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 8)]
    public unsafe struct AssetBundleReferenceHeader
    {
        [FieldOffset(0)]  public ulong                          Header; // Expected value should be "Collapse"
        [FieldOffset(8)]  public short                          Version;
        [FieldOffset(16)] public AssetBundleReferenceHeaderFlag HeaderFlag;
        [FieldOffset(20)] public int                            DataStructSize;
        [FieldOffset(12)] public int                            DataStructCount;
        [FieldOffset(64)] public fixed byte                     AdditionalMetadata[192];

        public AssetBundleReferenceHeader()
        {
            Header = AssetBundleReference.CollapseHeader;
            Version = 1;
        }
    }

    public static class AssetBundleReference
    {
        internal const ulong CollapseHeader = 7310310183885631299;

        public static unsafe AssetBundleReferenceData[] CreateData(Dictionary<string, List<AssetBundleReferenceData>> dictionary)
        {
            return Enumerate(dictionary).ToArray();

            IEnumerable<AssetBundleReferenceData> Enumerate(Dictionary<string, List<AssetBundleReferenceData>> dictionary)
            {
                foreach (var item in dictionary)
                {
                    foreach (var data in item.Value)
                    {
                        yield return data;
                    }
                }
            }
        }

        public static unsafe AssetBundleReferenceKVPData[] CreateKVP(Dictionary<string, List<AssetBundleReferenceData>> dictionary)
        {
            AssetBundleReferenceKVPData[] kvp = new AssetBundleReferenceKVPData[dictionary.Count];
            int dataSize = sizeof(AssetBundleReferenceData);

            foreach (var kvpItem in dictionary.Index())
            {
                int index = kvpItem.Index;
                string key = kvpItem.Item.Key;
                AssetBundleReferenceKVPData kvpData = new AssetBundleReferenceKVPData
                {
                    DataCount = kvpItem.Item.Value.Count,
                    DataSize = dataSize
                };

                kvpItem.Item.Key.AsSpan().CopyTo(new Span<char>(kvpData.Keys, key.Length));
                kvp[index] = kvpData;
            }

            return kvp;
        }

        public static unsafe (AssetBundleReferenceHeader Header, AssetBundleReferenceKVPData[] KVP, AssetBundleReferenceData[] Data)
            ReadAssetBundleReference(Stream stream)
        {
            // Read header buffer
            AssetBundleReferenceHeader header = new();
            Span<byte> headerBuffer = AsBytesSpan(ref header);
            stream.ReadExactly(headerBuffer);

            // If the header is not valid, throw an exception
            if (header.Header != CollapseHeader)
                throw new InvalidDataException("AssetBundleReference header is not valid!");

            // If the version is not 1, then throw
            if (header.Version != 1)
                throw new NotSupportedException($"AssetBundleReference version is not supported: {header.Version}");

            // Create input stream (as create decompression stream if any)
            using Stream inputStream = CreateDecompressionStream(stream, ref header);

            // Get KVP struct array
            AssetBundleReferenceKVPData[] kvpData = ReadStructFromStream<AssetBundleReferenceKVPData>(inputStream, header.DataStructCount, header.DataStructSize);

            // Get actual data struct array
            int actualDataCount = 0;
            int actualDataSize = 0;
            for (int i = 0; i < kvpData.Length; i++)
            {
                actualDataSize = kvpData[i].DataSize;
                actualDataCount += kvpData[i].DataCount;
            }
            AssetBundleReferenceData[] actualData = ReadStructFromStream<AssetBundleReferenceData>(inputStream, actualDataCount, actualDataSize);

            // Return the header and the Key-value pair data
            return (header, kvpData, actualData);
        }

        private static unsafe T[] ReadStructFromStream<T>(Stream inputStream, int dataCount, int structSize)
            where T : unmanaged
        {
            T[] structData = new T[dataCount];
            int expectedDataLength = dataCount * structSize;
            Span<byte> dataAsBytes = new(Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(structData)), expectedDataLength);

            // Read the data struct from stream
            int read;
            int offset = 0;
            while ((read = inputStream.Read(dataAsBytes[offset..])) > 0)
            {
                offset += read;
                if (offset >= expectedDataLength)
                    throw new Exception($"AssetBundleReference's read data is larger than expected: {expectedDataLength} bytes");
            }

            return structData;
        }

        public static void WriteAssetBundleReference(Stream stream,
                                                     ref AssetBundleReferenceHeader header,
                                                     ReadOnlySpan<AssetBundleReferenceKVPData> referenceKvpData,
                                                     ReadOnlySpan<AssetBundleReferenceData> referenceData)
        {
            // Read header struct as header and write into the stream
            ReadOnlySpan<byte> headerSpan = AsBytesReadOnlySpan(ref header);
            stream.Write(headerSpan);

            // Create the decompression stream
            using Stream outputStream = CreateCompressionStream(stream, ref header);
            WriteStructToStream(referenceKvpData, outputStream);
            WriteStructToStream(referenceData, outputStream);
        }

        private static void WriteStructToStream<T>(ReadOnlySpan<T> referenceKvpData, Stream outputStream)
            where T : unmanaged
        {
            // Get a reference of start and end of the data struct span
            ref T dataStart = ref MemoryMarshal.GetReference(referenceKvpData);
            ref T dataEnd = ref Unsafe.Add(ref dataStart, referenceKvpData.Length);

            // Do a loop and write the data struct into the stream
            while (Unsafe.IsAddressLessThan(ref dataStart, ref dataEnd))
            {
                ReadOnlySpan<byte> dataSpan = AsBytesReadOnlySpan(ref dataStart);
                outputStream.Write(dataSpan);

                dataStart = ref Unsafe.Add(ref dataStart, 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static ReadOnlySpan<byte> AsBytesReadOnlySpan<T>(ref T data)
            where T : unmanaged
        {
            byte* asBytesPtr = (byte*)Unsafe.AsPointer(ref data);
            ReadOnlySpan<byte> dataSpan = new(asBytesPtr, sizeof(T));
            return dataSpan;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static Span<byte> AsBytesSpan<T>(ref T data)
            where T : unmanaged
        {
            byte* asBytesPtr = (byte*)Unsafe.AsPointer(ref data);
            Span<byte> dataSpan = new(asBytesPtr, sizeof(T));
            return dataSpan;
        }

        private static Stream CreateCompressionStream(Stream stream, ref AssetBundleReferenceHeader header)
        {
            bool isCompressed = header.HeaderFlag.HasFlag(AssetBundleReferenceHeaderFlag.IsCompressed);

            if (!isCompressed)
            {
                return stream;
            }

            if (header.HeaderFlag.HasFlag(AssetBundleReferenceHeaderFlag.Compression_Brotli))
            {
                return new BrotliStream(stream, new BrotliCompressionOptions
                {
                    Quality = 11,
                }, true);
            }

            if (header.HeaderFlag.HasFlag(AssetBundleReferenceHeaderFlag.Compression_Zstd))
            {
                return new ZstdCompressionStream(stream, new ZstdCompressionOptions(22));
            }

            if (header.HeaderFlag.HasFlag(AssetBundleReferenceHeaderFlag.Compression_Deflate))
            {
                return new DeflateStream(stream, new ZLibCompressionOptions
                {
                    CompressionLevel = 9,
                    CompressionStrategy = ZLibCompressionStrategy.Default
                }, true);
            }

            if (header.HeaderFlag.HasFlag(AssetBundleReferenceHeaderFlag.Compression_Gzip))
            {
                return new GZipStream(stream, new ZLibCompressionOptions
                {
                    CompressionLevel = 9,
                    CompressionStrategy = ZLibCompressionStrategy.Default
                }, true);
            }

            throw new NotSupportedException("Unsupported compression type!");
        }

        private static Stream CreateDecompressionStream(Stream stream, ref AssetBundleReferenceHeader header)
        {
            bool isCompressed = header.HeaderFlag.HasFlag(AssetBundleReferenceHeaderFlag.IsCompressed);

            if (!isCompressed)
            {
                return stream;
            }

            if (header.HeaderFlag.HasFlag(AssetBundleReferenceHeaderFlag.Compression_Brotli))
            {
                return new BrotliStream(stream, CompressionMode.Decompress, true);
            }

            if (header.HeaderFlag.HasFlag(AssetBundleReferenceHeaderFlag.Compression_Zstd))
            {
                return new ZstdDecompressionStream(stream);
            }

            if (header.HeaderFlag.HasFlag(AssetBundleReferenceHeaderFlag.Compression_Deflate))
            {
                return new DeflateStream(stream, CompressionMode.Decompress, true);
            }

            if (header.HeaderFlag.HasFlag(AssetBundleReferenceHeaderFlag.Compression_Gzip))
            {
                return new GZipStream(stream, CompressionMode.Decompress, true);
            }

            throw new NotSupportedException("Unsupported compression type!");
        }
    }
}
