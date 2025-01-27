using Hi3Helper.Data;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable CheckNamespace

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    [JsonConverter(typeof(JsonStringEnumConverter<AudioLanguageType>))]
    public enum AudioLanguageType // TypeDefIndex: 33475
    {
        Common = 0,
        Chinese = 1,
        Japanese = 2
    }

    public enum AudioPCKType // TypeDefIndex: 33474
    {
        MustHave = 0,       // 0
        MapBtn,             // 1
        DLC,                // 2
        DLC2,               // 3
        MainLine10_12,      // 4
        MainLine13_14,      // 5
        MainLine15_17,      // 6
        MainLine18_19,      // 7
        MainLine20_22,      // 8
        MainLine23_25,      // 9
        GodWar1,            // 10
        GodWar2,            // 11
        GodWar3,            // 12
        MainLine29_31,      // 13
        MainLine32_35,      // 14
        MainLine36_39,      // 15
        All = 100           // 100
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
