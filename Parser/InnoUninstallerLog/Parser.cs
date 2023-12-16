using Hi3Helper.Data;
using LibISULR;
using LibISULR.Records;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace Hi3Helper.EncTool.Parser.InnoUninstallerLog
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Size = 0x1C0)]
    public struct TUninstallLogHeader
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x40)]
        public string ID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x80)]
        public string AppId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x80)]
        public string AppName;
        public int Version;
        public int RecordsCount;
        public int FileEndOffset;
        public int UninstallFlags;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x6C)]
        public byte[] ReservedHeaderBytes;
        public uint Crc;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0xC)]
    public struct TUninstallCrcHeader
    {
        public uint Size, NotSize, Crc;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0xA, Pack = 1)]
    public struct TUninstallFileRec
    {
        public RecordType TUninstallRecTyp;
        public int ExtraData;
        public uint DataSize;
    }

    public class InnoUninstLog : IDisposable
    {
        public TUninstallLogHeader? Header { get; set; }
        public List<BaseRecord> Records { get; set; }

        public void Dispose()
        {
            Header = null;
            Records.Clear();
        }

        public static InnoUninstLog Load(string path, bool skipCrcCheck = false)
        {
            // Load the file as stream
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Load(stream, skipCrcCheck);
            }
        }

        public void Save(string path)
        {
            // Save the record to file path by using Stream
            using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                Save(stream);
            }
        }

        public static InnoUninstLog Load(Stream stream, bool skipCrcCheck = false)
        {
            // SANITY CHECK: Check the stream if it's readable
            if (!stream.CanRead) throw new ArgumentException("Stream must be readable!", "stream");

            // Read header structures
            ReadTUninstallLogHeader(stream, skipCrcCheck, out TUninstallLogHeader headerStruct);

            // Initialize the result class
            InnoUninstLog result = new InnoUninstLog
            {
                Header = headerStruct,
                Records = new List<BaseRecord>()
            };

            // Assign the stream to the reader and leave it open
            using CrcBridgeStream crcStream = new CrcBridgeStream(stream, true, skipCrcCheck);

            // Start reading records and its header
            int start = 0;
        ReadHeaderRecords:
            ReadTStructure(crcStream, out TUninstallFileRec uninstallFileRec); // Read the header and load it into the struct
            byte[] buffer = new byte[uninstallFileRec.DataSize]; // Initialize the buffer for the data
            crcStream.ReadExactly(buffer); // Then read the crc stream to the buffer

            // Try create the record and add it into the record list
            result.Records.Add(RecordFactory.CreateRecord(uninstallFileRec.TUninstallRecTyp, uninstallFileRec.ExtraData, buffer));
            if (++start < headerStruct.RecordsCount) goto ReadHeaderRecords; // Do loop if the record still remains

            // Return the list
            return result;
        }

        public void Save(Stream stream)
        {
            // SANITY CHECK: Check the stream if it's writable
            if (!stream.CanWrite) throw new ArgumentException("Stream must be writable!", "stream");

            // Write the header
            WriteTUninstallLogHeader(stream, Header.Value);

            // Borrowing the write buffer with size (at least) 128 KB from ArrayPool<T>
            byte[] writeBuffer = ArrayPool<byte>.Shared.Rent(128 << 10);
            try
            {
                // Initialize the CrcBridgeStream in write mode
                using (CrcBridgeStream crcStream = new CrcBridgeStream(stream, true, false, true))
                {
                    // Get the size of the record header (TUninstallLogHeader)
                    // Note: The size should at least 10 bytes expected
                    int headerSizeOf = Marshal.SizeOf<TUninstallFileRec>();
                    // Iterate the records
                    foreach (BaseRecord record in Records)
                    {
                        // Set the offset of the buffer and update the content into the write buffer
                        int offset = 0;
                        // Move and start buffer forward (based on headerSizeOf) to reserve space for TUninstallFileRec
                        uint dataLen = (uint)record.UpdateContent(writeBuffer.AsSpan(headerSizeOf));

                        // Initialize the record header (TUninstallFileRec) and try serialize it into buffer
                        TUninstallFileRec dataRec = new TUninstallFileRec
                        {
                            DataSize = dataLen,
                            ExtraData = record.FlagsNum,
                            TUninstallRecTyp = record.Type
                        };
                        ConverterTool.TrySerializeStruct(dataRec, ref offset, writeBuffer);

                        // Write the buffer with the size of headerSizeOf and dataLen
                        crcStream.Write(writeBuffer.AsSpan(0, headerSizeOf + (int)dataLen));
                    }
                    // Finalize the crc block inside of CrcBridgeStream
                    crcStream.FinalizeBlock();
                }
            }
            catch { throw; }
            finally
            {
                // Return the write buffer to ArrayPool
                ArrayPool<byte>.Shared.Return(writeBuffer);
            }
        }

        private static void WriteTUninstallLogHeader(Stream stream, TUninstallLogHeader header)
        {
            int i = 0; // Dummy
            int sizeOf = Marshal.SizeOf<TUninstallLogHeader>(); // Get the size of the struct

            // Allocate buffer from pool
            byte[] structBuffer = ArrayPool<byte>.Shared.Rent(sizeOf);
            try
            {
                // Serialize the struct into bytes
                ConverterTool.TrySerializeStruct(header, ref i, structBuffer);

                // Get the hash
                uint crc32Header = Crc32.HashToUInt32(structBuffer.AsSpan(0, sizeOf - 4));
                MemoryMarshal.Write(structBuffer.AsSpan(sizeOf - 4), crc32Header);

                // Write the buffer into stream
                stream.Write(structBuffer, 0, sizeOf);
            }
            catch { throw; }
            finally
            {
                // Return the pool buffer
                ArrayPool<byte>.Shared.Return(structBuffer);
            }
        }

        private static void ReadTUninstallLogHeader(Stream stream, bool skipCrcCheck, out TUninstallLogHeader header)
        {
            int i = 0; // Dummy
            int sizeOf = Marshal.SizeOf<TUninstallLogHeader>(); // Get the size of the struct

            // Allocate buffer from pool
            byte[] structBuffer = ArrayPool<byte>.Shared.Rent(sizeOf);
            try
            {
                // Read the stream and store into buffer
                stream.ReadExactly(structBuffer, 0, sizeOf);

                // Deserialize the struct
                if (!ConverterTool.TryDeserializeStruct<TUninstallLogHeader>(structBuffer, ref i, out header))
                    throw new InvalidDataException("Header struct is invalid!");

                // Try calculate the Crc32 hash of the header and throw if not match
                uint crc32Header = Crc32.HashToUInt32(structBuffer.AsSpan(0, sizeOf - 4));
                if (!skipCrcCheck && crc32Header != header.Crc)
                    throw new DataException($"Header Crc32 isn't match! Getting {crc32Header} while expecting {header.Crc}");
            }
            finally
            {
                // Return the pool buffer
                ArrayPool<byte>.Shared.Return(structBuffer);
            }
        }

        internal static void ReadTStructure<T>(Stream stream, out T header)
            where T : struct
        {
            int i = 0; // Dummy
            int sizeOf = Marshal.SizeOf<T>(); // Get the size of the struct

            // Allocate buffer from pool
            byte[] structBuffer = ArrayPool<byte>.Shared.Rent(sizeOf);
            try
            {
                // Read the stream and store into buffer
                stream.ReadExactly(structBuffer, 0, sizeOf);

                // Deserialize the struct
                if (!ConverterTool.TryDeserializeStruct<T>(structBuffer, ref i, out header))
                    throw new InvalidDataException("Header struct is invalid!");
            }
            finally
            {
                // Return the pool buffer
                ArrayPool<byte>.Shared.Return(structBuffer);
            }
        }
    }
}
