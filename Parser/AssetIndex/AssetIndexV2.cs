using Hi3Helper.Data;
using Hi3Helper.EncTool.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Hi3Helper.EncTool.Parser.AssetIndex
{
    public static class AssetIndexV2
    {
        private const ulong HeaderMagic = 7310310183885631299; // Collapse
        private const byte Version = 2;

        public static void Serialize(string path, string outPath, bool isCompressed)
        {
            using FileStream fileStream = File.OpenRead(path);
            using FileStream outputStream = File.Create(outPath);
            using StreamReader streamReader = new StreamReader(fileStream);

            List<PkgVersionProperties> versionList = new List<PkgVersionProperties>();

            while (!streamReader.EndOfStream)
            {
                string line = streamReader.ReadLine()!;
                versionList.Add((PkgVersionProperties)JsonSerializer.Deserialize(line, typeof(PkgVersionProperties), JSONContext.Default)!);
            }

            SerializeToBinary(outputStream, versionList, isCompressed);
        }

        private static void SerializeToBinary(Stream outputStream, List<PkgVersionProperties> versionList, bool isCompressed)
        {
            using BinaryWriter rawBinaryWriter = new BinaryWriter(outputStream);

            rawBinaryWriter.Write(HeaderMagic);
            rawBinaryWriter.Write(Version);
            rawBinaryWriter.Write(isCompressed);

            using Stream writeOutStream = isCompressed ? new BrotliStream(outputStream, CompressionLevel.SmallestSize) : outputStream;
            using BinaryWriter binaryWriter = new BinaryWriter(writeOutStream);

            binaryWriter.Write((ushort)versionList.Count);

            int sizeOf = Marshal.SizeOf<AssetProperty>();
            sizeOf = sizeOf * versionList.Count;
            byte[] bufferOut = new byte[sizeOf];
            AssetProperty[] assets = versionList
                .Select(x => new AssetProperty
                {
                    hash = HexTool.HexToBytesUnsafe(x.md5!),
                    size = (uint)x.fileSize
                }).ToArray();

            ConverterTool.TrySerializeStruct(assets, bufferOut, out int read);
            binaryWriter.Write(sizeOf);
            writeOutStream.Write(bufferOut);

            string[] paths = versionList.Select(x => x.remoteName).ToArray()!;
            uint stringBytesLen = (uint)paths.Sum(x => (uint)x.Length + 1);
            binaryWriter.Write(stringBytesLen);

            Span<byte> stringBuffer = new byte[stringBytesLen];

            int pos = 0;
            foreach (string? path in paths)
            {
                byte[] strBuffer = new byte[(path?.Length ?? 0) + 1];
                Encoding.UTF8.GetBytes(path, stringBuffer.Slice(pos));
                pos += strBuffer.Length;
            }
            writeOutStream.Write(stringBuffer);
        }

        public static PkgVersionProperties[] Deserialize(string path)
        {
            using FileStream fileStream = File.OpenRead(path);
            return Deserialize(fileStream);
        }

        public static PkgVersionProperties[] Deserialize(Stream stream)
        {
            using BinaryReader rawStreamReader = new BinaryReader(stream);

            ulong magic = rawStreamReader.ReadUInt64();
            if (HeaderMagic != magic) throw new InvalidDataException($"Deserializer is expecting a magic: {HeaderMagic} but get {magic} instead. Invalid format!");

            byte version = rawStreamReader.ReadByte();
            if (Version != version) throw new InvalidDataException($"Version of this asset index is {version} while deserializer is expecting version: {Version}. Unsupported version!");

            return DeserializeFromBinary(stream);
        }

        private static unsafe PkgVersionProperties[] DeserializeFromBinary(Stream fileStream)
        {
            using BinaryReader rawBinaryReader = new BinaryReader(fileStream);
            bool isCompressed = rawBinaryReader.ReadBoolean();

            using Stream streamInput = isCompressed ? new BrotliStream(fileStream, CompressionMode.Decompress) : fileStream;
            using BinaryReader binaryReader = new BinaryReader(streamInput);

            ushort assetCount = binaryReader.ReadUInt16();
            uint sizeOf = binaryReader.ReadUInt32();

            byte[] bufferOut = new byte[sizeOf];
            streamInput.ReadExactly(bufferOut, 0, bufferOut.Length);
            ConverterTool.TryDeserializeStruct(bufferOut, assetCount, out AssetProperty[] returnStruct);

            uint stringBytes = binaryReader.ReadUInt32();
            bufferOut = new byte[stringBytes];
            streamInput.ReadExactly(bufferOut, 0, bufferOut.Length);

            ConverterTool.GetListOfPaths(bufferOut, out string[] returnString, assetCount);

            PkgVersionProperties[] pkgReturn = new PkgVersionProperties[assetCount];
            for (ushort i = 0; i < assetCount; i++)
            {
                pkgReturn[i] = new PkgVersionProperties()
                {
                    remoteName = returnString[i],
                    md5 = HexTool.BytesToHexUnsafe(returnStruct[i].hash),
                    fileSize = returnStruct[i].size
                };
            }

            return pkgReturn;
        }
    }
}
