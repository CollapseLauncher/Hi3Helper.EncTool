using Hi3Helper.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hi3Helper.EncTool.Parser
{
    public sealed partial class XMFParser
    {
        internal const byte _signatureLength = 0x10;    // 16
        internal const byte _versioningLength = 0x04;   // 4
        internal const byte _allowedMinVersion = 0x00;  // 0
        internal const byte _allowedMaxVersion = 0x40;  // 64
        internal string _xmfPath;
        internal static string _folderPath;
        private bool _isMeta;

        public XMFParser(string path, Stream xmfStream, bool isMeta)
        {
            _xmfPath = path;
            _isMeta = isMeta;

            ParseMetadata(xmfStream, isMeta);

#if DEBUG
            Console.WriteLine($"XMF File Loaded   : {_xmfPath}");
            Console.WriteLine($"Folder Path       : {_folderPath}");
            Console.WriteLine($"Version Signature : {HexTool.BytesToHexUnsafe(VersionSignature)}");
            Console.WriteLine($"Manifest Version  : {string.Join('.', Version)}");
            Console.WriteLine($"Block Count       : {BlockCount}");
            Console.WriteLine($"Block Total Size  : {BlockTotalSize} bytes");
            Console.WriteLine($"Asset Entries     : {BlockEntry.Sum(x => x.AssetCount)}");
#endif
        }

        public Dictionary<string, uint> BlockIndexCatalog { get; private set; }

        public byte[] VersionSignature { get; private set; }

        public int[] Version { get; private set; }

        public string BlockDirectory { get => _folderPath; }

        public XMFBlock[] BlockEntry { get; private set; }

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
                if (BlockEntry == null) return 0;
                return BlockEntry.Sum(x => x.Size);
            }
        }
    }
}
