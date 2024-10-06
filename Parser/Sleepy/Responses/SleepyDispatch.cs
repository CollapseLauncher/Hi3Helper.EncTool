using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hi3Helper.EncTool.Parser.Sleepy.Responses
{
    internal class SleepyDispatch : SleepyReturnableCode
    {
        [JsonPropertyName("region_list")] public List<SleepyDispatchRegionInfo> RegionList { get; init; }
    }
}
