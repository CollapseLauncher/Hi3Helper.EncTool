using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.UABT.Binary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace

namespace Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset
{
    public class SRAssetProperty
    {
        public string        BaseURL                { get; set; }
        public string        StartBaseURL           { get; set; }
        public string        MetadataRemoteURL      { get; set; }
        public string        MetadataStartRemoteURL { get; set; }
        public string        MetadataLocalName      { get; set; }
        public string        MetadataStartLocalName { get; set; }
        public Stream        MetadataStream         { get; set; }
        public Stream        MetadataStartStream    { get; set; }
        public uint          MetadataRevision       { get; set; }
        public uint          MetadataStartRevision  { get; set; }
        public long          AssetTotalSize         { get => AssetList.Count == 0 ? 0 : AssetList.Sum(x => x.Size); }
        public List<SRAsset> AssetList              { get; set; }

        public SRAssetProperty()
        {
            MetadataStream      = new MemoryStream();
            MetadataStartStream = new MemoryStream();
            AssetList           = [];
        }

        public SRAssetProperty(string metadataPath, string metadataStartPath = null)
        {
            string metadataFolder = Path.GetDirectoryName(metadataPath);
            if (!string.IsNullOrEmpty(metadataFolder) && !Directory.Exists(metadataFolder))
            {
                Directory.CreateDirectory(metadataFolder);
            }

            MetadataStream = new FileStream(metadataPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            if (!string.IsNullOrEmpty(metadataStartPath))
            {
                string metadataStartFolder = Path.GetDirectoryName(metadataStartPath);
                if (!string.IsNullOrEmpty(metadataStartFolder) && !Directory.Exists(metadataStartFolder))
                {
                    Directory.CreateDirectory(metadataStartFolder);
                }
                MetadataStartStream = new FileStream(metadataStartPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            }
            AssetList = [];
        }
    }

    public class SRAsset : IAssetIndexSummary
    {
        public string      LocalName { get; set; }
        public string      RemoteURL { get; set; }
        public long        Size      { get; set; }
        public byte[]      Hash      { get; set; }
        public SRAssetType AssetType { get; set; }
        public bool        IsPatch   { get; set; }

        public string PrintSummary() => $"File [T: {AssetType}]: {LocalName}\t{ConverterTool.SummarizeSizeSimple(Size)} ({Size} bytes)";
        public long GetAssetSize() => Size;
        public string GetRemoteURL() => RemoteURL;
        public void SetRemoteURL(string url) => RemoteURL = url;
    }

    public enum SRAssetType
    {
        IFix,
        DesignData,
        NativeData,
        Asb,
        Block,
        Lua,
        Video,
        Audio,

        RawRes // TODO: Introduced in v3.5. Need to implement the parser in Collapse v1.83.x update later
    }

    public abstract class SRMetadataBase : IDisposable
    {
        protected abstract SRAssetProperty AssetProperty     { get; set; }
        internal virtual   string          Magic             { get; set; }
        internal virtual   ushort          TypeID            { get; set; }
        protected abstract string          ParentRemotePath  { get; set; }
        protected abstract string          MetadataPath      { get; set; }
        protected          string          BaseURL           { get; init; }
        protected          string          BaseURLAlt        { get; set; } = "";
        protected          string          PersistentPath    { get; set; }
        protected          bool            UseURLAltForAsset { get; set; }

        protected SRMetadataBase(string baseURL, string baseURLAlt = "")
        {
            BaseURL = baseURL;
            if (!string.IsNullOrEmpty(baseURLAlt))
            {
                BaseURLAlt = baseURLAlt;
            }
            if (string.IsNullOrEmpty(BaseURL)) throw new NullReferenceException("BaseURL is empty!");
        }

        internal virtual async Task GetRemoteMetadata(DownloadClient downloadClient, DownloadProgressDelegate downloadProgressDelegate, string persistentPath, CancellationToken token, string localManifestPath)
        {
            PersistentPath = persistentPath;
            string metadataPath = Path.Combine(PersistentPath, localManifestPath, MetadataPath.TrimStart('/'));
            string metadataURL = BaseURL + ParentRemotePath + MetadataPath;
            string metadataURLAlt = !string.IsNullOrEmpty(BaseURLAlt) ? BaseURLAlt + ParentRemotePath + MetadataPath : "";

            AssetProperty = new SRAssetProperty(metadataPath);

#if DEBUG
            Console.WriteLine($"[SRMetadataBase:GetRemoteData] Fetching metadata from {metadataURL}");
#endif

            bool isUseAltForAsset = await GetRefDataAndTrySetAssetUrl(downloadClient,
                                                                      downloadProgressDelegate,
                                                                      metadataURL,
                                                                      metadataURLAlt,
                                                                      AssetProperty.MetadataStream,
                                                                      token);

            if (isUseAltForAsset && !string.IsNullOrEmpty(BaseURLAlt))
            {
                UseURLAltForAsset = true;
            }
        }

        protected virtual async Task<bool> GetRefDataAndTrySetAssetUrl(
            DownloadClient           downloadClient,
            DownloadProgressDelegate downloadProgressDelegate,
            string                   metadataUrl,
            string                   metadataUrlAlt,
            Stream                   outputStream,
            CancellationToken        token)
        {
            try
            {
                await downloadClient.PerformCopyToDownload(metadataUrl, downloadProgressDelegate, outputStream, token);
                outputStream.Position = 0;
                return true;
            }
            catch when (!string.IsNullOrEmpty(metadataUrlAlt))
            {
                _ = await GetRefDataAndTrySetAssetUrl(downloadClient, downloadProgressDelegate, metadataUrlAlt, "", outputStream, token);
                return false;
            }
        }

        protected void EnsureMagicIsValid(EndianBinaryReader reader)
        {
            string magic = reader.ReadAlignedString(4);
            if (!magic.Equals(Magic)) throw new FormatException($"Magic is not valid for metadata: {Magic}. Getting {magic} instead");

            ushort typeID = reader.ReadUInt16();
            if (typeID != TypeID) throw new FormatException($"TypeID for metadata: {Magic} is not valid. Getting {typeID} while expecting {TypeID}");
        }

        internal abstract void Deserialize();
        public SRAssetProperty GetAssets() => AssetProperty;
        public IEnumerable<SRAsset> EnumerateAssets() => AssetProperty.AssetList;
        internal abstract void Dispose(bool Disposing);
        internal static string GetMetadataPathFromArchiveInfo(Dictionary<string, SRDispatchArchiveInfo> dictArchiveInfo, string key)
        {
            if (!dictArchiveInfo.TryGetValue(key, out var archiveInfo)) throw new KeyNotFoundException($"Key: {key} in ArchiveInfo dictionary does not exist!");

            ReadOnlySpan<char> baseName = archiveInfo.FileName.AsSpan()[2..];

            return '/' + string.Concat(baseName, ['_'], archiveInfo.ContentHash, ".bytes");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
