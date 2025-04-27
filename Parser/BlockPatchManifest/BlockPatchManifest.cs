using Hi3Helper.Data;
using Hi3Helper.UABT.Binary;
using System;
using System.Collections.Generic;
using System.IO;

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    public class BlockOldPatchInfo
    {
        public  byte[] OldHash       { get; set; }
        public  string OldName       { get; set; }
        public  byte[] PatchHash     { get; set; }
        public  string PatchName     { get; set; }
        public  int[]  OldVersion    { get; set; }
        public  uint   PatchSize     { get; set; }
        public  string OldVersionDir { get => $"{string.Join('_', OldVersion)}"; }

        public BlockOldPatchInfo Copy() => new()
        {
            OldHash    = OldHash,
            OldName    = OldName,
            PatchHash  = PatchHash,
            PatchName  = PatchName,
            OldVersion = OldVersion,
            PatchSize  = PatchSize
        };
    }

    public class BlockPatchInfo
    {
        public  byte[]                  NewHash    { get; set; }
        public  string                  NewName    { get; set; }
        public  List<BlockOldPatchInfo> PatchPairs { get; set; }
    }

    public class BlockPatchManifest
    {

        public Dictionary<string, int> NewBlockCatalog = [];
        public List<BlockPatchInfo>    PatchAsset { get; private set; }

        public BlockPatchManifest(string filePath)
        {
            using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Initialize(fs);
        }

        public BlockPatchManifest(Stream stream, bool disposeStream = false)
        {
            Initialize(stream);

            if (disposeStream)
            {
                stream.Dispose();
            }
        }

        private void Initialize(Stream stream)
        {
            // Initialize the stream into the endian reader
            EndianBinaryReader reader = new(stream);

            // Start deserializing
            PatchAsset = DeserializeManifest(reader);
        }

        private List<BlockPatchInfo> DeserializeManifest(EndianBinaryReader reader)
        {
            // Initialize return list
            List<BlockPatchInfo> ret = [];

            // Get the patch count
            int patchCount = (int)reader.ReadUInt32();

            // Clear the reference directory
            NewBlockCatalog.Clear();

            // Do loop to read patch information
            for (int i = 0; i < patchCount; i++)
            {
                // Get the props
                string oldHash   = TrimBlockName(reader.ReadString());
                string newHash   = TrimBlockName(reader.ReadString());
                string patchHash = TrimBlockName(reader.ReadString());
                int[]  version   = TrimVersionStringToArray(reader.ReadString());
                uint   patchSize = reader.ReadUInt32();

                // Check if the patch info reference already exist. If not, then
                // add it as a new one.
                string newHashKey = newHash + ".wmv";
                if (NewBlockCatalog.TryAdd(newHashKey, ret.Count))
                {
                    // Initialize the patch info
                    BlockPatchInfo info = new()
                    {
                        NewName    = newHash + ".wmv",
                        NewHash    = HexTool.HexToBytesUnsafe(newHash),
                        PatchPairs =
                        [
                            new BlockOldPatchInfo
                            {
                                OldName    = oldHash + ".wmv",
                                OldHash    = HexTool.HexToBytesUnsafe(oldHash),
                                PatchName  = patchHash + ".wmv",
                                PatchHash  = HexTool.HexToBytesUnsafe(patchHash),
                                OldVersion = version,
                                PatchSize  = patchSize
                            }
                        ]
                    };

                    // Add the index references and patch info
                    ret.Add(info);
                }
                // Otherwise, try to add the old reference
                else
                {
                    // Get the index of the patch info
                    int index = NewBlockCatalog[newHashKey];

                    // Get the reference and add another old patch info pairs
                    BlockPatchInfo info = ret[index];
                    info.PatchPairs.Add(new BlockOldPatchInfo
                    {
                        OldName    = oldHash + ".wmv",
                        OldHash    = HexTool.HexToBytesUnsafe(oldHash),
                        PatchName  = patchHash + ".wmv",
                        PatchHash  = HexTool.HexToBytesUnsafe(patchHash),
                        OldVersion = version,
                        PatchSize  = patchSize
                    });
                }
            }

            // Return the value
            return ret;
        }

        private static string TrimBlockName(ReadOnlySpan<char> name)
        {
            // Trim the filename without extension
            ReadOnlySpan<char> split = Path.GetFileNameWithoutExtension(name);

            // Return the trimmed filename
            return new string(split);
        }

        private static int[] TrimVersionStringToArray(string version)
        {
            // Initialize return value
            int[] ret = new int[4];

            // Try split the version into array
            string[] versionArray = version.Split('_');

            // If the version array is null or the length is invalid, then throw
            if (versionArray is not { Length: 4 })
            {
                throw new FormatException($"Version string is invalid! Got: {version} as the value");
            }

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
            }

            // Return the value
            return ret;
        }
    }
}
