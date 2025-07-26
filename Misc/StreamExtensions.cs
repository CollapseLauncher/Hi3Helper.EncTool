using Hi3Helper.Http;
using System;
using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
// ReSharper disable once CheckNamespace
namespace Hi3Helper.EncTool;

internal static class StreamExtensions
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
            CDNCacheUtil.CDNCacheResult result       = await client.TryGetCachedStreamFrom(url, null, token);
            await using Stream          resultStream = result.Stream;

            int size = (int)resultStream.Length;
            DownloadProgress progress = new()
            {
                BytesDownloaded = 0,
                BytesTotal      = size
            };

            int read;
            while ((read = await resultStream.ReadAsync(buffer, token)) > 0)
            {
                await targetStream.WriteAsync(buffer.AsMemory(0, read), token);
                progress.BytesDownloaded += read;
                downloadDelegate?.Invoke(read, progress);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
