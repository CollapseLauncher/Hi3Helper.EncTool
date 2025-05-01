using System.Text.Json.Serialization;
// ReSharper disable StringLiteralTypo

namespace Hi3Helper.EncTool.Parser.Sleepy
{
    internal class SleepyReturnableCode
    {
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        [JsonPropertyName("retcode")] public int ReturnCode { get; init; } = short.MinValue;
    }
}
