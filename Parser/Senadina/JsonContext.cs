using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable IdentifierTypo

namespace Hi3Helper.EncTool.Parser.Senadina
{
    [JsonSerializable(typeof(Dictionary<string, SenadinaFileIdentifier>))]
    public partial class SenadinaJsonContext : JsonSerializerContext;
}
