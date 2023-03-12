using Hi3Helper.Data;
using Hi3Helper.UABT.Binary;
using System;
using System.Collections.Generic;
using System.IO;

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    public struct BlockPatchInfo
    {
        public byte[] OldHash { get; set; }
        public byte[] NewHash { get; set; }
        public byte[] PatchHash { get; set; }
        public int[] Version { get; set; }
        public long PatchSize { get; set; }

        public string OldBlockName { get => HexTool.BytesToHexUnsafe(OldHash); }
        public string NewBlockName { get => HexTool.BytesToHexUnsafe(NewHash); }
        public string PatchName { get => HexTool.BytesToHexUnsafe(PatchHash); }
        public string VersionDir { get => $"{string.Join('_', Version)}"; }
    }

    public class BlockPatchManifest
    {
        private int[] _localGameVersion { get; set; }

        public Dictionary<string, int> OldBlockCatalog { get; private set; }
        public Dictionary<string, int> NewBlockCatalog { get; private set; }
        public Dictionary<string, int> PatchCatalog { get; private set; }
        public List<BlockPatchInfo> PatchAsset { get; private set; }

        public BlockPatchManifest(string filePath, int[] gameVersion)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Initialize(fs, gameVersion);
            }
        }

        public BlockPatchManifest(Stream stream, int[] gameVersion, bool disposeStream = false)
        {
            Initialize(stream, gameVersion);

            if (disposeStream)
            {
                stream.Dispose();
            }
        }

        private void Initialize(Stream stream, int[] gameVersion)
        {
            // Assign the game version
            _localGameVersion = gameVersion;

            // Initialize the stream into the endian reader
            EndianBinaryReader reader = new EndianBinaryReader(stream);

            // Start deserializing
            PatchAsset = DeserializeManifest(reader);
        }

        private List<BlockPatchInfo> DeserializeManifest(EndianBinaryReader reader)
        {
            // Initialize return list
            List<BlockPatchInfo> ret = new List<BlockPatchInfo>();
            OldBlockCatalog = new Dictionary<string, int>();
            NewBlockCatalog = new Dictionary<string, int>();
            PatchCatalog = new Dictionary<string, int>();

            // Get the patch count
            uint patchCount = reader.ReadUInt32();

            // Do loop to read patch information
            for (uint i = 0; i < patchCount; i++)
            {
                BlockPatchInfo info = ReadPatchInfo(reader);
                ret.Add(info);

                // Add the patch hash into catalog
                OldBlockCatalog.Add(info.OldBlockName, (int)i);
                NewBlockCatalog.Add(info.NewBlockName, (int)i);
                PatchCatalog.Add(info.PatchName, (int)i);
            }

            // Return the value
            return ret;
        }

        private BlockPatchInfo ReadPatchInfo(EndianBinaryReader reader)
        {
            // Initialize return value
            BlockPatchInfo blockPatchInfo = new BlockPatchInfo();

            // Read the values
            blockPatchInfo.OldHash = TrimBlockNameToHash(reader.ReadString());
            blockPatchInfo.NewHash = TrimBlockNameToHash(reader.ReadString());
            blockPatchInfo.PatchHash = TrimBlockNameToHash(reader.ReadString());
            blockPatchInfo.Version = TrimVersionStringToArray(reader.ReadString());
            blockPatchInfo.PatchSize = reader.ReadInt32();

            // Return the value
            return blockPatchInfo;
        }

        private byte[] TrimBlockNameToHash(string name)
        {
            // Trim the filename without extension
            string trimmed = Path.GetFileNameWithoutExtension(name);

            // Return the trimmed filename as bytes array
            return HexTool.HexToBytesUnsafe(trimmed);
        }

        private int[] TrimVersionStringToArray(string version)
        {
            // Initialize return value
            int[] ret = new int[4];

#nullable enable
            // Try split the version into array
            string[]? versionArray = version.Split('_');

            // If the version array is null or the length is invalid, then throw
            if (versionArray == null || versionArray.Length != 4)
            {
                throw new FormatException($"Version string is invalid! Got: {version} as the value");
            }
#nullable disable

            // Do loop to assign return value from string array
            for (int i = 0; i < ret.Length; i++)
            {
                // Try parse
                bool isValid = int.TryParse(versionArray[i], out int value);

                // If not valid, then throw
                if (!isValid)
                {
                    throw new FormatException($"Version string is not a valid integer value! Got: {versionArray[i]} as the value from: {version}");
                }

                // Check if the version matches
                if (value != _localGameVersion[i])
                {
                    throw new FormatException($"Format of the block patch manifest doesn't match with the version of the game! Manifest: {string.Join('.', versionArray)} Game Version: {string.Join('.', _localGameVersion)}");
                }

                // Assign the number to return value
                ret[i] = value;
            }

            // Return the value
            return ret;
        }
    }
}
