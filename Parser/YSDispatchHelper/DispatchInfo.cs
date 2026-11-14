using System.Text.Json.Serialization;

#nullable enable
namespace Hi3Helper.EncTool.Parser.YSDispatchHelper
{
    public class DispatchInfo
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
