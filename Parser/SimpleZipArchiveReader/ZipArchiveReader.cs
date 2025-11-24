using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

#nullable enable
namespace Hi3Helper.EncTool.Parser.SimpleZipArchiveReader;

/// <summary>
/// A delegate function to create an instance of <see cref="Stream"/> for the <see cref="ZipArchiveReader"/> to read from.
/// </summary>
/// <param name="offset">The offset or position of the <see cref="Stream"/> to read from.</param>
/// <param name="length">Size of data to be read.</param>
/// <param name="token">Cancellation token for asynchronous operations.</param>
/// <returns>An instance of <see cref="Stream"/> for the <see cref="ZipArchiveReader"/> to read from.</returns>
public delegate Task<Stream> StreamFactoryAsync(long? offset, long? length, CancellationToken token);

/// <summary>
/// A Simple Zip Archive reader with ability to read from remote HTTP(S) source or any Stream factories.<br/>
/// This instance extends <see cref="IReadOnlyCollection{T}"/> and can be enumerated.
/// </summary>
public class ZipArchiveReader : IReadOnlyCollection<SimpleZipArchiveEntry>
{
    #region Constants
    private const   int                EOCDBufferLength = 64 << 10;
    internal static ReadOnlySpan<byte> ZipEOCDRHeaderMagic    => "PK\x05\x06"u8;
    internal static ReadOnlySpan<byte> Zip64EOCDRHeaderMagic  => "PK\x06\x06"u8;
    internal static ReadOnlySpan<byte> Zip64EOCDRLHeaderMagic => "PK\x06\x07"u8;
    #endregion

    #region Properties
    public bool                        IsEmpty => Entries.Count == 0;
    public List<SimpleZipArchiveEntry> Entries { get; } = [];
    #endregion

    #region Public Methods
    /// <summary>
    /// Creates a <see cref="ZipArchiveReader"/> from a remote HTTP(S) URL.
    /// </summary>
    /// <param name="url">The URL of the Zip archive.</param>
    /// <param name="token">Cancellation token for asynchronous operations.</param>
    /// <returns>A parsed Zip Archive including entries to read from.</returns>
    public static Task<ZipArchiveReader> CreateFromRemoteAsync(
        string            url,
        CancellationToken token = default) =>
        CreateFromRemoteAsync(new Uri(url), token);

    /// <summary>
    /// Creates a <see cref="ZipArchiveReader"/> from a remote HTTP(S) URL.
    /// </summary>
    /// <param name="url">The URL of the Zip archive.</param>
    /// <param name="token">Cancellation token for asynchronous operations.</param>
    /// <returns>A parsed Zip Archive including entries to read from.</returns>
    public static async Task<ZipArchiveReader> CreateFromRemoteAsync(
        Uri               url,
        CancellationToken token = default)
    {
        HttpClient client = CreateSocketHandlerHttpClient();

        try
        {
            UrlStatus response = await client.GetCachedUrlStatus(url, token);
            response.EnsureSuccessStatusCode();

            if (response.FileSize == 0)
            {
                throw new NotSupportedException($"The requested URL: {url} doesn't have Content-Length response header or the file content is empty!");
            }

            long offsetOfCD;
            long sizeOfCD;
            long offsetOfEOCD = Math.Clamp(response.FileSize - EOCDBufferLength,
                                           0,
                                           response.FileSize);

            await using (Stream bufferStreamOfEOCD =
                         await GetHttpStreamFromPosAsync(client,
                                                         url,
                                                         offsetOfEOCD,
                                                         null,
                                                         token))
            {
                (offsetOfCD, sizeOfCD) =
                    await FindCentralDirectoryOffsetAndSizeAsync(bufferStreamOfEOCD,
                                                                 EOCDBufferLength,
                                                                 token);
            }

            if (offsetOfCD == 0)
            {
                throw new InvalidOperationException("Cannot find Central Directory Record offset");
            }

            if (sizeOfCD == 0)
            {
                return new ZipArchiveReader();
            }

            return await CreateFromCentralDirectoryStreamAsync(CreateStreamFromOffset,
                                                               sizeOfCD,
                                                               offsetOfCD,
                                                               token);
        }
        finally
        {
            client.Dispose();
        }

        static HttpClient CreateSocketHandlerHttpClient()
        {
            SocketsHttpHandler httpHandler = new()
            {
                // Using HTTP-side compression causing content-length to be unsupported,
                // making us unable to get the exact size of the Zip archive and thus locating
                // the offset of central directory.
                //
                // So in this case, we are disabling it for good measure.
                AutomaticDecompression = DecompressionMethods.None
            };
            return new HttpClient(httpHandler);
        }

        Task<Stream> CreateStreamFromOffset(long? offset, long? length, CancellationToken innerToken)
            => GetHttpStreamFromPosAsync(client, url, offset, length, innerToken);
    }

    /// <summary>
    /// Creates a <see cref="ZipArchiveReader"/> from a Stream factory.
    /// </summary>
    /// <param name="streamFactory">The factory of the source <see cref="Stream"/> for the reader to read from.</param>
    /// <param name="token">Cancellation token for asynchronous operations.</param>
    /// <returns>A parsed Zip Archive including entries to read from.</returns>
    public static async Task<ZipArchiveReader> CreateFromStreamFactoryAsync(
        StreamFactoryAsync streamFactory,
        CancellationToken  token = default)
    {
        long streamLength = await GetLengthFromStreamFactoryAsync(streamFactory, token);
        if (streamLength <= 0)
        {
            throw new InvalidOperationException("Stream has 0 bytes in size!");
        }

        long offsetOfCD;
        long sizeOfCD;
        long offsetOfEOCD = Math.Clamp(streamLength - EOCDBufferLength,
                                       0,
                                       streamLength);

        await using (Stream bufferStreamOfEOCD =
                     await streamFactory(offsetOfEOCD, EOCDBufferLength, token))
        {
            (offsetOfCD, sizeOfCD) =
                await FindCentralDirectoryOffsetAndSizeAsync(bufferStreamOfEOCD,
                                                             EOCDBufferLength,
                                                             token);
        }

        if (offsetOfCD <= 0)
        {
            throw new InvalidOperationException("Cannot find Central Directory Record offset");
        }

        if (sizeOfCD == 0)
        {
            return new ZipArchiveReader();
        }

        return await CreateFromCentralDirectoryStreamAsync(streamFactory,
                                                           sizeOfCD,
                                                           offsetOfCD,
                                                           token);
    }
    #endregion

    #region Utilities

    private static async Task<ZipArchiveReader>
        CreateFromCentralDirectoryStreamAsync(
        StreamFactoryAsync streamFactory,
        long               size,
        long               offset,
        CancellationToken  token = default)
    {
        if (size == 0)
        {
            return new ZipArchiveReader();
        }

        bool isUseRentBuffer = size <= 4 << 20;
        byte[] centralDirectoryBuffer = isUseRentBuffer
            ? ArrayPool<byte>.Shared.Rent((int)size)
            : GC.AllocateUninitializedArray<byte>((int)size);

        try
        {
            await using Stream centralDirectoryStream =
                await streamFactory(offset, null, token);

            int bufferOffset = 0;
            while (size > 0)
            {
                int read = await centralDirectoryStream
                   .ReadAsync(centralDirectoryBuffer.AsMemory(bufferOffset, (int)size),
                              token);

                if (read == 0)
                {
                    throw new IndexOutOfRangeException("Stream has prematurely reached End of Stream while more bytes need to be read");
                }

                bufferOffset += read;
                size         -= (uint)read;
            }

            ZipArchiveReader archive = new();

            ReadOnlySpan<byte> bufferSpan = centralDirectoryBuffer.AsSpan(0, bufferOffset);
            while (!bufferSpan.IsEmpty)
            {
                bufferSpan = SimpleZipArchiveEntry
                   .CreateFromBlockSpan(bufferSpan, out SimpleZipArchiveEntry entry);
                archive.Entries.Add(entry);
            }

            return archive;
        }
        finally
        {
            if (isUseRentBuffer)
            {
                ArrayPool<byte>.Shared.Return(centralDirectoryBuffer);
            }
        }
    }

    private static async Task<long> GetLengthFromStreamFactoryAsync(
        StreamFactoryAsync streamFactory,
        CancellationToken token = default)
    {
        await using Stream stream = await streamFactory(0, null, token);
        return stream.Length;
    }

    private static async ValueTask<(long Offset, long Size)>
        FindCentralDirectoryOffsetAndSizeAsync(
            Stream stream,
            int bufferSize,
            CancellationToken token)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            int                read             = await stream.ReadAtLeastAsync(buffer, bufferSize, false, token);
            ReadOnlySpan<byte> bufferSpan       = buffer.AsSpan(0, read);

            int lastIndexOfMagic32 = bufferSpan.LastIndexOf(ZipEOCDRHeaderMagic);
            int lastIndexOfMagic64 = bufferSpan.LastIndexOf(Zip64EOCDRHeaderMagic);
            if (lastIndexOfMagic32 < 0 && lastIndexOfMagic64 < 0)
            {
                throw new IndexOutOfRangeException("Cannot find an offset of the Central Directory");
            }

            // First, check if the archive uses Zip64 record for EOCDR.
            // If so, parse the Zip64 instead.
            return lastIndexOfMagic64 > 0
                ? FindCentralDirectoryOffsetAndSize64(bufferSpan, lastIndexOfMagic32, lastIndexOfMagic64)
                : FindCentralDirectoryOffsetAndSize32(bufferSpan, lastIndexOfMagic32);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static (uint Offset, uint Size)
        FindCentralDirectoryOffsetAndSize32(ReadOnlySpan<byte> buffer, int offset)
    {
        buffer = buffer[offset..];

        uint sizeCDOnStream   = MemoryMarshal.Read<uint>(buffer[12..]);
        uint offsetCDOnStream = MemoryMarshal.Read<uint>(buffer[16..]);
        return (offsetCDOnStream, sizeCDOnStream);
    }

    private static (long Offset, long Size)
        FindCentralDirectoryOffsetAndSize64(ReadOnlySpan<byte> buffer, int offset32, int offset64)
    {
        // Try to get the offset from Zip32 record first. Since the size can be dynamic
        // and not always be defined in Zip64 record.
        (uint offsetEOCDR32, uint sizeEOCDR32) = FindCentralDirectoryOffsetAndSize32(buffer, offset32);

        // Skip if both offset and size aren't exceeding uint.MaxValue, even though Zip64 EOCDR exist.
        if (offsetEOCDR32 != SimpleZipArchiveEntry.Zip64Mask &&
            sizeEOCDR32 != SimpleZipArchiveEntry.Zip64Mask)
        {
            return (offsetEOCDR32, sizeEOCDR32);
        }

        long offsetEOCDR64 = offsetEOCDR32;
        long sizeEOCDR64   = sizeEOCDR32;

        // Then, we try to capture the offset and size from Zip64 EOCDR.
        ReadOnlySpan<byte> buffer64 = buffer[offset64..];

        if (sizeEOCDR32 == SimpleZipArchiveEntry.Zip64Mask)
        {
            sizeEOCDR64 = MemoryMarshal.Read<long>(buffer64[40..]);
        }

        if (offsetEOCDR32 == SimpleZipArchiveEntry.Zip64Mask)
        {
            offsetEOCDR64 = MemoryMarshal.Read<long>(buffer64[48..]);
        }

        return (offsetEOCDR64, sizeEOCDR64);
    }

    private static async Task<Stream> GetHttpStreamFromPosAsync(
        HttpClient        client,
        Uri               url,
        long?             offset,
        long?             length,
        CancellationToken token)
    {
        HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(offset, offset + length);

        HttpResponseMessage? response = null;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            return await response.Content.ReadAsStreamAsync(token);
        }
        finally
        {
            if (!(response?.IsSuccessStatusCode ?? false))
            {
                request.Dispose();
                response?.Dispose();
            }
        }
    }

    #endregion

    #region IReadOnlyCollection extensions

    /// <summary>
    /// Gets the <see cref="SimpleZipArchiveEntry"/> entry at specific index.
    /// </summary>
    public SimpleZipArchiveEntry this[int index]
    {
        get => Entries[index];
        set => Entries[index] = value;
    }

    public IEnumerator<SimpleZipArchiveEntry> GetEnumerator() => Entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Gets the total count of available <see cref="SimpleZipArchiveEntry"/> entries
    /// </summary>
    public int Count => Entries.Count;

    #endregion
}
