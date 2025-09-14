using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.Sleepy.Responses;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace Hi3Helper.EncTool.Parser.Sleepy
{
    public enum FileInfoKind
    {
        Res,
        Base,
        Silence,
        Data,
        Audio
    }

    public class SleepyInfo
    {
        private SleepyProperty Property        { get; }
        private RSA            RsaInstance     { get; }
        private HttpClient     Client          { get; }
        private SleepyGateway? ResponseGateway { get; set; }

        private SleepyInfo(HttpClient httpClient, RSA rsaInstance, SleepyProperty property)
        {
            Client      = httpClient;
            Property    = property;
            RsaInstance = rsaInstance;
        }

        public static SleepyInfo CreateSleepyInfo(HttpClient httpClient, RSA rsaInstance, SleepyProperty property) =>
            new SleepyInfo(httpClient, rsaInstance, property);

        public async Task Initialize(CancellationToken token = default)
        {
            // Get the primary gateway name
            SleepyDispatchRegionInfo? regionInfo = await GetRegionInfoFromGatewayName(Property.GatewayName, token);
            // Re-assign and get the fallback if regionInfo is still null
            regionInfo ??= await GetRegionInfoFromGatewayName(Property.GatewayNameFallback, token);

            // If the region info is still null, throw
            if (regionInfo == null)
                throw new NullReferenceException($"Content does not contain region: {Property.GatewayName}");

            // Get the gateway region URL
            Uri gatewayRegionUrl =
                CreateGatewayRegionUri(regionInfo,
                                       out Dictionary<string, string>
                                           gatewayRegionUrlQueries); // TODO: Add queries to the request header
            SleepyGatewayRegionContent? gatewayRegionContentResponse =
                await GetJsonFromUrl(gatewayRegionUrl,                                 gatewayRegionUrlQueries,
                                     SleepyContext.Default.SleepyGatewayRegionContent, token);

            if (gatewayRegionContentResponse == null || gatewayRegionContentResponse.Content.Length < 32)
                throw new NullReferenceException("Gateway content is empty!");

            ResponseGateway = ParseGatewayFromGatewayContent(gatewayRegionContentResponse).ThrowIfUnsuccessful();

            if (ResponseGateway == null)
                throw new NullReferenceException("Gateway response is empty!");

            (string BaseUrl, SleepyFileInfo ReferenceFileInfo, string? RevisionStamp) baseFileInfo = GetFileInfo(FileInfoKind.Base);
            string baseFileUrl =
                baseFileInfo.BaseUrl.CombineURLFromString(baseFileInfo.ReferenceFileInfo.FileName);

            string baseFileRevision = await Client.GetFromCachedStringAsync(baseFileUrl, HttpMethod.Get, token);
            ResponseGateway.CdnConfig.GameResConfig.BaseRevision = baseFileRevision;
        }

        private async Task<SleepyDispatchRegionInfo?> GetRegionInfoFromGatewayName(string gatewayName, CancellationToken cancellationToken)
        {
            // TODO: Add queries to the request header
            Uri dispatchUrl = CreateDispatchUri(out Dictionary<string, string> dispatchUrlQueries);
            SleepyDispatch? dispatcher = (await GetJsonFromUrl(
                                                               dispatchUrl,
                                                               dispatchUrlQueries,
                                                               SleepyContext.Default.SleepyDispatch,
                                                               cancellationToken)).ThrowIfUnsuccessful();

            // Find the region info and return whether it's found or not (null)
            SleepyDispatchRegionInfo? regionInfo = dispatcher?
                                                  .RegionList
                                                  .FirstOrDefault(x => x.GatewayName.Equals(gatewayName, StringComparison.OrdinalIgnoreCase));

            // Return the region info
            return regionInfo;
        }

        private SleepyGateway? ParseGatewayFromGatewayContent(SleepyGatewayRegionContent? gatewayResponse)
        {
            if (gatewayResponse == null)
            {
                return null;
            }

            byte[] gatewayResponseOutBuff = ArrayPool<byte>.Shared.Rent(gatewayResponse.Content.Length);
            try
            {
                int keyToReadLen       = RsaInstance.KeySize >> 3; // Should expect 128 bytes
                int offset             = 0;
                int offsetDec          = 0;
                int gatewayResponseLen = gatewayResponse.Content.Length;
                while (offset < gatewayResponseLen)
                {
                    int toRead = Math.Min(gatewayResponseLen - offset, keyToReadLen);
                    if (!RsaInstance.TryDecrypt(
                        gatewayResponse.Content.AsSpan(offset, toRead),
                        gatewayResponseOutBuff.AsSpan(offsetDec),
                        RSAEncryptionPadding.Pkcs1,
                        out int outDecWritten))
                    {
                        throw new InvalidOperationException("Cannot read gateway response!");
                    }

                    offset += toRead;
                    offsetDec += outDecWritten;
                }

                return JsonSerializer.Deserialize(gatewayResponseOutBuff.AsSpan(0, offsetDec), SleepyContext.Default.SleepyGateway);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(gatewayResponseOutBuff);
            }
        }

        private Uri CreateGatewayRegionUri(SleepyDispatchRegionInfo regionInfo, out Dictionary<string, string> urlQueries)
        {
            string formattedQuery = string.Format(Property.GatewayQuery,
                Property.ChannelString,
                Property.VersionString,
                Property.SeedString);

            string fullUrl = regionInfo.GatewayUrl.CombineURLFromString(formattedQuery);
            if (!Uri.TryCreate(fullUrl, UriKind.RelativeOrAbsolute, out Uri? result))
            {
                throw new InvalidDataException($"Gateway url: {fullUrl} is not a valid Url!");
            }

            ParseQueryToDictionary(formattedQuery, out urlQueries);

            return result;
        }

        private Uri CreateDispatchUri(out Dictionary<string, string> urlQueries)
        {
            string formattedQuery = string.Format(Property.DispatchQuery, Property.ChannelString, Property.VersionString);

            string fullUrl = Property.DispatchUrl.CombineURLFromString(formattedQuery);
            if (!Uri.TryCreate(fullUrl, UriKind.RelativeOrAbsolute, out Uri? result))
            {
                throw new InvalidDataException($"Dispatch url: {fullUrl} is not a valid Url!");
            }

            ParseQueryToDictionary(formattedQuery, out urlQueries);

            return result;
        }

        private static void ParseQueryToDictionary(string query, out Dictionary<string, string> urlQueries)
        {
            ReadOnlySpan<char> querySpan = query.AsSpan().TrimStart('?');

            urlQueries = new Dictionary<string, string>();
            Span<Range> currentQueryRange = stackalloc Range[2];
            foreach (Range queryRange in querySpan.Split('&'))
            {
                ReadOnlySpan<char> querySpanCurrent = querySpan[queryRange];
                int currentQuerySplitLen = querySpanCurrent.Split(currentQueryRange, '=');
                if (currentQuerySplitLen != 2)
                {
                    throw new InvalidDataException($"Dispatch query: {querySpanCurrent.ToString()} is malformed!");
                }

                string currentQueryKey   = querySpanCurrent[currentQueryRange[0]].ToString();
                string currentQueryValue = querySpanCurrent[currentQueryRange[1]].ToString();

                urlQueries.Add(currentQueryKey, currentQueryValue);
            }
        }

        private async Task<T?> GetJsonFromUrl<T>(Uri url, Dictionary<string, string> httpHeader, JsonTypeInfo<T?> typeInfo, CancellationToken token = default)
            where T : class
        {
            using HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
            foreach (KeyValuePair<string, string> headerKvp in httpHeader)
            {
                requestMessage.Headers.Add(headerKvp.Key, headerKvp.Value);
            }

            return await Client.GetFromCachedJsonAsync(requestMessage, typeInfo, token);
        }

        public (string BaseUrl, SleepyFileInfo ReferenceFileInfo, string? RevisionStamp) GetFileInfo(FileInfoKind kind)
        {
            SleepyGatewayMetadataConfig? metadataConfig = kind switch
            {
                FileInfoKind.Res or FileInfoKind.Audio or FileInfoKind.Base => ResponseGateway?.CdnConfig.GameResConfig,
                FileInfoKind.Data => ResponseGateway?.CdnConfig.DesignDataConfig,
                FileInfoKind.Silence => ResponseGateway?.CdnConfig.SilenceDataConfig,
                _ => throw new NotImplementedException($"FileInfoKind.{kind} is not supported!")
            };
            
            ArgumentException.ThrowIfNullOrEmpty(metadataConfig?.BaseUrl, nameof(metadataConfig.BaseUrl));

            string findKeyName = $"{kind}_";
            string baseUrl = metadataConfig.BaseUrl!.CombineURLFromString(Property.BuildProperty.BuildIdentity,
                                                                          Property.BuildProperty.BuildArea
                                                                         );

            SleepyFileInfo? fileInfo = metadataConfig.FileInfoList.FirstOrDefault(x => x.FileName.StartsWith(findKeyName, StringComparison.OrdinalIgnoreCase));
            if (fileInfo == null)
                throw new KeyNotFoundException("File information is not found inside of the gateway response!");

            string? revisionStamp = kind switch
            {
                FileInfoKind.Res => $"{ResponseGateway?.CdnConfig.GameResConfig.ResRevision}",
                FileInfoKind.Base => ResponseGateway?.CdnConfig.GameResConfig.BaseRevision,
                FileInfoKind.Silence => $"{ResponseGateway?.CdnConfig.SilenceDataConfig.SilenceRevision}",
                FileInfoKind.Data => $"{ResponseGateway?.CdnConfig.DesignDataConfig.DataRevision}",
                FileInfoKind.Audio => $"{ResponseGateway?.CdnConfig.GameResConfig.AudioRevision}",
                _ => throw new NotImplementedException($"FileInfoKind.{kind} is not supported!")
            };

            return (baseUrl, fileInfo, revisionStamp);
        }

        public SleepyFileInfoResult GetFileInfoResult(FileInfoKind kind)
        {
            (string BaseUrl, SleepyFileInfo ReferenceFileInfo, string? RevisionStamp) result = GetFileInfo(kind);
            return new SleepyFileInfoResult(result.BaseUrl, result.ReferenceFileInfo, result.RevisionStamp);
        }
    }
}
