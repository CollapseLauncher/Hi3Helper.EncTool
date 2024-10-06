using System.Text.Json.Serialization;

namespace Hi3Helper.EncTool.Parser.Sleepy.Responses
{
    internal class SleepyGatewayRegionContent
    {
        [JsonPropertyName("content")] public byte[] Content { get; init; }
    }
}
