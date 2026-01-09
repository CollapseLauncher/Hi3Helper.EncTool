using Hi3Helper.Http;
using System;
using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
// ReSharper disable once CheckNamespace
namespace Hi3Helper.EncTool;

public static class StreamExtensions
{
    internal static Task PerformCopyToDownload(this DownloadClient       downloadClient,
                                               string                    url,
                                               DownloadProgressDelegate? downloadDelegate,
                                               Stream                    targetStream,
                                               CancellationToken         token)
        => downloadClient
          .GetHttpClient()
          .PerformCopyToDownload(url, downloadDelegate, targetStream, token);

    internal static async Task PerformCopyToDownload(this HttpClient           client,
                                                     string                    url,
                                                     DownloadProgressDelegate? downloadDelegate,
                                                     Stream                    targetStream,
                                                     CancellationToken         token)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(4 << 10);
        try
        {
            CDNCacheResult     result       = await client.TryGetCachedStreamFrom(url, null, token);
            await using Stream resultStream = result.Stream;

            DownloadProgress progress = new DownloadProgress
            {
                BytesTotal      = resultStream.Length,
                BytesDownloaded = 0
            };

            Read:
            int read = await resultStream.ReadAsync(buffer, token);
            if (read > 0)
            {
                await targetStream.WriteAsync(buffer.AsMemory(0, read), token);
                progress.BytesDownloaded += read;
                downloadDelegate?.Invoke(read, progress);
                goto Read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    extension(Stream stream)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<T> ReadAsync<T>(CancellationToken token = default)
            where T : unmanaged
        {
            int    sizeOfData = Unsafe.SizeOf<T>();
            byte[] buffer     = ArrayPool<byte>.Shared.Rent(sizeOfData);

            try
            {
                await stream.ReadExactlyAsync(buffer.AsMemory(0, sizeOfData), cancellationToken: token)
                            .ConfigureAwait(false);
                return MemoryMarshal.Read<T>(buffer);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public async ValueTask<int> SeekForwardAsync(int               seekBytesForward,
                                                     CancellationToken token = default)
        {
            // Just change the position if stream is seekable.
            if (stream.CanSeek)
            {
                stream.Position += seekBytesForward;
                return seekBytesForward;
            }

            ArgumentOutOfRangeException.ThrowIfLessThan(seekBytesForward, 0); // Throw if bytes to seek is negative
            if (seekBytesForward == 0)
            {
                return 0;
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(Math.Min(seekBytesForward, 64 << 10));
            try
            {
                int skipped  = 0;
                int remained = seekBytesForward;

                while (remained > 0)
                {
                    int toRead = Math.Min(remained, buffer.Length);
                    int read = await stream.ReadAsync(buffer.AsMemory(0, toRead), token)
                                           .ConfigureAwait(false);
                    skipped  += read;
                    remained -= read;
                    if (read == 0)
                    {
                        break;
                    }
                }

                return skipped;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
