using Hi3Helper.UABT.Binary;
using System;
using System.Collections.Generic;

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal struct SRAMBMMetadataStruct
    {
        public uint offset;
        public uint structCount;
        public uint structSize;
        public byte[][] structData;

        public void ClearStruct()
        {
            structData = null;
            offset = 0;
            structCount = 0;
            structSize = 0;
        }
    }

    internal enum SRAMBMMetadataType
    {
        SRAM,
        SRBM,
        JSON
    }

    internal class SRAMBMMetadataReader : SRMetadataBase
    {
        internal List<SRAMBMMetadataStruct> StructList { get; set; }
        internal SRAMBMMetadataType StructType { get; set; }

        protected override string ParentRemotePath { get; set; }
        protected override string MetadataPath { get; set; }
        protected override SRAssetProperty AssetProperty { get; set; }

        internal uint RemoteRevisionID { get; set; }
        internal uint MetadataInfoSize { get; set; }
        internal string AssetListFilename { get; set; }
        internal uint AssetListFilesize { get; set; }
        internal ulong AssetListUnixTimestamp { get; set; }
        internal string AssetListRootPath { get; set; }

        internal SRAMBMMetadataReader(string baseURL, Http.Http httpClient, string parentRemotePath, string metadataPath, SRAMBMMetadataType type) : base(baseURL, httpClient)
        {
            StructType = type;
            switch (type)
            {
                case SRAMBMMetadataType.SRAM:
                    Magic = "SRAM";
                    TypeID = 3;
                    break;
                case SRAMBMMetadataType.SRBM:
                    Magic = "SRBM";
                    TypeID = 1;
                    break;
            }
            ParentRemotePath = parentRemotePath;
            MetadataPath = metadataPath;
        }

        struct UnkHeadAsset
        {
            public byte[] hash;
            public uint uuid;
            public uint size;
        }

        internal override void Deserialize()
        {
            using (EndianBinaryReader reader = new EndianBinaryReader(AssetProperty.MetadataStream, UABT.EndianType.BigEndian, true))
            {
                StructList = new List<SRAMBMMetadataStruct>();
                EnsureMagicIsValid(reader);

                _ = reader.ReadUInt16();

                reader.endian = UABT.EndianType.LittleEndian;

                uint numA1 = reader.ReadUInt32();
                ushort count = reader.ReadUInt16();
                ushort numA2 = reader.ReadUInt16();

                uint numA3;
                uint numA4;
                uint numA5;
                uint offset;
                uint structCount;
                uint structSize;

#if DEBUG
                Console.WriteLine($"{Magic} Parsed Info: ({reader.BaseStream.Length} bytes) ({(StructType == SRAMBMMetadataType.SRAM ? count : 1)} structs)");
#endif

                switch (StructType)
                {
                    case SRAMBMMetadataType.SRAM:
                        numA3 = reader.ReadUInt32();
                        numA4 = reader.ReadUInt32();
                        numA5 = reader.ReadUInt32();
                        for (int i = 0; i < count; i++)
                        {
                            offset = reader.ReadUInt32();
                            structCount = reader.ReadUInt32();
                            structSize = reader.ReadUInt32();
                            StructList.Add(new SRAMBMMetadataStruct
                            {
                                offset = offset,
                                structCount = structCount,
                                structSize = structSize,
                                structData = new byte[structCount][]
                            });
                        }
                        break;
                    case SRAMBMMetadataType.SRBM:
                        offset = reader.ReadUInt32();
                        structCount = reader.ReadUInt32();
                        structSize = reader.ReadUInt32();
                        StructList.Add(new SRAMBMMetadataStruct
                        {
                            offset = offset,
                            structCount = structCount,
                            structSize = structSize,
                            structData = new byte[structCount][]
                        });
                        break;
                }

                ReadStruct(reader, StructList);
            }
        }

        private void ReadStruct(EndianBinaryReader reader, List<SRAMBMMetadataStruct> assetList)
        {
            for (int a = 0; a < assetList.Count; a++)
            {
#if DEBUG
                Console.WriteLine($"    Struct: {a} -> offset: {assetList[a].offset} | count: {assetList[a].structCount} | size: {assetList[a].structSize} bytes | toSize: {assetList[a].structSize * assetList[a].structCount} bytes");
#endif
                reader.Position = assetList[a].offset;
                for (int b = 0; b < assetList[a].structCount; b++)
                {
                    assetList[a].structData[b] = reader.ReadBytes((int)assetList[a].structSize);
                }
            }
        }

        internal override void Dispose(bool Disposing)
        {
            if (StructList != null)
            {
                StructList.Clear();
                StructList = null;
            }

            AssetProperty?.MetadataStream?.Dispose();
            AssetProperty?.AssetList?.Clear();
            AssetProperty = null;
        }
    }
}
