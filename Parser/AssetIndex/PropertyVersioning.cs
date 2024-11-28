using Hi3Helper.Data;
using Hi3Helper.Preset;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Hi3Helper.EncTool.Parser.AssetIndex
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct AssetProperty
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] hash;
        public uint size;
    }

    public class PkgVersionProperties : IAssetIndexSummary
    {
        public string localName                { get; set; }
        public string remoteURL                { get; set; }
        public string remoteName               { get; set; }
        public string remoteNamePersistent     { get; set; }
        public string md5                      { get; set; }
        [JsonPropertyName("hash")]
        public string xxh64hash                { get; set; }
        public long   fileSize                 { get; set; }
        public bool   isPatch                  { get; set; } = false;
        public string type                     { get; set; }
        public bool   isForceStoreInPersistent { get; set; }
        public bool   isForceStoreInStreaming  { get; set; }

        public string PrintSummary() => $"File [T: {type}]: {remoteName}\t{ConverterTool.SummarizeSizeSimple(fileSize)} ({fileSize} bytes)";
        public long GetAssetSize() => fileSize;
        public string GetRemoteURL() => remoteURL;
        public void SetRemoteURL(string url) => remoteURL = url;
    }

    [JsonSerializable(typeof(PkgVersionProperties))]
    internal partial class JSONContext : JsonSerializerContext { }
}
