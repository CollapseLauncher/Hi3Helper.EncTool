using Hi3Helper.Data;
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
    /* Collapse Asset Index V2 data spec
     * +--------------+----------------+-------------------------+-----------------+-------------------------------------------+------------------+
     * | Magic (long) | Version (byte) | Compression flag (byte) | Data Pos. (int) | Additional header information (... bytes) | Data (... bytes) |
     * +--------------+----------------+-------------------------+-----------------+-------------------------------------------+------------------+
     * Size remarks:
     *  long    -> 8 bytes
     *  int     -> 4 bytes
     *  byte    -> 1 byte
     * 
     * Compression flag remarks:
     *  0       -> No compression
     *  1       -> Deflate,
     *  2       -> GZip,
     *  3       -> Brotli
     * 
     * Note:
     *  The additional header information size may vary depending on the case so if you need to seek into the position pf
     *  the data, you may need to seek into the position provided by the "Data Pos.".
     *  
     *  The data might be compressed. You may refer to the flag provided by "Compression flag".
     */
    public class AssetIndexV2
    {
        private const ulong HeaderMagic = 7310310183885631299; // Collapse
        private const byte  Version     = 2;

        public void Serialize(string path, string outPath, CompressionFlag compressionType)
        {
            using FileStream fileStream   = File.OpenRead(path);
            using FileStream outputStream = File.Create(outPath);
            Serialize(fileStream, outputStream, compressionType);
        }

        public void Serialize(Stream input, Stream output, CompressionFlag compressionType)
        {
            // Assign the JSON input stream into stream reader
            using StreamReader streamReader = new(input, Encoding.UTF8, true, -1, true);

            // Deserialize lines of the JSON into the list
            List<PkgVersionProperties> versionList = [];
            while (!streamReader.EndOfStream)
            {
                string line = streamReader.ReadLine()!;
                versionList.Add((PkgVersionProperties)JsonSerializer.Deserialize(line, typeof(PkgVersionProperties), JsonContext.Default)!);
            }

            // Serialize the list into binary format
            SerializeToBinary(output, versionList, compressionType);
        }

        private unsafe void SerializeToBinary(Stream outputStream, List<PkgVersionProperties> versionList, CompressionFlag compressionType)
        {
            using BinaryWriter rawBinaryWriter = new(outputStream);

            // Writing out basic header properties (Header magic and the version)
            rawBinaryWriter.Write(HeaderMagic);
            rawBinaryWriter.Write(Version);
            rawBinaryWriter.Write((byte)compressionType);

            // Save the information position for storing the data position offset later
            int dataPosHeaderPos = (int)outputStream.Position;
            outputStream.Position += sizeof(int);

            // Writes additional header information
            rawBinaryWriter.Write(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            // Run override method to write potential additional header information
            WriteAdditionalHeaderInfo(outputStream);

            // Seek to the dataPos header position to write the dataPos offset
            int dataPos = (int)outputStream.Position;
            outputStream.Position = dataPosHeaderPos;
            rawBinaryWriter.Write(dataPos);
            outputStream.Position = dataPos;

            // Assign writer for data
            using Stream writeOutStream = compressionType switch
            {
                CompressionFlag.Deflate => new DeflateStream(outputStream, CompressionLevel.SmallestSize),
                CompressionFlag.Brotli => new BrotliStream(outputStream, CompressionLevel.SmallestSize),
                CompressionFlag.GZip => new GZipStream(outputStream, CompressionLevel.SmallestSize),
                _ => outputStream
            };
            using BinaryWriter binaryWriter = new(writeOutStream);

            // Write the count of the versionList (AssetProperty struct)
            binaryWriter.Write(versionList.Count);

            // If the count is not 0, then write the list into data
            if (versionList.Count > 0)
            {
                // Get the size of the struct and assign the size of the output buffer
                int sizeOf = Marshal.SizeOf<AssetProperty>();
                sizeOf *= versionList.Count;
                byte[] bufferOut = new byte[sizeOf];

                // Get the struct array
                AssetProperty[] assets = versionList
                    .Select(x =>
                    {
                        var assetProperty = new AssetProperty
                        {
                            size = (uint)x.fileSize
                        };

                        // Convert the MD5 hash into byte array and copy it into the struct
                        Span<byte> hashPtr = new(assetProperty.hash, 16);
                        byte[] md5Bytes = HexTool.HexToBytesUnsafe(x.md5);

                        md5Bytes.CopyTo(hashPtr);

                        return assetProperty;
                    }).ToArray();

                // Serialize the AssetProperty struct array into the output buffer
                ConverterTool.TrySerializeStruct<AssetProperty>(bufferOut, out int read, assets.AsSpan());

                // Write the size of the output buffer and output buffer itself
                binaryWriter.Write(sizeOf);
                writeOutStream.Write(bufferOut);

                // Get the path array
                string[] paths = versionList.Select(x => x.remoteName).ToArray()!;
                // Get the size of the path array
                uint stringBytesLen = (uint)paths.Sum(x => (uint)x.Length + 1);
                // Assign the size of the string output buffer and write the size into the writer
                Span<byte> stringBuffer = new byte[stringBytesLen];
                binaryWriter.Write(stringBytesLen);

                // Convert the string path to \0-terminated string and write it into the string output buffer
                int pos = 0;
                foreach (string path in paths)
                {
                    Encoding.UTF8.GetBytes(path, stringBuffer[pos..]);
                    pos += (path?.Length ?? 0) + 1;
                }

                // Write the string output buffer
                writeOutStream.Write(stringBuffer);
            }

            // Run override method to write potential additional/custom data
            WriteAdditionalData(writeOutStream);
        }

        protected virtual void WriteAdditionalHeaderInfo(Stream outputStream) { }
        protected virtual void WriteAdditionalData(Stream outputStream) { }

        public List<PkgVersionProperties> Deserialize(string path, out DateTime timestamp)
        {
            using FileStream fileStream = File.OpenRead(path);
            return Deserialize(fileStream, out timestamp);
        }

        public List<PkgVersionProperties> Deserialize(Stream stream, out DateTime timestamp)
        {
            // Assign the stream into raw binary reader
            using BinaryReader rawBinaryReader = new(stream);

            // Get the magic and check if the value is match
            ulong magic = rawBinaryReader.ReadUInt64();
            if (HeaderMagic != magic) throw new InvalidDataException($"Deserializer is expecting a magic: {HeaderMagic} but get {magic} instead. Invalid format!");

            // Get the version and check if the value is match
            byte version = rawBinaryReader.ReadByte();
            if (Version != version) throw new InvalidDataException($"Version of this asset index is {version} while deserializer is expecting version: {Version}. Unsupported version!");

            // Then start deserializing the binary into array
            return DeserializeFromBinary(stream, rawBinaryReader, out timestamp);
        }

        private unsafe List<PkgVersionProperties> DeserializeFromBinary(Stream fileStream, BinaryReader rawBinaryReader, out DateTime timestampUtc)
        {
            // Get the compression flag
            byte compressionByte = rawBinaryReader.ReadByte();
            if (!Enum.IsDefined(typeof(CompressionFlag), compressionByte)) throw new FormatException($"Compression format: {compressionByte} is not supported!");
            CompressionFlag compressionType = (CompressionFlag)compressionByte;

            // Get the data position
            int dataPos = rawBinaryReader.ReadInt32();

            // Read the additional header information
            long unixTimestamp = rawBinaryReader.ReadInt64();
            timestampUtc = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;

            // Run override method to read potential additional header information
            int readHeadInfo = ReadAdditionalHeaderInfo(fileStream);

            // Seek the position of the fileStream and try to get the data
            if (fileStream.CanSeek)
                fileStream.Position = dataPos;
            else
            {
                // 22 is the total of length from the first initial read
                int toSkip = dataPos - (22 + readHeadInfo);
                // If the skip length is more than 0, then seek by reading the rest of the data into dummy buffer
                if (toSkip > 0)
                {
                    // Read the data into dummy buffer
                    byte[] dummy = new byte[toSkip];
                    fileStream.ReadExactly(dummy, 0, toSkip);
                }
            }

            using Stream streamInput = compressionType switch
            {
                CompressionFlag.Deflate => new DeflateStream(fileStream, CompressionMode.Decompress),
                CompressionFlag.GZip => new GZipStream(fileStream, CompressionMode.Decompress),
                CompressionFlag.Brotli => new BrotliStream(fileStream, CompressionMode.Decompress),
                _ => fileStream
            };
            using BinaryReader binaryReader = new(streamInput);

            // Read the asset count
            int assetCount = binaryReader.ReadInt32();
            List<PkgVersionProperties> pkgReturn = [];

            // If the count is not 0, then read the data
            if (assetCount > 0)
            {
                // Read the size of the buffer
                int sizeOf = binaryReader.ReadInt32();

                // Assign the buffer with the size provided and read the data from the stream
                byte[] bufferOut = new byte[sizeOf];
                streamInput.ReadExactly(bufferOut, 0, bufferOut.Length);
                // Try to deserialize the buffer into AssetProperty struct array
                ConverterTool.TryDeserializeStruct(bufferOut, assetCount, out AssetProperty[] returnStruct);

                // Get the size of the string buffer, assign the size to the buffer and read the data from the stream
                uint stringBytes = binaryReader.ReadUInt32();
                bufferOut = new byte[stringBytes];
                streamInput.ReadExactly(bufferOut, 0, bufferOut.Length);

                // Try to get the string array from the string buffer
                ConverterTool.GetListOfPaths(bufferOut, out string[] returnString, assetCount);

                // Convert all the data provided by the string array and the AssetProperty struct array into PkgVersionProperties array
                for (ushort i = 0; i < assetCount; i++)
                {
                    fixed (byte* hashPtr = &returnStruct[i].hash[0])
                    {
                        Span<byte> hashSpan = new(hashPtr, 16);
                        ref ulong lowBytes = ref MemoryMarshal.AsRef<ulong>(hashSpan[8..]);
                        if (lowBytes == 0)
                        {
                            hashSpan = hashSpan[..8];
                        }

                        pkgReturn.Add(new PkgVersionProperties
                        {
                            remoteName = returnString[i],
                            md5 = HexTool.BytesToHexUnsafe(hashSpan),
                            fileSize = returnStruct[i].size
                        });
                    }
                }
            }

            // Run override method to read potential additional/custom data
            ReadAdditionalData(streamInput);

            // Return the PkgVersionProperties array
            return pkgReturn;
        }

        protected virtual int ReadAdditionalHeaderInfo(Stream inputStream) { return 0; }
        protected virtual int ReadAdditionalData(Stream inputStream) { return 0; }
    }
}
