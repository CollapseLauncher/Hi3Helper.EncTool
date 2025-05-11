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
        [JsonIgnore]
        public string localName            { get; set; }
        [JsonIgnore]
        public string remoteURL            { get; set; }
        [JsonIgnore]
        public string remoteURLPersistent  { get; set; }
        [JsonIgnore]
        public string remoteURLAlternative { get; set; }
        public string remoteName           { get; set; }
        [JsonIgnore]
        public string remoteNamePersistent { get; set; }
        public string md5                  { get; set; }
        [JsonIgnore]
        public string md5Persistent        { get; set; }
        [JsonPropertyName("hash")]
        public string xxh64hash { get;                set; }
        [JsonIgnore]
        public string xxh64hashPersistent      { get; set; }
        public long   fileSize                 { get; set; }
        [JsonIgnore]
        public long   fileSizePersistent       { get; set; }
        [JsonIgnore]
        public bool   isPatch                  { get; set; }
        [JsonIgnore]
        public string type                     { get; set; }
        [JsonIgnore]
        public bool   isForceStoreInPersistent { get; set; }
        [JsonIgnore]
        public bool   isForceStoreInStreaming  { get; set; }
        [JsonIgnore]
        public bool   isStreamingNeedDownload  { get; set; }
        [JsonIgnore]
        public bool   isPersistentNeedDownload { get; set; }

        public string PrintSummary() => $"File [T: {type}]: {remoteName}\t{ConverterTool.SummarizeSizeSimple(fileSize)} ({fileSize} bytes)";
        public long GetAssetSize() => fileSize;
        public string GetRemoteURL() => remoteURL;
        public void SetRemoteURL(string url) => remoteURL = url;
        public PkgVersionProperties Clone()
            => new()
            {
                localName                = localName,
                remoteURL                = remoteURL,
                remoteURLPersistent      = remoteURLPersistent,
                remoteName               = remoteName,
                remoteNamePersistent     = remoteNamePersistent,
                fileSize                 = fileSize,
                fileSizePersistent       = fileSizePersistent,
                isForceStoreInPersistent = isForceStoreInPersistent,
                isForceStoreInStreaming  = isForceStoreInStreaming,
                isPatch                  = isPatch,
                md5                      = md5,
                md5Persistent            = md5Persistent,
                type                     = type,
                xxh64hash                = xxh64hash,
                xxh64hashPersistent      = xxh64hashPersistent
            };

        public PkgVersionProperties CloneAsPersistent()
            => new()
            {
                localName                = localName,
                remoteURL                = remoteURLPersistent ?? remoteURL,
                remoteName               = remoteNamePersistent ?? remoteName,
                fileSize                 = fileSizePersistent == 0 ? fileSize : fileSizePersistent,
                isForceStoreInPersistent = isForceStoreInPersistent,
                isForceStoreInStreaming  = isForceStoreInStreaming,
                isPatch                  = isPatch,
                md5                      = md5Persistent ?? md5,
                type                     = type,
                xxh64hash                = xxh64hashPersistent ?? xxh64hash
            };
    }

    [JsonSerializable(typeof(PkgVersionProperties))]
    internal partial class JsonContext : JsonSerializerContext;
}
