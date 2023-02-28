using Hi3Helper.UABT;
using Hi3Helper.UABT.Binary;
using System;
using System.Collections.Generic;
using System.IO;

namespace Hi3Helper.EncTool.CacheParser
{
    public class CgHash
    {
        public int Hash { get; set; }
    }

    public class CGMetadata
    {
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
        public CgHash CgLockHint { get; set; }
        public int InStreamingAssets { get; set; }
        public int CgPlayMode { get; set; }
        public string CgExtraKey { get; set; }
        public int FileSize { get; set; }
        public byte PckType { get; set; }
        public string DownloadLimitTime { get; set; }
        public uint AppointmentDownloadScheduleID { get; set; }

        public static CGMetadata[] GetArray(CacheStream stream, EndianType endian = EndianType.LittleEndian)
        {
            using (EndianBinaryReader reader = new EndianBinaryReader(stream, endian))
            {
                // Get the entry count
                int entryCount = GetEntryCount(reader);

                // Initialize the return value of the instance
                CGMetadata[] entries = new CGMetadata[entryCount];

                // Assign the data to the return value
                for (int i = 0; i < entryCount; i++) entries[i] = Deserialize(reader);

                // Return the value
                return entries;
            }
        }

        public static List<CGMetadata> GetList(CacheStream stream, EndianType endian = EndianType.LittleEndian)
        {
            using (EndianBinaryReader reader = new EndianBinaryReader(stream, endian))
            {
                // Get the entry count
                int entryCount = GetEntryCount(reader);

                // Initialize the return value of the instance
                List<CGMetadata> entries = new List<CGMetadata>();

                // Assign the data to the return value
                for (int i = 0; i < entryCount; i++) entries.Add(Deserialize(reader));

                // Return the value
                return entries;
            }
        }

        public static IEnumerable<CGMetadata> Enumerate(CacheStream stream, EndianType endian = EndianType.LittleEndian)
        {
            using (EndianBinaryReader reader = new EndianBinaryReader(stream, endian))
            {
                // Get the entry count
                int entryCount = GetEntryCount(reader);

                // Initialize the return value of the instance
                List<CGMetadata> entries = new List<CGMetadata>();

                // Assign the data to the return value and yield it
                for (int i = 0; i < entryCount; i++) yield return Deserialize(reader);
            }
        }

        private static int GetEntryCount(EndianBinaryReader reader)
        {
            // Skip the file size information and skip to the entry count
            reader.BaseStream.Position += 4;
            int entryCount = reader.ReadInt32();

            // Skip the keys and data start offsets
            reader.BaseStream.Position += (entryCount * 4) * 2;

            return entryCount;
        }

        private static CGMetadata Deserialize(EndianBinaryReader reader)
        {
            CGMetadata entry = new CGMetadata();
            entry.CgID = reader.ReadInt32();

            entry.UnlockType = reader.ReadByte();
            entry.UnlockCondition = reader.ReadUInt32();
            entry.LevelIDBegin = reader.ReadInt32();
            entry.LevelIDEnd = reader.ReadInt32();
            entry.CgCategory = reader.ReadInt32();
            entry.CgSubCategory = reader.ReadInt32();

            int ptrToCgGroupIDArr = reader.ReadInt32();

            entry.WikiCgScore = reader.ReadInt32();
            entry.InitialUnlock = reader.ReadBoolean();

            int ptrToCgPath = reader.ReadInt32();
            int ptrToCgIconSpritePath = reader.ReadInt32();
            int ptrToPckType = reader.ReadInt32();

            entry.InStreamingAssets = reader.ReadInt32();
            entry.CgPlayMode = reader.ReadInt32();

            int ptrToCgExtraKey = reader.ReadInt32();

            entry.FileSize = reader.ReadInt32();

            entry.PckType = reader.ReadByte();
            int ptrToDownloadLimitTime = reader.ReadInt32();

            entry.AppointmentDownloadScheduleID = reader.ReadUInt32();

            reader.BaseStream.Position = ptrToCgGroupIDArr;
            uint CgGroupIDCount = reader.ReadUInt32();
            entry.CgGroupID = new int[CgGroupIDCount];
            for (int i = 0; i < CgGroupIDCount; i++)
            {
                entry.CgGroupID[i] = reader.ReadInt32();
            }

            reader.BaseStream.Position = ptrToCgPath;
            entry.CgPath = reader.ReadString();

            reader.BaseStream.Position = ptrToCgIconSpritePath;
            entry.CgIconSpritePath = reader.ReadString();

            reader.BaseStream.Position++;
            entry.CgLockHint = new CgHash { Hash = reader.ReadInt32() };

            reader.BaseStream.Position = ptrToPckType + 1;

            reader.BaseStream.Position = ptrToCgExtraKey;
            entry.CgExtraKey = reader.ReadString();

            reader.BaseStream.Position = ptrToDownloadLimitTime;
            entry.DownloadLimitTime = reader.ReadString();

            return entry;
        }
    }
}
