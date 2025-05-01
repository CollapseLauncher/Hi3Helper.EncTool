#nullable enable
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Proto.Genshin;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable IdentifierTypo
// ReSharper disable CheckNamespace
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

namespace Hi3Helper.EncTool.Parser.YSDispatchHelper
{
    [JsonSerializable(typeof(DispatchInfo))]
    [JsonSerializable(typeof(PkgVersionProperties))]
    internal partial class DispatchHelperContext : JsonSerializerContext;

    public sealed class DispatchHelper
    {
        private readonly CancellationToken _cancelToken;
        private readonly string _channelName = "OSRELWin";

        private          GenshinGateway? _gateway;
        private readonly HttpClient      _httpClient;
        private          QueryProperty?  _returnValProp;
        private readonly ILogger?        _logger;

        public DispatchHelper(HttpClient httpClient, int regionID, string dispatchKey, string dispatchURLPrefix,
                              string versionString = "2.6.0", CancellationToken cancelToken = default)
            : this(httpClient, regionID, dispatchKey, dispatchURLPrefix, versionString, null, cancelToken) { }

        public DispatchHelper(HttpClient httpClient, int regionID, string dispatchKey, string dispatchURLPrefix,
                              string versionString = "2.6.0", ILogger? logger = null, CancellationToken cancelToken = default)
        {
            if (regionID >= 4)
            {
                _channelName = "CNRELWin";
            }

            _httpClient     = httpClient;
            RegionSubdomain = GetSubdomainByRegionID(regionID);
            Version         = versionString;
            DispatchBaseURL = string.Format(
                                            dispatchURLPrefix,
                                            RegionSubdomain,
                                            $"{_channelName}{versionString}",
                                            dispatchKey);
            _cancelToken = cancelToken;
            _logger      = logger;
        }

        private string DispatchBaseURL { get; }
        private string RegionSubdomain { get; }
        private string Version { get; }

        public async Task<DispatchInfo?> LoadDispatchInfo()
        {
#if DEBUG
            // DEBUG ONLY: Show URL of Proto
            string dFormat = $"URL for Proto Response:\r\n{DispatchBaseURL}";
            _logger?.LogInformation(dFormat);
#endif

            return await _httpClient.GetFromJsonAsync(
                DispatchBaseURL,
                DispatchHelperContext.Default.DispatchInfo,
                _cancelToken);
        }

        public async Task LoadDispatch(byte[] customDispatchData)
        {
            _gateway = GenshinGateway.Parser!.ParseFrom(customDispatchData);
            _returnValProp = new QueryProperty
            {
                GameServerName = _gateway!.GatewayProperties!.ServerName,
                ClientGameResURL =
                    $"{_gateway.GatewayProperties.RepoResVersionURL}/output_{_gateway.GatewayProperties.RepoResVersionProperties!.ResVersionNumber}_{_gateway.GatewayProperties.RepoResVersionProperties.ResVersionHash}/client",
                ClientDesignDataURL =
                    $"{_gateway.GatewayProperties.RepoDesignDataURL}/output_{_gateway.GatewayProperties.RepoDesignDataNumber}_{_gateway.GatewayProperties.RepoDesignDataHash}/client/General",
                ClientDesignDataSilURL =
                    $"{_gateway.GatewayProperties.RepoDesignDataURL}/output_{_gateway.GatewayProperties.RepoDesignDataSilenceNumber}_{_gateway.GatewayProperties.RepoDesignDataSilenceHash}/client_silence/General",
                DataRevisionNum    = _gateway.GatewayProperties.RepoDesignDataNumber,
                SilenceRevisionNum = _gateway.GatewayProperties.RepoDesignDataSilenceNumber,
                ResRevisionNum     = _gateway.GatewayProperties.RepoResVersionProperties.ResVersionNumber,
                ChannelName        = _channelName,
                GameVersion        = Version
            };

            ParseGameResPkgProp(_returnValProp);
            ParseDesignDataURL(_returnValProp);
            await ParseAudioAssetsURL(_returnValProp);
        }

        private void ParseDesignDataURL(QueryProperty valProp)
        {
            string[] dataList = _gateway!.GatewayProperties!.RepoResVersionProperties!.ResVersionMapJSON!.Split("\r\n");
            valProp.ClientGameRes = new List<PkgVersionProperties?>();
            foreach (string data in dataList)
            {
                (valProp.ClientGameRes as List<PkgVersionProperties?>)?
                   .Add(JsonSerializer.Deserialize(data, DispatchHelperContext.Default.PkgVersionProperties));
            }
        }

        private void ParseGameResPkgProp(QueryProperty valProp)
        {
            var jsonDesignData    = _gateway!.GatewayProperties!.RepoDesignDataJSON;
            var jsonDesignDataSil = _gateway!.GatewayProperties!.RepoDesignDataSilenceJSON;
#if DEBUG
            _logger?.LogDebug($"[GenshinDispatchHelper::ParseGameResPkgProp] DesignData Response:" +
                              $"\r\n\tDesignData:\r\n{jsonDesignData}" +
                              $"\r\n\tDesignData_Silence:\r\n{jsonDesignDataSil}");
#endif

            if (!string.IsNullOrEmpty(jsonDesignData))
            {
                string[] designDataArr = jsonDesignData.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

                foreach (string designData in designDataArr)
                {
                    var designDataSer =
                        JsonSerializer.Deserialize(designData, DispatchHelperContext.Default.PkgVersionProperties);
                    // Only serialize data_versions
                    if (designDataSer is { remoteName: "data_versions" })
                    {
                        valProp.ClientDesignData = designDataSer;
                    }

                    if (designDataSer == null)
                    {
                        _logger?.LogWarning("[GenshinDispatchHelper::ParseGameResPkgProp] DesignData is null!");
                    }
                }
            }

            if (jsonDesignDataSil != null)
            {
                valProp.ClientDesignDataSil =
                    JsonSerializer.Deserialize(jsonDesignDataSil, DispatchHelperContext.Default.PkgVersionProperties);
            }
            else
            {
                _logger?.LogWarning("[GenshinDispatchHelper::ParseGameResPkgProp] DesignData_Silence is null!");
            }
        }

        private async Task ParseAudioAssetsURL(QueryProperty valProp)
        {
            ArgumentException.ThrowIfNullOrEmpty(valProp.ClientGameResURL, nameof(valProp.ClientGameResURL));
            
            byte[] byteData = await _httpClient
               .GetByteArrayAsync(
                                  valProp.ClientGameResURL.CombineURLFromString("/StandaloneWindows64/base_revision"),
                                  _cancelToken);
            string[] responseData = Encoding.UTF8.GetString(byteData).Split(' ');

            valProp.ClientAudioAssetsURL =
                $"{_gateway!.GatewayProperties!.RepoResVersionURL}/output_{responseData[0]}_{responseData[1]}/client";
            valProp.AudioRevisionNum = uint.Parse(responseData[0]);
        }

        public QueryProperty? GetResult()
        {
            return _returnValProp;
        }

        private static string GetSubdomainByRegionID(int regionID)
        {
            return regionID switch
            {
                /*
                 * Region ID:
                 * 0 = USA
                 * 1 = Europe
                 * 2 = Asia
                 * 3 = TW/HK/MO
                 * 4 = Mainland China
                 * 5 = Mainland China (Bilibili)
                 */
                0 => "osusadispatch",
                1 => "oseurodispatch",
                2 => "osasiadispatch",
                3 => "oschtdispatch",
                4 => "cngfdispatch",
                5 => "cnqddispatch",
                _ => throw new FormatException("Unknown region ID!")
            };
        }
    }
}