#if DEBUG
using Hi3Helper.Data;
using System;
#endif
using System.Collections.Generic;
using System.IO;
using System.Linq;

// ReSharper disable InconsistentNaming
namespace Hi3Helper.EncTool.Parser
{
    public sealed partial class XMFParser
    {
        internal const  byte   SignatureLength   = 0x10;    // 16
        internal const  byte   VersioningLength  = 0x04;   // 4
        internal const  byte   AllowedMinVersion = 0x00;  // 0
        internal const  byte   AllowedMaxVersion = 0x40;  // 64
        internal        string XmfPath;
        internal static string FolderPath;

        public XMFParser(string path, Stream xmfStream, bool isMeta)
        {
            XmfPath = path;
            IsMeta = isMeta;

            ParseMetadata(xmfStream, isMeta);

#if DEBUG
            Console.WriteLine($"XMF File Loaded   : {XmfPath}");
            Console.WriteLine($"Folder Path       : {FolderPath}");
            Console.WriteLine($"Version Signature : {HexTool.BytesToHexUnsafe(VersionSignature)}");
            Console.WriteLine($"Manifest Version  : {string.Join('.', Version)}");
            Console.WriteLine($"Block Count       : {BlockCount}");
            Console.WriteLine($"Block Total Size  : {BlockTotalSize} bytes");
            Console.WriteLine($"Asset Entries     : {BlockEntry.Sum(x => x.AssetCount)}");
#endif
        }

        public Dictionary<string, uint> BlockIndexCatalog { get; private set; }
        public byte[]                   VersionSignature  { get; private set; }
        public int[]                    Version           { get; private set; }
        public bool                     IsMeta            { get; }
        public string                   BlockDirectory    { get => FolderPath; }
        public XMFBlock[]               BlockEntry        { get; private set; }

        public uint BlockCount
        {
            get
            {
                if (BlockEntry == null) return 0;
                return (uint)BlockEntry.Length;
            }
        }

        public long BlockTotalSize
        {
            get
            {
                return BlockEntry?.Sum(x => x.Size) ?? 0;
            }
        }
    }
}
