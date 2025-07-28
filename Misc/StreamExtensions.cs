using Hi3Helper.Http;
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
            CDNCacheResult     result       = await client.TryGetCachedStreamFrom(url, null, token);
            await using Stream resultStream = result.Stream;

            await using CopyToStream copyToStream = new CopyToStream(resultStream, targetStream, downloadDelegate, false);

            Read:
            int read = await copyToStream.ReadAsync(buffer, token);
            if (read > 0)
                goto Read;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
