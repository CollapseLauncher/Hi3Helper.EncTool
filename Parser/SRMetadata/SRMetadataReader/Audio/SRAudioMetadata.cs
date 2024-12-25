using Hi3Helper.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    internal struct SRJSONAssetInfo
    {
        public string Path { get; set; }
        public string Md5 { get; set; }
        public long Size { get; set; }
        public bool Patch { get; set; }
        public int SubPackId { get; set; }
    }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(SRJSONAssetInfo))]
    internal sealed partial class SRJSONAssetInfoContext : JsonSerializerContext { }

    internal partial class SRAudioMetadata : SRAsbMetadata
    {
        protected SRAudioMetadata(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL) : base(dictArchiveInfo, baseURL)
        {
            ParentRemotePath = "/client/Windows/AudioBlock";
            MetadataRemoteName = "M_AudioV";
            MetadataType = SRAMBMMetadataType.JSON;
            AssetType = SRAssetType.Audio;
        }

        internal static new SRMetadataBase CreateInstance(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string baseURL) => new SRAudioMetadata(dictArchiveInfo, baseURL);

        internal override void Deserialize()
        {
            using (StreamReader reader = new StreamReader(AssetProperty.MetadataStream, Encoding.UTF8, true, -1, false))
            {
#if DEBUG
                Console.WriteLine($"{AssetType} Assets Parsed Info: ({AssetProperty.MetadataStream.Length} bytes)");
#endif
                int index = 0;
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    SRJSONAssetInfo assetInfo = (SRJSONAssetInfo)JsonSerializer.Deserialize(line, typeof(SRJSONAssetInfo), SRJSONAssetInfoContext.Default);

                    SRAsset asset = new SRAsset
                    {
                        AssetType = AssetType,
                        Hash = HexTool.HexToBytesUnsafe(assetInfo.Md5),
                        LocalName = assetInfo.Path,
                        RemoteURL = (assetInfo.Patch ? BaseURL + ParentRemotePath : AssetProperty.BaseURL) + '/' + assetInfo.Path,
                        Size = assetInfo.Size,
                        IsPatch = assetInfo.Patch
                    };

#if DEBUG
                    Console.WriteLine($"    {index} {assetInfo.Path} -> {assetInfo.Md5} | {assetInfo.Size} bytes | IsPatch: {assetInfo.Patch}");
#endif

                    AssetProperty.AssetList.Add(asset);
                    index++;
                }
            }
        }
    }
}
