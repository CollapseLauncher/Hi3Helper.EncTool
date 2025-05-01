using Hi3Helper.Data;
using System.Runtime.CompilerServices;
using System;

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    public class ManifestAudioPatchInfo(string name, string fileMD5, string newFileMD5, string patchMD5, uint patchSize)
    {
        private static unsafe string NumAsHex(string source)
        {
            if (!ulong.TryParse(source, out ulong result))
            {
                return source;
            }

            void*              ptr       = Unsafe.AsPointer(ref Unsafe.AsRef(ref result));
            ReadOnlySpan<byte> bytes     = new(ptr, sizeof(ulong));
            Span<byte>         bytesTemp = stackalloc byte[bytes.Length];
            bytes.CopyTo(bytesTemp);
            bytesTemp.Reverse();
            return HexTool.BytesToHexUnsafe(bytesTemp);
        }

        public string AudioFilename    { get; private set; } = name;
        public string PatchFilename    { get => $"{patchMD5}.patch"; }
        public byte[] OldAudioMD5Array { get => HexTool.HexToBytesUnsafe(NumAsHex(fileMD5)); }
        public byte[] NewAudioMD5Array { get => HexTool.HexToBytesUnsafe(NumAsHex(newFileMD5)); }
        public byte[] PatchMD5Array    { get => HexTool.HexToBytesUnsafe(NumAsHex(patchMD5)); }
        public uint   PatchFileSize    { get; private set; } = patchSize; // 0x30
    }
}
