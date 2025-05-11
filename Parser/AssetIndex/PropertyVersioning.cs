using Hi3Helper.Data;
using Hi3Helper.Preset;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming
// ReSharper disable PartialTypeWithSinglePart

namespace Hi3Helper.EncTool.Parser.AssetIndex
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct AssetProperty
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] hash;
        public uint size;
    }

    public class PkgVersionProperties : IAssetIndexSummary<PkgVersionProperties>
    {
        public string localName            { get; set; }
        [JsonIgnore]
        public string remoteURL            { get; set; }
        [JsonIgnore]
        public string remoteURLAlternative { get; set; }
        public string remoteName           { get; set; }
        public string md5                  { get; set; }
        [JsonPropertyName("hash")]
        public string xxh64hash { get;                set; }
        public long   fileSize                 { get; set; }
        [JsonIgnore]
        public bool   isPatch                  { get; set; }
        [JsonIgnore]
        public bool   isForceStoreInPersistent { get; set; }
        [JsonIgnore]
        public bool   isForceStoreInStreaming  { get; set; }

        public string PrintSummary() => $"{remoteName}\t{ConverterTool.SummarizeSizeSimple(fileSize)} ({fileSize} bytes)";
        public long GetAssetSize() => fileSize;
        public string GetRemoteURL() => remoteURL;
        public void SetRemoteURL(string url) => remoteURL = url;
        public PkgVersionProperties Clone()
            => new()
            {
                localName                = localName,
                remoteURL                = remoteURL,
                remoteName               = remoteName,
                fileSize                 = fileSize,
                isForceStoreInPersistent = isForceStoreInPersistent,
                isForceStoreInStreaming  = isForceStoreInStreaming,
                isPatch                  = isPatch,
                md5                      = md5,
                xxh64hash                = xxh64hash,
            };
    }

    [JsonSerializable(typeof(PkgVersionProperties))]
    internal partial class JsonContext : JsonSerializerContext;
}
