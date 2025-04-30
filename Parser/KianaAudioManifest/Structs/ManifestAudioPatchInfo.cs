using Hi3Helper.Data;
using System.Runtime.CompilerServices;
using System;

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    public class ManifestAudioPatchInfo
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

        private static unsafe string NumAsHex(string source)
        {
            if (ulong.TryParse(source, out ulong result))
            {
                void* ptr = Unsafe.AsPointer(ref Unsafe.AsRef(ref result));
                ReadOnlySpan<byte> bytes = new(ptr, sizeof(ulong));
                Span<byte> bytesTemp = stackalloc byte[bytes.Length];
                bytes.CopyTo(bytesTemp);
                bytesTemp.Reverse();
                return HexTool.BytesToHexUnsafe(bytesTemp);
            }

            return source;
        }

        public string AudioFilename { get; private set; }
        public string PatchFilename { get => $"{_PatchFileMD5}.patch"; }
        public byte[] OldAudioMD5Array { get => HexTool.HexToBytesUnsafe(NumAsHex(_OldAudioFileMD5)); }
        public byte[] NewAudioMD5Array { get => HexTool.HexToBytesUnsafe(NumAsHex(_NewAudioFileMD5)); }
        public byte[] PatchMD5Array { get => HexTool.HexToBytesUnsafe(NumAsHex(_PatchFileMD5)); }
        public uint PatchFileSize { get; private set; } // 0x30
    }
}
