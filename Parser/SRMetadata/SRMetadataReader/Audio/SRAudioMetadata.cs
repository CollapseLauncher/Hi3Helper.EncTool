﻿using Hi3Helper.Data;
#if DEBUG
using System;
#endif
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal sealed class SRJSONAssetInfo
    {
        public string Path { get; set; }
        public string Md5 { get; set; } 
        public long Size { get; set; }
        public bool Patch { get; set; }
        public int SubPackId { get; set; }
    }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(SRJSONAssetInfo))]
    internal sealed partial class SRJSONAssetInfoContext : JsonSerializerContext;

    internal partial class SRAudioMetadata : SRAsbMetadata
    {
        protected SRAudioMetadata(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL) : base(dictArchiveInfo, baseURL)
        {
            ParentRemotePath   = "/client/Windows/AudioBlock";
            MetadataRemoteName = "M_AudioV";
            MetadataType       = SRAMBMMetadataType.JSON;
            AssetType          = SRAssetType.Audio;
        }

        protected sealed override string ParentRemotePath
        {
            get { return base.ParentRemotePath; }
            set { base.ParentRemotePath = value; }
        }

        internal new static SRMetadataBase CreateInstance(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL) => new SRAudioMetadata(dictArchiveInfo, baseURL);

        internal override void Deserialize()
        {
            using StreamReader reader = new(AssetProperty.MetadataStream, Encoding.UTF8, true, -1, false);
#if DEBUG
            Console.WriteLine($"{AssetType} Assets Parsed Info: ({AssetProperty.MetadataStream.Length} bytes)");
#endif
#if DEBUG
            int index = 0;
#endif
            while (!reader.EndOfStream)
            {
            #nullable enable
                string           line      = reader.ReadLine() ?? "";
                SRJSONAssetInfo? assetInfo = JsonSerializer.Deserialize(line, SRJSONAssetInfoContext.Default.SRJSONAssetInfo);

                SRAsset asset = new()
                {
                    AssetType = AssetType,
                    Hash      = HexTool.HexToBytesUnsafe(assetInfo?.Md5 ?? ""),
                    LocalName = assetInfo?.Path,
                    RemoteURL = (assetInfo?.Patch ?? false ? BaseURL + ParentRemotePath : AssetProperty.BaseURL) + '/' + assetInfo?.Path,
                    Size      = assetInfo?.Size ?? 0,
                    IsPatch   = assetInfo?.Patch ?? false
                };
            #nullable restore

#if DEBUG
                Console.WriteLine($"    {index} {assetInfo?.Path} -> {assetInfo?.Md5} | {assetInfo?.Size} bytes | IsPatch: {assetInfo?.Patch}");
#endif

                AssetProperty.AssetList.Add(asset);
#if DEBUG
                index++;
#endif
            }
        }
    }
}
