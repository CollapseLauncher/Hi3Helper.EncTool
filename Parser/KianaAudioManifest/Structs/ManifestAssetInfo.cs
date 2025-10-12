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
        Chinese,
        Japanese
    }

    public enum AudioPCKType
    {
        MustHave = 0,
        Activity,
        DLC,
        DLC2,
        MainLine10_12,
        MainLine13_14,
        MainLine15_17,
        MainLine18_19,
        MainLine20_22,
        MainLine23_25,
        GodWar1,
        MainLine26_28,
        GodWar3,
        MainLine29_31,
        MainLine32_35,
        MainLine36_39,
        All = 100
    }

#nullable enable
    public class ManifestAssetInfo
    {
        public required string Name { get; set; } // 0x10
        public required string Path { get; set; } // 0x18
        public required byte[] Hash { get; set; } // 0x20
        public string HashString { get => HexTool.BytesToHexUnsafe(Hash) ?? ""; }
        public int Size { get; set; } // 0x30
        public AudioLanguageType Language { get; set; } // 0x34
        public AudioPCKType PckType { get; set; } // 0x38
        public bool NeedMap { get; set; } // 0x3C
        public bool IsHasPatch { get => PatchInfo != null; }
        public ManifestAudioPatchInfo? PatchInfo { get; private set; }

        public void AddPatchInfo(ManifestAudioPatchInfo? patchInfo)
        {
            PatchInfo = patchInfo;
        }
    }
}
