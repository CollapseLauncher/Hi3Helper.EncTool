using Hi3Helper.EncTool.Parser.Sleepy.Responses;
using System.Text.Json.Serialization;
// ReSharper disable PartialTypeWithSinglePart

namespace Hi3Helper.EncTool.Parser.Sleepy
{
    [JsonSerializable(typeof(SleepyDispatch))]
    [JsonSerializable(typeof(SleepyFileInfo))]
    [JsonSerializable(typeof(SleepyGateway))]
    [JsonSerializable(typeof(SleepyGatewayRegionContent))]
    internal partial class SleepyContext : JsonSerializerContext
    {
    }
}
