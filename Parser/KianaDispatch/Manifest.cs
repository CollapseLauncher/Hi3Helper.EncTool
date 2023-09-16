// Added since v6.9 (nice) changes
// :teri_copium:

using System.Text.Json.Serialization;

namespace Hi3Helper.EncTool.Parser.KianaDispatch
{
    // field: manifest
    public class ManifestBase
    {
        [JsonPropertyName("Audio")] public ManifestAudioBase ManifestAudio { get; set; }
    }

    // field: manifest -> Audio
    public class ManifestAudioBase
    {
        [JsonPropertyName("platform")] public ManifestAudioPlatform ManifestAudioPlatform { get; set; }
        [JsonPropertyName("revision")] public int ManifestAudioRevision { get; set; }
    }

    // field: manifest -> Audio -> yada-yadi-yada
    public class ManifestAudioPlatform
    {
        [JsonPropertyName("Windows")] public string ManifestWindows { get; set; }
        [JsonPropertyName("Android")] public string ManifestAndroid { get; set; }
        [JsonPropertyName("iOS")] public string ManifestIOS { get; set; }
    }
}
