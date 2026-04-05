using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global

#nullable enable
namespace Hi3Helper.EncTool.Parser.CacheParser;

public enum CGPCKType : byte
{
    MustHave = 0,
    WithAll  = 1,
    Needing  = 2,
    All      = 100
}

public enum CGCategory
{
    MainStory,
    ExtraStory,
    Activity,
    VersionPV,
    Birthday,
    Misc = 1001
}

public enum CGPlayMode
{
    BothPlayInLevelInReplay,
    OnlyPlayInLevel
}

public enum CGUnlockType
{
    Level,
    Material,
    OWStory,
    PjmsCondition
}

public enum CGDownloadMode
{
    DownloadTipOnce,
    DownloadTipAlways,
    TipButEnableEnter
}

public class KianaCgMetadata
{
    public CGCategory     Category      { get; init; }
    public CGPlayMode     PlayMode      { get; init; }
    public CGDownloadMode DownloadMode  { get; init; }
    public CGPCKType      PckType       { get; init; }
    public string?        PathJp        { get; init; }
    public string?        PathCn        { get; init; }
    public long           SizeJp        { get; init; }
    public long           SizeCn        { get; init; }
    public int            SubCategoryId { get; init; }

    public override int GetHashCode() =>
        HashCode.Combine(Category, PlayMode, DownloadMode, PathJp, PathCn, SizeJp, SizeCn);

    public override string ToString() => $"{Category} | {DownloadMode} | {PckType} | {SubCategoryId} | {PathJp ?? PathCn ?? ""}";

    public static Dictionary<int, KianaCgMetadata> Parse(Stream stream)
    {
        byte[] data     = ArrayPool<byte>.Shared.Rent(1 << 20);
        int[]  assetIds = ArrayPool<int>.Shared.Rent(64 << 10);

        try
        {
            int read = stream.ReadAtLeast(data, data.Length, false);
            ReadOnlySpan<byte> span = InitializeDictionary(data.AsSpan(0, read),
                                                           assetIds,
                                                           out int metadataSize,
                                                           out Dictionary<int, KianaCgMetadata> result);

            if (read != metadataSize)
            {
                throw new InvalidOperationException($"Data received/decrypted from the stream is incomplete! Expecting {metadataSize} bytes but received {read} instead");
            }

            ReadCore((int)data.GetOffsetRelativeTo(span), span, assetIds, result);
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(data);
            ArrayPool<int>.Shared.Return(assetIds);
        }
    }

    private static ReadOnlySpan<byte> InitializeDictionary(
        ReadOnlySpan<byte>                   data,
        Span<int>                            assetIds,
        out int                              metadataSize,
        out Dictionary<int, KianaCgMetadata> dictionary)
    {
        ReadOnlySpan<byte> readSpan = data.Read(out metadataSize);
        readSpan = readSpan.Read(out int assetCount);

        if (metadataSize < 0 || metadataSize > data.Length)
        {
            throw new IndexOutOfRangeException($"Metadata size is too big or reported as negative number ({metadataSize} mentioned in metadata instead)");
        }

        if (assetCount is < 0 or > 64 << 10)
        {
            throw new IndexOutOfRangeException($"Metadata asset count is too big or reported as negative number ({assetCount} mentioned in metadata instead)");
        }

        // Initialize the dictionary. Read the asset IDs and make it as the key.
        dictionary = [];

        int readAssetCount = assetCount;
        int index          = 0;
        while (readAssetCount > 0)
        {
            --readAssetCount;
            readSpan = readSpan.Read(out int assetId);
            assetIds[index++] = assetId;

            CollectionsMarshal.GetValueRefOrAddDefault(dictionary, assetId, out bool exist);
            if (exist)
            {
                throw new InvalidOperationException($"CG asset with ID: {assetId} is duplicated!");
            }
        }

        return readSpan;
    }

    private static void ReadCore(
        int                              dataOriginOffset,
        ReadOnlySpan<byte>               data,
        ReadOnlySpan<int>                assetIds,
        Dictionary<int, KianaCgMetadata> dictionary)
    {
        Span<int> relativeOffsets = stackalloc int[dictionary.Count];
        int dataOffset = dataOriginOffset + dictionary.Count * sizeof(int); // Advanced to the offset of the data struct offsets

        data = GetDataStructOffsets(dataOffset, relativeOffsets, data, dictionary);
        for (int offsetIndex = 0; offsetIndex < relativeOffsets.Length; offsetIndex++)
        {
            GetChunkRange(offsetIndex, relativeOffsets, out int offset, out Range chunkRange);
            int chunkAbsoluteOffset = dataOffset + offset;

            ReadOnlySpan<byte> chunk = data[chunkRange];
            GetCgPathFieldOffsetInChunk(chunk, chunkAbsoluteOffset, out int pathCnOffset, out int pathJpOffset);

            ReadOnlySpan<byte> chunkPathCn = chunk[pathCnOffset..];
            ReadOnlySpan<byte> chunkPathJp = chunk[pathJpOffset..];

            string? dataPathCn = chunkPathCn.ReadFixedLengthString();
            string? dataPathJp = chunkPathJp.ReadFixedLengthString();

            GetOtherFieldsInChunk(chunk,
                                  out CGCategory dataCgCategory,
                                  out CGDownloadMode dataCgDownloadMode,
                                  out CGPlayMode dataCgPlayMode,
                                  out CGPCKType dataCgPckType,
                                  out int dataCgSubCategoryId,
                                  out int dataSizeCn,
                                  out int dataSizeJp);

            int                  assetIdKey = assetIds[offsetIndex];
            ref KianaCgMetadata? metadata   = ref CollectionsMarshal.GetValueRefOrAddDefault(dictionary, assetIdKey, out _);
            if (metadata != null)
            {
                throw new InvalidOperationException("Metadata was previously added. The data is duplicated!");
            }

            metadata = new KianaCgMetadata
            {
                Category      = dataCgCategory,
                DownloadMode  = dataCgDownloadMode,
                PlayMode      = dataCgPlayMode,
                PckType       = dataCgPckType,
                SubCategoryId = dataCgSubCategoryId,
                PathCn        = dataPathCn,
                PathJp        = dataPathJp,
                SizeCn        = dataSizeCn,
                SizeJp        = dataSizeJp
            };
        }
    }

    private static void GetOtherFieldsInChunk(
        ReadOnlySpan<byte> chunk,
        out CGCategory     cgCategory,
        out CGDownloadMode cgDownloadMode,
        out CGPlayMode     cgPlayMode,
        out CGPCKType      cgPckType,
        out int            cgSubCategoryId,
        out int            sizeCn,
        out int            sizeJp)
    {
        const int offsetCgCategory      = 13;
        const int offsetCgSubCategoryId = 21;
        const int offsetCgDownloadMode  = 54;
        const int offsetCgPlayMode      = 58;
        const int offsetSizeCn          = 70;
        const int offsetSizeJp          = 78;
        const int offsetCgPckType       = 82;

        chunk[offsetCgCategory..].Read(out cgCategory);
        chunk[offsetCgSubCategoryId..].Read(out cgSubCategoryId);
        chunk[offsetCgDownloadMode..].Read(out cgDownloadMode);
        chunk[offsetCgPlayMode..].Read(out cgPlayMode);

        chunk[offsetSizeCn..].Read(out sizeCn);
        chunk[offsetSizeJp..].Read(out sizeJp);

        chunk[offsetCgPckType..].Read(out cgPckType);
    }

    private static void GetCgPathFieldOffsetInChunk(
        ReadOnlySpan<byte> chunk,
        int                chunkAbsoluteOffset,
        out int            pathCnOffsetInChunk,
        out int            pathJpOffsetInChunk)
    {
        const int startCn = 34;
        const int startJp = 42;

        chunk[startCn..].Read(out int pathCnAbsOffset);
        chunk[startJp..].Read(out int pathJpAbsOffset);

        pathCnOffsetInChunk = pathCnAbsOffset - chunkAbsoluteOffset;
        pathJpOffsetInChunk = pathJpAbsOffset - chunkAbsoluteOffset;
    }

    private static void GetChunkRange(int offsetIndex, ReadOnlySpan<int> offsets, out int offset, out Range chunkRange)
    {
        offset = offsets[offsetIndex];
        if (offsets.Length - 1 > offsetIndex)
        {
            chunkRange = new Range(offset, offsets[offsetIndex + 1]);
            return;
        }

        chunkRange = new Range(offset, new Index(0, true));
    }

    private static ReadOnlySpan<byte> GetDataStructOffsets(
        int                              dataOriginOffset,
        scoped Span<int>                 relativeOffsets,
        ReadOnlySpan<byte>               data,
        Dictionary<int, KianaCgMetadata> dictionary)
    {
        int count = dictionary.Count;
        for (int i = 0; i < count; i++)
        {
            data               = data.Read(out int absoluteOffset);
            relativeOffsets[i] = absoluteOffset - dataOriginOffset;
        }

        return data;
    }
}

file static class SpanReaderExtension
{
    public static unsafe ReadOnlySpan<byte> Read<T>(
        this ReadOnlySpan<byte> span,
        out  T                  result)
        where T : unmanaged
    {
        int sizeOf = sizeof(T);
        if (span.Length < sizeOf)
        {
            throw new IndexOutOfRangeException($"Size of the remained read buffer is too small to read type: {typeof(T).Name}");
        }

        result = MemoryMarshal.Read<T>(span);
        return span[sizeOf..];
    }

    public static nint GetOffsetRelativeTo<T>(this ReadOnlySpan<T> origin,
                                              ReadOnlySpan<T>      target)
        where T : unmanaged
    {
        ReadOnlySpan<byte> originBytes = MemoryMarshal.AsBytes(origin);
        ReadOnlySpan<byte> targetBytes = MemoryMarshal.AsBytes(target);

        ref readonly byte originRef = ref MemoryMarshal.AsRef<byte>(originBytes);
        ref readonly byte targetRef = ref MemoryMarshal.AsRef<byte>(targetBytes);

        return Unsafe.ByteOffset(in originRef, in targetRef);
    }

    public static string? ReadFixedLengthString(this ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return null;
        }

        data.Read(out ushort len);
        return Encoding.UTF8.GetString(data.Slice(sizeof(ushort), len));
    }
}