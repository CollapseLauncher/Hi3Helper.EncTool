using Hi3Helper.Data;
using Hi3Helper.UABT;
using Hi3Helper.UABT.Binary;
using System;
using System.IO;
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal sealed class SRMIMetadataReader : SRMetadataBase
    {
        protected override string          ParentRemotePath { get; set; }
        protected override string          MetadataPath     { get; set; }
        protected override SRAssetProperty AssetProperty    { get; set; }

        internal uint   RemoteRevisionID       { get; set; }
        internal uint   MetadataInfoSize       { get; set; }
        internal string AssetListFilename      { get; set; }
        internal uint   AssetListFilesize      { get; set; }
        internal ulong  AssetListUnixTimestamp { get; set; }
        internal string AssetListRootPath      { get; set; }

        internal SRMIMetadataReader(string baseURL, string parentRemotePath, string metadataPath, ushort typeID = 2) : base(baseURL)
        {
            Magic            = "SRMI";
            TypeID           = typeID;
            ParentRemotePath = parentRemotePath;
            MetadataPath     = metadataPath;
        }

        internal override void Deserialize()
        {
            using EndianBinaryReader reader = new EndianBinaryReader(AssetProperty.MetadataStream, EndianType.BigEndian, true);
            EnsureMagicIsValid(reader);

            _                = reader.ReadUInt16();
            reader.Endian    = EndianType.LittleEndian;
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

        private unsafe void ReadAssets(EndianBinaryReader reader)
        {
            Span<byte> fullHash = stackalloc byte[16];
            Int128 hashInt128 = reader.ReadInt128();

            byte* chunk = (byte*)&hashInt128;
            fixed (byte* hashReturn = fullHash)
            {
                *hashReturn = *(chunk + 3);
                *(hashReturn + 1) = *(chunk + 2);
                *(hashReturn + 2) = *(chunk + 1);
                *(hashReturn + 3) = *chunk;

                *(hashReturn + 4) = *(chunk + 7);
                *(hashReturn + 5) = *(chunk + 6);
                *(hashReturn + 6) = *(chunk + 5);
                *(hashReturn + 7) = *(chunk + 4);

                *(hashReturn + 8) = *(chunk + 11);
                *(hashReturn + 9) = *(chunk + 10);
                *(hashReturn + 10) = *(chunk + 9);
                *(hashReturn + 11) = *(chunk + 8);

                *(hashReturn + 12) = *(chunk + 15);
                *(hashReturn + 13) = *(chunk + 14);
                *(hashReturn + 14) = *(chunk + 13);
                *(hashReturn + 15) = *(chunk + 12);
            }

            AssetListFilesize      = reader.ReadUInt32();
            _                      = reader.ReadUInt32();
            AssetListUnixTimestamp = reader.ReadUInt64();
            AssetListRootPath      = reader.ReadString();
            AssetListFilename      = MetadataPath.Split('_', '.')[1] + $"_{HexTool.BytesToHexUnsafe(fullHash)}.bytes";
        }

        internal override void Dispose(bool Disposing)
        {
            AssetProperty?.MetadataStream?.Dispose();
            AssetProperty?.AssetList?.Clear();
            AssetProperty = null;
        }
    }
}
