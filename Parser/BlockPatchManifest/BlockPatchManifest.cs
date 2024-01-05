using Hi3Helper.Data;
using Hi3Helper.UABT.Binary;
using System;
using System.Collections.Generic;
using System.IO;

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    public struct BlockOldPatchInfo
    {
        private byte[] _oldHash;
        private byte[] _patchHash;
        public byte[] OldHash { get => _oldHash == null ? _oldHash = HexTool.HexToBytesUnsafe(OldHashStr) : _oldHash; }
        public string OldHashStr { get; set; }
        public byte[] PatchHash { get => _patchHash == null ? _patchHash = HexTool.HexToBytesUnsafe(PatchHashStr) : _patchHash; }
        public string PatchHashStr { get; set; }
        public int[] OldVersion { get; set; }

        public uint PatchSize { get; set; }
        public string OldVersionDir { get => $"{string.Join('_', OldVersion)}"; }

        public BlockOldPatchInfo Copy() => new BlockOldPatchInfo
        {
            _oldHash = _oldHash,
            _patchHash = _patchHash,
            OldHashStr = OldHashStr,
            PatchHashStr = PatchHashStr,
            OldVersion = OldVersion,
            PatchSize = PatchSize
        };
    }

    public struct BlockPatchInfo
    {
        private byte[] _newHash;
        public byte[] NewHash { get => _newHash == null ? _newHash = HexTool.HexToBytesUnsafe(NewHashStr) : _newHash; }
        public string NewHashStr { get; set; }
        public List<BlockOldPatchInfo> PatchPairs { get; set; }

        public string NewBlockName { get => HexTool.BytesToHexUnsafe(NewHash); }
    }

    public class BlockPatchManifest
    {

        public Dictionary<string, int> NewBlockCatalog = new Dictionary<string, int>();
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
            // Initialize the stream into the endian reader
            EndianBinaryReader reader = new EndianBinaryReader(stream);

            // Start deserializing
            PatchAsset = DeserializeManifest(reader);
        }

        private List<BlockPatchInfo> DeserializeManifest(EndianBinaryReader reader)
        {
            // Initialize return list
            List<BlockPatchInfo> ret = new List<BlockPatchInfo>();

            // Get the patch count
            int patchCount = (int)reader.ReadUInt32();

            // Clear the reference directory
            NewBlockCatalog.Clear();

            // Do loop to read patch information
            for (int i = 0; i < patchCount; i++)
            {
                // Get the props
                string OldHash = TrimBlockName(reader.ReadString());
                string NewHash = TrimBlockName(reader.ReadString());
                string PatchHash = TrimBlockName(reader.ReadString());
                int[] Version = TrimVersionStringToArray(reader.ReadString());
                uint PatchSize = reader.ReadUInt32();

                // Check if the patch info reference already exist. If not, then
                // add it as a new one.
                bool isKeyExist = NewBlockCatalog.ContainsKey(NewHash);
                if (!isKeyExist)
                {
                    // Initialize the patch info
                    BlockPatchInfo info = new BlockPatchInfo
                    {
                        NewHashStr = NewHash,
                        PatchPairs = new List<BlockOldPatchInfo>
                        {
                            // Also initialize the old patch info pairs
                            new BlockOldPatchInfo
                            {
                                OldHashStr = OldHash,
                                PatchHashStr = PatchHash,
                                OldVersion = Version,
                                PatchSize = PatchSize
                            }
                        }
                    };

                    // Add the index references and patch info
                    NewBlockCatalog.Add(NewHash, ret.Count);
                    ret.Add(info);
                }
                // Otherwise, try to add the old reference
                else
                {
                    // Get the index of the patch info
                    int index = NewBlockCatalog[NewHash];

                    // Get the reference and add another old patch info pairs
                    BlockPatchInfo info = ret[index];
                    info.PatchPairs.Add(new BlockOldPatchInfo
                    {
                        OldHashStr = OldHash,
                        PatchHashStr = PatchHash,
                        OldVersion = Version,
                        PatchSize = PatchSize
                    });
                }
            }

            // Return the value
            return ret;
        }

        private string TrimBlockName(ReadOnlySpan<char> name)
        {
            // Trim the filename without extension
            ReadOnlySpan<char> split = Path.GetFileNameWithoutExtension(name);

            // Return the trimmed filename
            return new string(split);
        }

        private int[] TrimVersionStringToArray(string version)
        {
            // Initialize return value
            int[] ret = new int[4];
            int[] versionArrayNum = new int[4];

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

                // Assign the number to return value
                ret[i] = value;
                versionArrayNum[i] = value;
            }

            // Return the value
            return ret;
        }
    }
}
