#if DEBUG
using System;
#endif
using Hi3Helper.Data;
using Hi3Helper.UABT.Binary;

// ReSharper disable InconsistentNaming
// ReSharper disable PartialTypeWithSinglePart

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal partial class SRLuaMetadata : SRIFixMetadata
    {
        protected SRAssetType InheritedAssetType { get; set; }

        protected SRLuaMetadata(string baseURL) : base(baseURL, 3)
        {
            AssetProperty = new SRAssetProperty();
            ParentRemotePath = "/client/Windows";
            MetadataPath = "/M_LuaV.bytes";
            InheritedAssetType = SRAssetType.Lua;
        }

        protected sealed override string MetadataPath
        {
            get { return base.MetadataPath; }
            set { base.MetadataPath = value; }
        }

        protected sealed override SRAssetProperty AssetProperty
        {
            get { return base.AssetProperty; }
            set { base.AssetProperty = value; }
        }

        protected sealed override string ParentRemotePath
        {
            get { return base.ParentRemotePath; }
            set { base.ParentRemotePath = value; }
        }

        internal new static SRMetadataBase CreateInstance(string baseURL) => new SRLuaMetadata(baseURL);

        internal override void Deserialize()
        {
            using EndianBinaryReader reader           = new EndianBinaryReader(AssetProperty.MetadataStream);
            uint                     toSeekPos        = 20;
            uint                     count            = reader.ReadUInt32();
            uint                     childAssetsCount = 0;

            if (count == 255)
            {
                _                = reader.ReadUInt32(); // version
                count            = reader.ReadUInt32();
#if DEBUG
                childAssetsCount
#else
                _
#endif
                    = reader.ReadUInt32();
                toSeekPos        = 12;
#if DEBUG
                Console.WriteLine($"Switching to read new {InheritedAssetType} metadata format! -> parentAsset: {count} childAsset: {childAssetsCount}");
#endif
            }

#if DEBUG
            Console.WriteLine($"{InheritedAssetType} Assets Parsed Info: ({reader.BaseStream.Length} bytes) ({count} assets)");
#endif

#if DEBUG
            uint sanityChildAssetsCount = 0;
#endif
            for (int i = 0; i < count; i++)
            {
            #if DEBUG
                long   lastPos
            #else
                _
            #endif
                    = reader.Position;
            #if DEBUG
                byte[] assetID
            #else
                _
                #endif
                    = reader.ReadBytes(4);
                byte[] hash    = reader.ReadBytes(16);
                _ = reader.ReadUInt32(); // type
                uint size        = reader.ReadUInt32();
                uint insideCount = reader.ReadUInt32();

#if DEBUG
                sanityChildAssetsCount += insideCount;
#endif

                // toSeekPos        = Number of bytes to read the asset's content
                // insideCount      = Number of content count inside the asset
                // 1                = Unknown offset, seek +1
                uint toSeek = insideCount * toSeekPos + 1;
                reader.Position += toSeek;

                string hashName  = HexTool.BytesToHexUnsafe(hash);
                string assetName = $"{hashName}.bytes";
                AssetProperty.AssetList.Add(new SRAsset
                {
                    AssetType = InheritedAssetType,
                    Hash      = hash,
                    LocalName = assetName,
                    RemoteURL = BaseURL + ParentRemotePath + '/' + assetName,
                    Size      = size
                });
#if DEBUG
                Console.WriteLine($"    Mark: {HexTool.BytesToHexUnsafe(assetID)} {hashName} -> Size: {size} Pos: {lastPos} Seek: {toSeek} Count: {insideCount}");
#endif
            }

#if DEBUG
            if (count == 255 && sanityChildAssetsCount != childAssetsCount) throw new IndexOutOfRangeException("SANITY CHECK: childAssetsCount is not equal as sanityChildAssetsCount. There might be another additional data here");
#endif
#if DEBUG
            if (reader.Position < reader.BaseStream.Length) throw new IndexOutOfRangeException("SANITY CHECK: reader.Position is not equal as reader.BaseStream.Length. There might be another additional data here");
#endif
        }
    }
}
