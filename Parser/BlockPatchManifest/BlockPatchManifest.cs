using Hi3Helper.Data;
using Hi3Helper.UABT;
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
        public  int    PatchSize     { get; set; }
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
        public Dictionary<string, int> NewBlockCatalog { get; } = [];
        public List<BlockPatchInfo>    PatchAsset      { get; } = [];

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
            using EndianBinaryReader reader = new(stream, EndianType.BigEndian, true);

            // Start deserializing
            DeserializeManifest(reader);
        }

        private void DeserializeManifest(EndianBinaryReader reader)
        {
            // Clear the reference directory
            PatchAsset.Clear();
            NewBlockCatalog.Clear();

            // Get the patch count
            int patchCount = reader.ReadInt32();

            // Do loop to read patch information
            for (int i = 0; i < patchCount; i++)
            {
                // Get the props
                string oldFileName   = reader.ReadString();
                string newFileName   = reader.ReadString();
                string patchFileName = reader.ReadString();
                int[]  version       = TrimVersionStringToArray(reader.ReadString());
                int    patchSize     = reader.ReadInt32();

                ReadOnlySpan<char> oldFileHash   = Path.GetFileNameWithoutExtension(oldFileName);
                ReadOnlySpan<char> newFileHash   = Path.GetFileNameWithoutExtension(newFileName);
                ReadOnlySpan<char> patchFileHash = Path.GetFileNameWithoutExtension(patchFileName);

                // Check if the patch info reference already exist. If not, then
                // add it as a new one.
                if (NewBlockCatalog.TryAdd(newFileName, PatchAsset.Count))
                {
                    // Initialize the patch info
                    BlockPatchInfo info = new()
                    {
                        NewName    = newFileName,
                        NewHash    = HexTool.HexToBytesUnsafe(newFileHash),
                        PatchPairs =
                        [
                            new BlockOldPatchInfo
                            {
                                OldName    = oldFileName,
                                OldHash    = HexTool.HexToBytesUnsafe(oldFileHash),
                                PatchName  = patchFileName,
                                PatchHash  = HexTool.HexToBytesUnsafe(patchFileHash),
                                OldVersion = version,
                                PatchSize  = patchSize
                            }
                        ]
                    };

                    // Add the index references and patch info
                    PatchAsset.Add(info);
                }
                // Otherwise, try to add the old reference
                else
                {
                    // Get the index of the patch info
                    int index = NewBlockCatalog[newFileName];

                    // Get the reference and add another old patch info pairs
                    BlockPatchInfo info = PatchAsset[index];
                    info.PatchPairs.Add(new BlockOldPatchInfo
                    {
                        OldName    = oldFileName,
                        OldHash    = HexTool.HexToBytesUnsafe(oldFileHash),
                        PatchName  = patchFileName,
                        PatchHash  = HexTool.HexToBytesUnsafe(patchFileHash),
                        OldVersion = version,
                        PatchSize  = patchSize
                    });
                }
            }
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
