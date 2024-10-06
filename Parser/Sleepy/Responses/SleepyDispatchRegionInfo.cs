using System.Text.Json.Serialization;

namespace Hi3Helper.EncTool.Parser.Sleepy.Responses
{
    internal class SleepyDispatchRegionInfo : SleepyReturnableCode
    {
        [JsonPropertyName("dispatch_url")] public string GatewayUrl { get; init; }
        [JsonPropertyName("name")] public string GatewayName { get; init; }
        [JsonPropertyName("is_recommend")] public bool IsAccessible { get; init; }
    }
}
