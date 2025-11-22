using System;
using System.Buffers;
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
/// A Simple Zip Archive reader with ability to read from remote HTTP(S) source or any Stream factories.
/// </summary>
public class ZipArchiveReader
{
    #region Constants
    private const   int                EOCDBufferLength = 64 << 10;
    internal static ReadOnlySpan<byte> ZipEODRHeaderMagic => "PK\x05\x06"u8;
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

            uint offsetOfCD;
            uint sizeOfCD;
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

        uint offsetOfCD;
        uint sizeOfCD;
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
        uint               size,
        uint               offset,
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
                    throw new IndexOutOfRangeException("Stream has prematurely reached End of Stream while more bytes needs to be read");
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

    private static async ValueTask<(uint Offset, uint Size)>
        FindCentralDirectoryOffsetAndSizeAsync(
            Stream stream,
            int bufferSize,
            CancellationToken token)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            int read = await stream.ReadAtLeastAsync(buffer, bufferSize, false, token);
            ReadOnlySpan<byte> bufferSpan = buffer.AsSpan(0, read);

            int lastIndexOfMagic = bufferSpan.LastIndexOf(ZipEODRHeaderMagic);
            if (lastIndexOfMagic < 0)
            {
                throw new IndexOutOfRangeException("Cannot find an offset of the Central Directory");
            }

            bufferSpan = bufferSpan[lastIndexOfMagic..];

            uint sizeCDOnStream   = MemoryMarshal.Read<uint>(bufferSpan[12..]);
            uint offsetCDOnStream = MemoryMarshal.Read<uint>(bufferSpan[16..]);
            return (offsetCDOnStream, sizeCDOnStream);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<Stream> GetHttpStreamFromPosAsync(
        HttpClient client,
        Uri url,
        long? offset,
        long? length,
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
}
