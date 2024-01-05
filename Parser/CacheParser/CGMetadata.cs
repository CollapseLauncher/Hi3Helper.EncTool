using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;

namespace Hi3Helper.EncTool.Parser.Cache
{
    [JsonConverter(typeof(JsonStringEnumConverter<CGPCKType>))]
    public enum CGPCKType : byte // TypeDefIndex: 34029
    {
        MustHave = 0,
        WithAll = 1,
        Needing = 2,
        All = 100
    }

    internal struct CgGroupID
    {
        internal int ID;
        internal int FileOffset;
    }

    internal struct TextID
    {
        internal int hash;
    }

    public class CGMetadata
    {
        private static List<CgGroupID> _groupID = new List<CgGroupID>();
        private static Encoding _encoding { get; set; }

        public int CgID { get; set; }
        public byte UnlockType { get; set; }
        public uint UnlockCondition { get; set; }
        public int LevelIDBegin { get; set; }
        public int LevelIDEnd { get; set; }
        public int CgCategory { get; set; }
        public int CgSubCategory { get; set; }
        public int[] CgGroupID { get; set; }
        public int WikiCgScore { get; set; }
        public bool InitialUnlock { get; set; }
        public string CgPath { get; set; }
        public string CgIconSpritePath { get; set; }
        internal TextID CgLockHint { get; set; }
        public bool InStreamingAssets { get; set; }
        public int CgPlayMode { get; set; }
        public string CgExtraKey { get; set; }
        public long FileSize { get; set; }
        public CGPCKType PckType { get; set; }
        public string DownloadLimitTime { get; set; }
        public uint AppointmentDownloadScheduleID { get; set; }
        public int Unk1 { get; set; }
        public short Unk2 { get; set; }
        public short Unk3 { get; set; }
        public byte Unk4 { get; set; }
        public int Unk5 { get; set; }

        public static CGMetadata[] GetArray(Stream stream, Encoding encoding)
        {
            // Set the encoding
            _encoding = encoding;

            // Get the entry count
            int entryCount = GetEntryCount(stream);

            // Initialize the return value of the instance
            CGMetadata[] entries = new CGMetadata[entryCount];

            // Assign the data to the return value
            for (int i = 0; i < entryCount; i++) entries[i] = Deserialize(stream, i);

            // Return the value
            return entries;
        }

        public static List<CGMetadata> GetList(Stream stream, Encoding encoding)
        {
            // Set the encoding
            _encoding = encoding;

            // Get the entry count
            int entryCount = GetEntryCount(stream);

            // Initialize the return value of the instance
            List<CGMetadata> entries = new List<CGMetadata>();

            // Assign the data to the return value
            for (int i = 0; i < entryCount; i++) entries.Add(Deserialize(stream, i));

            // Return the value
            return entries;
        }

        public static IEnumerable<CGMetadata> Enumerate(Stream stream, Encoding encoding)
        {
            // Set the encoding
            _encoding = encoding;

            // Get the entry count
            int entryCount = GetEntryCount(stream);

            // Assign the data to the return value and yield it
            for (int i = 0; i < entryCount; i++) yield return Deserialize(stream, i);
        }

        private static int GetEntryCount(Stream stream)
        {
            // Skip the file size information and skip to the entry count
            stream.Position += 4;
            int entryCount = ReadInt32(stream);

            // Read GroupID and Offsets
            ReadGroupIDAndOffsets(stream, entryCount);

            return entryCount;
        }

        private static void ReadGroupIDAndOffsets(Stream stream, int readCount)
        {
            // Clear the _groupID cache
            _groupID.Clear();
            int toRead = readCount * 4;
            int readOffset = 0;

            byte[] buffer1 = ArrayPool<byte>.Shared.Rent(toRead);
            byte[] buffer2 = ArrayPool<byte>.Shared.Rent(toRead);
            stream.ReadExactly(buffer1, 0, toRead);
            stream.ReadExactly(buffer2, 0, toRead);
            Span<byte> bufferGroupID = buffer1;
            Span<byte> bufferOffset = buffer2;

            try
            {
            ReadGroupIDAndOffset_Label:
                // Read the GroupID and offsets number
                int groupID = BinaryPrimitives.ReadInt32LittleEndian(bufferGroupID.Slice(readOffset));
                int offsets = BinaryPrimitives.ReadInt32LittleEndian(bufferOffset.Slice(readOffset));
                readOffset += 4;
                CgGroupID groupIDstruct = new CgGroupID { ID = groupID, FileOffset = offsets };
                _groupID.Add(groupIDstruct);
                if (readOffset < toRead)
                    goto ReadGroupIDAndOffset_Label;
            }
            catch { throw; }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer1);
                ArrayPool<byte>.Shared.Return(buffer2);
            }
        }

        private static CGMetadata Deserialize(Stream stream, int groupIDIndex)
        {
            CGMetadata entry = new CGMetadata();

            stream.Position = _groupID[groupIDIndex].FileOffset;
            entry.CgID = _groupID[groupIDIndex].FileOffset;
#if !DEBUG
            Span<byte> unk1_2 = stackalloc byte[4];
#else
            Span<byte> unk1_2 = new byte[4];
#endif
            _ = stream.Read(unk1_2);
            entry.Unk1 = BinaryPrimitives.ReadInt32LittleEndian(unk1_2);
            entry.Unk2 = BinaryPrimitives.ReadInt16LittleEndian(unk1_2);
            entry.Unk3 = BinaryPrimitives.ReadInt16LittleEndian(unk1_2.Slice(2));

            entry.UnlockType = ReadByte(stream);
            entry.UnlockCondition = ReadUInt32(stream);
            entry.LevelIDBegin = ReadInt32(stream);
            entry.LevelIDEnd = ReadInt32(stream);
            entry.CgCategory = ReadInt32(stream);
            entry.CgSubCategory = ReadInt32(stream);

            entry.WikiCgScore = ReadInt32(stream);
            entry.InitialUnlock = ReadBoolean(stream);

            int ptrToCgPath = ReadInt32(stream);
            int ptrToCgIconSpritePath = ReadInt32(stream);
            int ptrToPckType = ReadInt32(stream);
            int ptrToUnk1 = ReadInt32(stream);

            entry.InStreamingAssets = (ptrToUnk1 > 8 ? ReadInt32(stream) : ptrToUnk1) == 1;
            entry.CgPlayMode = ReadInt32(stream);

            int ptrToCgExtraKey = ReadInt32(stream);

            // Starting from 7.2, we have another unknown value to ptrToCgExtraKey, which is actually being used
            // for getting the status of FileSize type whether it's an int or a long.
            // If the ptrToUnk1 >= _groupID[groupIDIndex].FileOffset, then determine there's an additional
            // data (which is a Low bit for the FileSize) and re-read the actual value for ptrToUnk1.
            // Otherwise, set isSizeALong = false and skip from reading the additional data.
            bool isSizeALong = !(ptrToUnk1 < _groupID[groupIDIndex].FileOffset);
            if (ptrToCgExtraKey < _groupID[groupIDIndex].FileOffset)
                ptrToCgExtraKey = ReadInt32(stream);

            // For the sake of compatibility with both 7.2 and 7.1 metadata, we need to check the
            // size type because starting from 7.2, the FileSize is actually a long while <= 7.1, it's an int.
            // We still unsure since the function provided in the UserAssembly is kinda fucced up.
            entry.FileSize = isSizeALong ? ReadInt64(stream) : ReadInt32(stream);

            entry.PckType = (CGPCKType)ReadByte(stream);
            int ptrToDownloadLimitTime = ReadInt32(stream);

            entry.AppointmentDownloadScheduleID = ReadUInt32(stream);

            int CgGroupIDCount = ReadInt32(stream);
            entry.CgGroupID = new int[CgGroupIDCount];
            for (int i = 0; i < CgGroupIDCount; i++)
            {
                entry.CgGroupID[i] = ReadInt32(stream);
            }

            _ = stream.ReadByte();
            int partSizeToRead = ReadInt32(stream);
            stream.Position = ptrToCgPath;
            entry.CgPath = ReadString(stream);
            entry.CgIconSpritePath = ReadString(stream);

            entry.Unk4 = (byte)stream.ReadByte();
            entry.CgLockHint = new TextID { hash = ReadInt32(stream) };

            stream.Position = ptrToCgExtraKey;
            entry.CgExtraKey = ReadString(stream);

            stream.Position = ptrToDownloadLimitTime;
            entry.DownloadLimitTime = ReadString(stream);
            entry.Unk5 = ReadInt32(stream);

#if DEBUG
            Console.WriteLine($"CG [T: {entry.PckType}][BuiltIn: {entry.InStreamingAssets}]: {entry.CgPath} [{entry.FileSize} b] [ID: {entry.CgID}] [Category: {entry.CgSubCategory}] [Unk5: {entry.Unk5}]");
#endif

            return entry;
        }

        private static long ReadInt64(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[8];
            stream.Read(buffer);
            return BinaryPrimitives.ReadInt64LittleEndian(buffer);
        }

        private static ulong ReadUInt64(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[8];
            stream.Read(buffer);
            return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        }

        private static int ReadInt32(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[4];
            stream.Read(buffer);
            return BinaryPrimitives.ReadInt32LittleEndian(buffer);
        }

        private static uint ReadUInt32(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[4];
            stream.Read(buffer);
            return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        private static short ReadInt16(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[2];
            stream.Read(buffer);
            return BinaryPrimitives.ReadInt16LittleEndian(buffer);
        }

        private static ushort ReadUInt16(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[2];
            stream.Read(buffer);
            return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        }

        private static string ReadString(Stream stream)
        {
            ushort len = ReadUInt16(stream);
            Span<byte> strArr = stackalloc byte[len];
            stream.Read(strArr);
            return _encoding.GetString(strArr);
        }

        private static byte ReadByte(Stream stream) => (byte)stream.ReadByte();

        private static bool ReadBoolean(Stream stream) => ReadByte(stream) == 1;
    }
}
