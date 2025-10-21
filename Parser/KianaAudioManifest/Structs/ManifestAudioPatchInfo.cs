using Hi3Helper.Data;
using System.Runtime.CompilerServices;
using System;

// ReSharper disable once CheckNamespace
namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    public class ManifestAudioPatchInfo(string name, string fileMD5, string newFileMD5, string patchMD5, uint patchSize)
    {
        private static unsafe byte[] DecodeNumberAsBytes(string source)
        {
            if (!ulong.TryParse(source, out ulong result))
            {
                return HexTool.HexToBytesUnsafe(source);
            }

            void*              ptr       = Unsafe.AsPointer(ref Unsafe.AsRef(ref result));
            ReadOnlySpan<byte> bytes     = new ReadOnlySpan<byte>(ptr, sizeof(ulong));
            byte[]             bytesTemp = new byte[bytes.Length];
            bytes.CopyTo(bytesTemp);
            bytesTemp.Reverse();
            return bytesTemp;
        }

        public string AudioFilename    { get; } = name;
        public string PatchFilename    { get; } = $"{patchMD5}.pck";
        public byte[] OldAudioMD5Array { get; } = DecodeNumberAsBytes(fileMD5);
        public byte[] NewAudioMD5Array { get; } = DecodeNumberAsBytes(newFileMD5);
        public byte[] PatchMD5Array    { get; } = DecodeNumberAsBytes(patchMD5);
        public uint   PatchFileSize    { get; } = patchSize; // 0x30
    }
}
