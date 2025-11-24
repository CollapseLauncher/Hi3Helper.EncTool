using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable UnusedMember.Global

#nullable enable
namespace Hi3Helper.EncTool.Parser.SimpleZipArchiveReader;

public partial class SimpleZipArchiveEntry
{
    #region Constants
    private const  uint ZipCentralDirectoryMagic = 0x02014b50;
    private const  uint ZipLocalHeaderMagic      = 0x04034b50;
    internal const uint Zip64Mask                = uint.MaxValue;
    #endregion

    #region Public Properties
    public required string         Filename       { get; init; }
    public required string?        Comment        { get; init; }
    public required long           Size           { get; init; }
    public required long           SizeCompressed { get; init; }
    public required DateTimeOffset LastModified   { get; init; }
    public required BitFlagValues  Flags          { get; init; }
    public required uint           Crc32          { get; init; }
    public required bool           IsDeflate64    { get; init; }
    public required bool           IsDeflate      { get; init; }

    public bool IsDirectory => Filename[^1] is '/' or '\\';

    public override string ToString() => IsDirectory
        ? Filename + (string.IsNullOrEmpty(Comment) ? string.Empty : $" | Comment: {Comment}")
        : $"{Filename} | SizeU: {Size} | SizeC: {SizeCompressed}" + (string.IsNullOrEmpty(Comment) ? string.Empty : $" | Comment: {Comment}");

    #endregion

    #region Isolated Properties
    private long LocalBlockOffsetFromStream { get; init; }
    #endregion

    /// <summary>
    /// Open the entry as <see cref="Stream"/> from the factory.
    /// </summary>
    /// <param name="streamFactory">The factory of the source <see cref="Stream"/> for the reader to read from.</param>
    /// <param name="token">Cancellation token for asynchronous operations.</param>
    /// <returns>Either decompression <see cref="DeflateStream"/> or non-compressed <see cref="Stream"/> (Stored).</returns>
    /// <exception cref="InvalidOperationException"/>
    public async Task<Stream> OpenStreamFromFactoryAsync(
        StreamFactoryAsync streamFactory,
        CancellationToken  token = default)
    {
        const int localHeaderLen      = 30;
        const int filenameLenOffset   = 26;
        const int extraFieldLenOffset = 28;

        if (IsDirectory)
        {
            throw new InvalidOperationException("Cannot open Stream for Directory-kind entry.");
        }

        Stream stream = await streamFactory(LocalBlockOffsetFromStream, null, token);

        byte[]  headerBuffer    = ArrayPool<byte>.Shared.Rent(localHeaderLen);
        byte[]? extraDataBuffer = null;
        try
        {
            // Try skip local header
            int read = await stream.ReadAtLeastAsync(headerBuffer.AsMemory(0, localHeaderLen),
                                                     localHeaderLen,
                                                     false,
                                                     token);

            if (read < localHeaderLen)
            {
                throw new InvalidOperationException("Local Zip Block header is invalid!");
            }

            uint   signature     = MemoryMarshal.Read<uint>(headerBuffer);
            ushort fileNameLen   = MemoryMarshal.Read<ushort>(headerBuffer.AsSpan(filenameLenOffset));
            ushort extraFieldLen = MemoryMarshal.Read<ushort>(headerBuffer.AsSpan(extraFieldLenOffset));
            if (ZipLocalHeaderMagic != signature ||
                fileNameLen > short.MaxValue ||
                extraFieldLen > short.MaxValue)
            {
                throw new
                    InvalidOperationException("Local Zip Block header signature is invalid! Zip might be corrupted.");
            }

            int extraDataLen = fileNameLen + extraFieldLen;
            extraDataBuffer = ArrayPool<byte>.Shared.Rent(extraDataLen);
            _ = await stream.ReadAtLeastAsync(extraDataBuffer.AsMemory(0, extraDataLen),
                                              extraDataLen,
                                              false,
                                              token);

            SequentialReadStream chunkStream = new(stream, SizeCompressed);
            if (!IsDeflate)
            {
                return chunkStream;
            }

            return new DeflateStream(chunkStream, CompressionMode.Decompress);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(headerBuffer);
            if (extraDataBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(extraDataBuffer);
            }
        }
    }
}
