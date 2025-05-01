#nullable enable
using System.Text.Json.Serialization;

namespace Hi3Helper.EncTool.Parser.YSDispatchHelper
{
    public class DispatchInfo
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
