using System.Text.Json.Serialization;

namespace Hi3Helper.EncTool.Parser.AssetIndex
{
    [JsonConverter(typeof(JsonStringEnumConverter<CompressionFlag>))]
    public enum CompressionFlag : byte
    {
        None = 0,
        Deflate = 1,
        GZip = 2,
        Brotli = 3
    }
}
