using Hi3Helper.Data;
using System.Text.Json.Serialization;

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AudioLanguageType : int // TypeDefIndex: 33475
    {
        Common = 0,
        Chinese = 1,
        Japanese = 2
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AudioPCKType : int // TypeDefIndex: 33474
    {
        MustHave = 0,
        MapBtn = 1,
        DLC = 2,
        DLC2 = 3,
        All = 100
    }

    public class ManifestAssetInfo
    {
        public string Name { get; set; } // 0x10
        public string Path { get; set; } // 0x18
        public byte[] Hash { get; set; } // 0x20
        public string HashString { get => HexTool.BytesToHexUnsafe(Hash); }
        public int Size { get; set; } // 0x30
        public AudioLanguageType Language { get; set; } // 0x34
        public AudioPCKType PckType { get; set; } // 0x38
        public bool NeedMap { get; set; } // 0x3C
        public bool IsHasPatch { get => PatchInfo.HasValue; }
        public ManifestAudioPatchInfo? PatchInfo { get; private set; }

        public void AddPatchInfo(ManifestAudioPatchInfo? patchInfo)
        {
            PatchInfo = patchInfo;
        }
    }
}
