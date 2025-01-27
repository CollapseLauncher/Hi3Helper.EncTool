using Hi3Helper.Data;
using Hi3Helper.UABT;
using Hi3Helper.UABT.Binary;
using System.Collections.Generic;
using System.IO;
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Global
// ReSharper disable CollectionNeverQueried.Global

namespace Hi3Helper.EncTool.Parser.AssetMetadata
{
    public struct CachePatchInfo
    {
        public string OldName { get; set; }
        public byte[] OldHashSHA1 { get; set; }
        public string NewName { get; set; }
        public byte[] NewHashSHA1 { get; set; }
        public byte[] OldHashMD5 { get; set; }
        public byte[] PatchHashMD5 { get; set; }
        public uint PatchSize { get; set; }
        public string PatchName { get => HexTool.BytesToHexUnsafe(PatchHashMD5) + ".patch"; }
    }

    public class CachePatchManifest
    {
        public List<CachePatchInfo> PatchAsset { get; private set; }

        public CachePatchManifest(string filePath)
        {
            using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Initialize(fs);
        }

        public CachePatchManifest(Stream stream, bool disposeStream = false)
        {
            Initialize(stream);

            if (disposeStream)
            {
                stream.Dispose();
            }
        }

        private void Initialize(Stream stream)
        {
            // Initialize patch asset
            PatchAsset = [];

            // Initialize the stream into the endian reader
            EndianBinaryReader reader = new(stream, EndianType.BigEndian, true);

            // Start deserializing
            DeserializeManifest(reader);
        }

        private void DeserializeManifest(EndianBinaryReader reader)
        {
            // Get asset count
            uint count = reader.ReadUInt32();

            // Iterate the count and read the asset property
            for (uint i = 0; i < count; i++)
            {
                // Read values
                string oldName = reader.ReadString();
                string oldHashSHA1 = oldName.Split('_')[1];
                string newName = reader.ReadString();
                string newHashSHA1 = newName.Split('_')[1];
                string oldHash = reader.ReadString();
                string patchHash = reader.ReadString();
                uint patchSize = reader.ReadUInt32();

                // Add it to list
                PatchAsset.Add(new CachePatchInfo
                {
                    OldName = oldName,
                    OldHashSHA1 = HexTool.HexToBytesUnsafe(oldHashSHA1),
                    NewName = newName,
                    NewHashSHA1 = HexTool.HexToBytesUnsafe(newHashSHA1),
                    OldHashMD5 = HexTool.HexToBytesUnsafe(oldHash),
                    PatchHashMD5 = HexTool.HexToBytesUnsafe(patchHash),
                    PatchSize = patchSize
                });
            }
        }
    }
}
