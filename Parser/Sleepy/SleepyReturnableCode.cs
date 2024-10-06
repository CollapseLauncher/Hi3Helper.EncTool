using System.Text.Json.Serialization;

namespace Hi3Helper.EncTool.Parser.Sleepy
{
    internal class SleepyReturnableCode
    {
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        [JsonPropertyName("retcode")] public int ReturnCode { get; init; } = short.MinValue;
    }
}
