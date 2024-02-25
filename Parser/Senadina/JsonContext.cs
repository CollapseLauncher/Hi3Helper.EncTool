using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hi3Helper.EncTool.Parser.Senadina
{
    [JsonSerializable(typeof(Dictionary<string, SenadinaFileIdentifier>))]
    public partial class SenadinaJSONContext : JsonSerializerContext { }
}
