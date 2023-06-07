using Hi3Helper.Data;
using Hi3Helper.UABT.Binary;
using System;
using System.IO;

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal class SRMIMetadataReader : SRMetadataBase
    {
        protected override string ParentRemotePath { get; set; }
        protected override string MetadataPath { get; set; }
        protected override SRAssetProperty AssetProperty { get; set; }

        internal uint RemoteRevisionID { get; set; }
        internal uint MetadataInfoSize { get; set; }
        internal string AssetListFilename { get; set; }
        internal uint AssetListFilesize { get; set; }
        internal ulong AssetListUnixTimestamp { get; set; }
        internal string AssetListRootPath { get; set; }

        internal SRMIMetadataReader(string baseURL, Http.Http httpClient, string parentRemotePath, string metadataPath, ushort typeID = 2) : base(baseURL, httpClient)
        {
            Magic = "SRMI";
            TypeID = typeID;
            ParentRemotePath = parentRemotePath;
            MetadataPath = metadataPath;
        }

        internal override void Deserialize()
        {
            using (EndianBinaryReader reader = new EndianBinaryReader(AssetProperty.MetadataStream, UABT.EndianType.BigEndian, true))
            {
                EnsureMagicIsValid(reader);

                _ = reader.ReadUInt16();
                reader.endian = UABT.EndianType.LittleEndian;
                MetadataInfoSize = reader.ReadUInt32();
                reader.BaseStream.Seek(0xC, SeekOrigin.Current);

                RemoteRevisionID = reader.ReadUInt32();

                ReadAssets(reader);

#if DEBUG
                Console.WriteLine($"SRMI Parsed Info: ({MetadataInfoSize} bytes)");
                Console.WriteLine($"    AssetListFilename: {AssetListFilename}");
                Console.WriteLine($"    AssetListFilesize: {AssetListFilesize} bytes");
                Console.WriteLine($"    AssetListUnixTimestamp: {AssetListUnixTimestamp} or {new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(AssetListUnixTimestamp)} UTC");
                Console.WriteLine($"    AssetListRootPath: {AssetListRootPath}");
                Console.WriteLine($"    RemoteRevisionID: {RemoteRevisionID}");
#endif
            }
        }

        private unsafe void ReadAssets(EndianBinaryReader reader)
        {
            Span<byte> fullHash = stackalloc byte[16];
            for (
                int i = 0, k = 0;
                i < 4;
                i++)
            {
                int hashC = reader.ReadInt32();
                byte* chunk = (byte*)&hashC;
                for (
                    int j = 3;
                    j >= 0;
                    j--, k++)
                {
                    fullHash[k] = chunk[j];
                }
            }

            AssetListFilesize = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            AssetListUnixTimestamp = reader.ReadUInt64();
            AssetListRootPath = reader.ReadString();

            AssetListFilename = MetadataPath.Split('_', '.')[1] + $"_{HexTool.BytesToHexUnsafe(fullHash)}.bytes";
        }

        internal override void Dispose(bool Disposing)
        {
            AssetProperty?.MetadataStream?.Dispose();
            AssetProperty?.AssetList?.Clear();
            AssetProperty = null;
        }
    }
}
