using Hi3Helper.Data;

namespace Hi3Helper.EncTool.KianaManifest
{
    public enum AssetLanguage : int
    {
        Common = 0,
        Chinese = 1,
        Japanese = 2
    }

    public class ManifestAssetInfo
    {
        public string Name { get; set; } // 0x10
        public string Path { get; set; } // 0x18
        public byte[] Hash { get; set; } // 0x20
        public string HashString { get => HexTool.BytesToHexUnsafe(Hash); }
        public int Size { get; set; } // 0x30
        public AssetLanguage Language { get; set; } // 0x34
        public int PckType { get; set; } // 0x38
        public bool NeedMap { get; set; } // 0x3C
        public bool IsHasPatch { get => PatchInfo.HasValue; }
        public ManifestAudioPatchInfo? PatchInfo { get; private set; }

        public void AddPatchInfo(ManifestAudioPatchInfo? patchInfo)
        {
            PatchInfo = patchInfo;
        }
    }
}
