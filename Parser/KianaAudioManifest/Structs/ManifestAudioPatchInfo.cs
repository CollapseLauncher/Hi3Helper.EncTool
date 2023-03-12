using Hi3Helper.Data;

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    public struct ManifestAudioPatchInfo
    {
        private readonly string _OldAudioFileMD5; // 0x18
        private readonly string _NewAudioFileMD5; // 0x20
        private readonly string _PatchFileMD5; // 0x28

        public ManifestAudioPatchInfo(string name, string fileMD5, string newFileMD5, string patchMD5, uint patchSize)
        {
            AudioFilename = name;
            _OldAudioFileMD5 = fileMD5;
            _NewAudioFileMD5 = newFileMD5;
            _PatchFileMD5 = patchMD5;
            PatchFileSize = patchSize;
        }

        public string AudioFilename { get; private set; }
        public string PatchFilename { get => $"{_PatchFileMD5}.patch"; }
        public byte[] OldAudioMD5Array { get => HexTool.HexToBytesUnsafe(_OldAudioFileMD5); }
        public byte[] NewAudioMD5Array { get => HexTool.HexToBytesUnsafe(_NewAudioFileMD5); }
        public byte[] PatchMD5Array { get => HexTool.HexToBytesUnsafe(_PatchFileMD5); }
        public uint PatchFileSize { get; private set; } // 0x30
    }
}
