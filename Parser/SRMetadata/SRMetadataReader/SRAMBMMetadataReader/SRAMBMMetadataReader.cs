using Hi3Helper.UABT;
using Hi3Helper.UABT.Binary;
using System;
using System.Collections.Generic;
// ReSharper disable IdentifierTypo
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable StringLiteralTypo
// ReSharper disable InconsistentNaming
// ReSharper disable PartialTypeWithSinglePart

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal struct SRAMBMMetadataStruct
    {
        public uint Offset;
        public uint StructCount;
        public uint StructSize;
        public byte[][] StructData;

        public void ClearStruct()
        {
            StructData = null;
            Offset = 0;
            StructCount = 0;
            StructSize = 0;
        }
    }

    internal enum SRAMBMMetadataType
    {
        SRAM,
        SRBM,
        JSON
    }

    internal sealed partial class SRAMBMMetadataReader : SRMetadataBase
    {
        internal           List<SRAMBMMetadataStruct> StructList             { get; set; }
        internal           SRAMBMMetadataType         StructType             { get; set; }
        protected override string                     ParentRemotePath       { get; set; }
        protected override string                     MetadataPath           { get; set; }
        protected override SRAssetProperty            AssetProperty          { get; set; }
        internal           uint                       RemoteRevisionID       { get; set; }
        internal           uint                       MetadataInfoSize       { get; set; }
        internal           string                     AssetListFilename      { get; set; }
        internal           uint                       AssetListFilesize      { get; set; }
        internal           ulong                      AssetListUnixTimestamp { get; set; }
        internal           string                     AssetListRootPath      { get; set; }

        internal SRAMBMMetadataReader(string baseURL, string parentRemotePath, string metadataPath, SRAMBMMetadataType type) : base(baseURL)
        {
            StructType = type;
            switch (type)
            {
                case SRAMBMMetadataType.SRAM:
                    Magic = "SRAM";
                    TypeID = 4;
                    break;
                case SRAMBMMetadataType.SRBM:
                    Magic = "SRBM";
                    TypeID = 1;
                    break;
            }
            ParentRemotePath = parentRemotePath;
            MetadataPath = metadataPath;
        }

        internal override void Deserialize()
        {
            using EndianBinaryReader reader = new EndianBinaryReader(AssetProperty.MetadataStream, EndianType.BigEndian, true);
            StructList = [];
            EnsureMagicIsValid(reader);

            _ = reader.ReadUInt16();

            reader.Endian = EndianType.LittleEndian;

            _ = reader.ReadUInt32(); // A1
            ushort count = reader.ReadUInt16();
            _ = reader.ReadUInt16(); // A2
                
            uint offset;
            uint structCount;
            uint structSize;

        #if DEBUG
            Console.WriteLine($"{Magic} Parsed Info: ({reader.BaseStream.Length} bytes) ({(StructType == SRAMBMMetadataType.SRAM ? count : 1)} structs)");
        #endif

            switch (StructType)
            {
                case SRAMBMMetadataType.SRAM:
                    _ = reader.ReadUInt32(); // A3
                    _ = reader.ReadUInt32(); // A4
                    _ = reader.ReadUInt32(); // A5
                    for (int i = 0; i < count; i++)
                    {
                        offset      = reader.ReadUInt32();
                        structCount = reader.ReadUInt32();
                        structSize  = reader.ReadUInt32();
                        StructList.Add(new SRAMBMMetadataStruct
                        {
                            Offset      = offset,
                            StructCount = structCount,
                            StructSize  = structSize,
                            StructData  = new byte[structCount][]
                        });
                    }
                    break;
                case SRAMBMMetadataType.SRBM:
                    offset      = reader.ReadUInt32();
                    structCount = reader.ReadUInt32();
                    structSize  = reader.ReadUInt32();
                    StructList.Add(new SRAMBMMetadataStruct
                    {
                        Offset      = offset,
                        StructCount = structCount,
                        StructSize  = structSize,
                        StructData  = new byte[structCount][]
                    });
                    break;
            }

            ReadStruct(reader, StructList);
        }

        private static void ReadStruct(EndianBinaryReader reader, List<SRAMBMMetadataStruct> assetList)
        {
            for (int a = 0; a < assetList.Count; a++)
            {
            #if DEBUG
                Console.WriteLine($"    Struct: {a} -> offset: {assetList[a].Offset} | count: {assetList[a].StructCount} | size: {assetList[a].StructSize} bytes | toSize: {assetList[a].StructSize * assetList[a].StructCount} bytes");
            #endif
                reader.Position = assetList[a].Offset;

                if (a > 0)
                {
                #if DEBUG
                    Console.WriteLine($"    Skipping struct index: {a} info");
                #endif
                    continue;
                }

                for (int b = 0; b < assetList[a].StructCount; b++)
                {
                    assetList[a].StructData[b] = reader.ReadBytes((int)assetList[a].StructSize);
                }
            }
        }

        internal override void Dispose(bool disposing)
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
