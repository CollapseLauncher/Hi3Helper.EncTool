using Hi3Helper.UABT;
using Hi3Helper.UABT.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
// ReSharper disable CommentTypo
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMember.Global

namespace Hi3Helper.EncTool.Parser
{
    public sealed partial class XMFParser
    {
        private void TryCheckXmfPath()
        {
            // If the path does exist as a file, then set _folderPath.
            if (File.Exists(XmfPath))
            {
                FolderPath = Path.GetDirectoryName(XmfPath);
                return;
            }

            // Try find XMF file by enumerating the content of the given path as a directory.
            if (!Directory.Exists(XmfPath))
            {
                throw new DirectoryNotFoundException($"You're trying to load XMF from a directory in this path: \"{XmfPath}\" and it doesn't exist.");
            }

            // Try to enumerate XMF file from the given path.
            string assumedXmfPath = Directory.EnumerateFiles(XmfPath, "Blocks.xmf", SearchOption.TopDirectoryOnly).FirstOrDefault();

            XmfPath    = assumedXmfPath ?? throw new
                // If it doesn't, then...
                FileNotFoundException($"XMF file in this path: \"{XmfPath}\" doesn't exist or the directory with the path given has no XMF file inside it.");
            FolderPath = Path.GetDirectoryName(assumedXmfPath);
        }

        private void ParseMetadata(Stream xmfStream, bool isMeta)
        {
            // Read XMF with Endianess-aware BinaryReader
            using EndianBinaryReader reader = new(xmfStream);
            // Start read the header of the XMF file.
            ReadHeader(reader);

            // Start read the metadata including block info and asset indexes.
            ReadMetadata(reader, isMeta);

            // Finalize by creating catalog for block lookup as hash name and index.
            // This will make searching process for the block easier.
            CreateBlockIndexCatalog(BlockCount);
        }

        private void CreateBlockIndexCatalog(uint count)
        {
            // Initialize block lookup catalog and start adding hash as key and index as value.
            BlockIndexCatalog = new Dictionary<string, uint>();
            for (uint i = 0; i < count; i++)
            {
                BlockIndexCatalog.Add(BlockEntry[i].BlockName, i);
            }
        }

        internal static byte[] ReadSignature(EndianBinaryReader reader)
        {
            // Switch to Little-endian on reading the header.
            reader.Endian = EndianType.LittleEndian;
            return XMFBlock.TryReadMD5HashOrOther64(reader);
        }

        internal static int[] ReadVersion(EndianBinaryReader reader)
        {
            // Read block version.
            int[] ver = new int[VersioningLength];
            for (int i = 0; i < VersioningLength; i++)
            {
                // If the offset is more than 2, then read the revision number as byte.
                if (i > 2)
                {
                    ver[i] = reader.ReadByte();
                }
                // Else, read it as int.
                else
                {
                    ver[i] = reader.ReadInt32();
                }

                if (ver[i] < AllowedMinVersion || ver[i] > AllowedMaxVersion)
                {
                    throw new InvalidDataException($"Header version on array: {i} is invalid with value: {ver[i]}. The allowed range is: ({AllowedMinVersion} - {AllowedMaxVersion})");
                }
            }

            return ver;
        }

        private void ReadHeader(EndianBinaryReader reader)
        {
            // Read signature (32 bytes).
            VersionSignature = ReadSignature(reader);

            // Skip unknown field
            _ = reader.ReadInt32();

            // Read block version.
            Version = ReadVersion(reader);

            // Switch to Big-endian.
            reader.Endian = EndianType.BigEndian;

            // Allocate the size of Block array.
            BlockEntry = new XMFBlock[reader.ReadUInt32()];
        }

        private void ReadMetadata(EndianBinaryReader reader, bool isMeta)
        {
            // Initialize the XMFBlock instance to the BlockEntry array.
            // At the same time, the XMFBlock will read the metadata section of the block.
            for (int i = 0; i < BlockEntry.Length; i++)
            {
                BlockEntry[i] = new XMFBlock(reader, isMeta);
            }
        }

        /// <summary>
        /// Get a block by using the hash string of the block.<br/>
        /// If you can't figure out the hash you want to get, Use <c>EnumerateBlockHashString()</c> to get the block hashes.
        /// </summary>
        /// <param name="hash">Given hash string of the block.</param>
        /// <returns>An instance of <c>XMFBlock</c> that contains an information about the block.</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public XMFBlock GetBlockByHashString(string hash)
        {
            // Check if the block catalog contains the key name.
            // If not, then throw.
            if (!BlockIndexCatalog.TryGetValue(hash, out var value))
            {
                throw new KeyNotFoundException($"Block: {hash} doesn't exist!");
            }

            // If found, then return the block entry.
            return BlockEntry[value];
        }
    }
}
