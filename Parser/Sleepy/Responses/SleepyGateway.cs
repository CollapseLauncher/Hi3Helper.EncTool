using Hi3Helper.EncTool.Parser.Sleepy.JsonConverters;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hi3Helper.EncTool.Parser.Sleepy.Responses
{
    internal class SleepyGateway : SleepyReturnableCode
    {
        [JsonPropertyName("cdn_check_url")]
        public string CdnCheckUrl { get; init; }

        [JsonPropertyName("cdn_conf_ext")]
        public SleepyGatewayCdnConfig CdnConfig { get; init; }
    }

    internal class SleepyGatewayCdnConfig
    {
        [JsonPropertyName("design_data")]
        public SleepyGatewayDesignDataConfig DesignDataConfig { get; init; }

        [JsonPropertyName("game_res")]
        public SleepyGatewayGameResConfig GameResConfig { get; init; }

        [JsonPropertyName("silence_data")]
        public SleepyGatewaySilenceDataConfig SilenceDataConfig { get; init; }
    }

    internal class SleepyGatewayDesignDataConfig : SleepyGatewayMetadataConfig
    {
        [JsonPropertyName("data_revision")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int DataRevision { get; init; }
    }

    internal class SleepyGatewayGameResConfig : SleepyGatewayMetadataConfig
    {
        [JsonPropertyName("audio_revision")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int AudioRevision { get; init; }

        [JsonPropertyName("res_revision")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int ResRevision { get; init; }

        [JsonPropertyName("branch")]
        public string Branch { get; init; }
    }

    internal class SleepyGatewaySilenceDataConfig : SleepyGatewayMetadataConfig
    {
        [JsonPropertyName("silence_revision")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int SilenceRevision { get; init; }
    }

    internal class SleepyGatewayMetadataConfig
    {
        [JsonPropertyName("base_url")]
        public string BaseUrl { get; init; }

        [JsonPropertyName("md5_files")]
        [JsonConverter(typeof(StringToSleepyFileInfoListConverter))]
        public List<SleepyFileInfo> FileInfoList { get; init; }
    }

    public class SleepyFileInfo
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; init; }

        [JsonPropertyName("fileMD5")] // They called the field as "fileMD5" while in-fact, it's not even an MD5 hash. It's a XXH64 hash written to UInt64!!!!!
        [JsonConverter(typeof(NumberStringToXxh64HashBytesConverter))]
        public byte[] FileXxh64Hash { get; init; }
        public string FileXxh64HashString { get => Convert.ToHexStringLower(FileXxh64Hash); }

        [JsonPropertyName("fileSize")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long FileSize { get; init; }
    }
}
