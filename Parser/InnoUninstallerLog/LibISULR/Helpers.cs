using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace LibISULR
{
    public ref struct StringSplitter
    {
        private ReadOnlySpan<byte> data;
        private unsafe byte* dataPtr;
        private int index;

        public unsafe StringSplitter(byte[] data)
            : this(data, 0) { }

        public unsafe StringSplitter(byte[] data, int offset)
            : this(data.AsSpan(offset)) { }

        public unsafe StringSplitter(Span<byte> data)
        {
            this.data = data;
            fixed (byte* dataPtr = this.data)
                this.dataPtr = dataPtr;
            index = 0;
        }

        private int ReadLength(out bool eof)
        {
            byte type = data[index++];
            if (eof = type == 0xFF) // If the type is 0xFF (end), then set eof = true and return 0
                return 0;

            return type switch
            {
                0xFD => data.ReadUShort(ref index), // UTF-8 string length Type
                0xFE => data.ReadInt(ref index), // Unicode/UTF-16 string length Type
                _ => type // Type as the length
            };
        }

        public unsafe string ReadString()
        {
            bool eof;
            int length = ReadLength(out eof);
            if (eof)
                return null;

            string result;

            if (length < 0)
            {
                length = -length;
                result = Encoding.Unicode.GetString(dataPtr + index, length);
            }
            else if (length == 0)
                return string.Empty;
            else
                result = Encoding.Default.GetString(dataPtr + index, length);

            index += length;
            return result;
        }

        public unsafe int WriteString(Span<byte> buffer, Encoding encoding, string inputString)
        {
            bool isUTF16 = encoding.GetType() == Encoding.Unicode.GetType();
            byte strType = (byte)(isUTF16 ? 0xFE : 0xFD);
            MemoryMarshal.Write(buffer, strType);

            int offset = 1;
            int strByteLen = inputString.Length * (isUTF16 ? 2 : 1);

            if (isUTF16)
                MemoryMarshal.Write(buffer.Slice(offset), -strByteLen);
            else
                MemoryMarshal.Write(buffer.Slice(offset), -(ushort)strByteLen);

            offset += isUTF16 ? 4 : 2;
            offset += encoding.GetBytes(inputString, buffer.Slice(offset));

            return offset;
        }

        public int WriteDateTime(Span<byte> buffer, DateTime inputDateTime)
        {
            bool isDateEmpty = DateTime.MinValue == inputDateTime;
            byte lenType = (byte)(isDateEmpty ? 0xFF : 0xFE);
            MemoryMarshal.Write(buffer, lenType);

            int offset = 1;
            if (isDateEmpty) return offset;

            Span<ushort> dateTimeInUShorts = new ushort[8];
            MemoryMarshal.Write(buffer.Slice(offset), -(dateTimeInUShorts.Length * 2));
            offset += 4;

            dateTimeInUShorts[0] = (ushort)inputDateTime.Year;
            dateTimeInUShorts[1] = (ushort)inputDateTime.Month;
            dateTimeInUShorts[2] = (ushort)inputDateTime.DayOfWeek;
            dateTimeInUShorts[3] = (ushort)inputDateTime.Day;
            dateTimeInUShorts[4] = (ushort)inputDateTime.Hour;
            dateTimeInUShorts[5] = (ushort)inputDateTime.Minute;
            dateTimeInUShorts[6] = (ushort)inputDateTime.Second;
            dateTimeInUShorts[7] = (ushort)inputDateTime.Millisecond;

            Span<byte> dateInTimeBytes = MemoryMarshal.AsBytes(dateTimeInUShorts);
            dateInTimeBytes.CopyTo(buffer.Slice(offset));
            offset += dateTimeInUShorts.Length * 2;

            return offset;
        }

        public DateTime ReadDateTime()
        {
            bool eof;
            int length = ReadLength(out eof);
            if (eof)
                return DateTime.MinValue;
            if (length < 0)
                length = -length;

            DateTime result;

            /*
            type TSystemTime = record
              Year: Word;	      // Year part
              Month: Word;	    // Month part
              DayOfWeek: Word;	
              Day: Word;        // Day of month part
              Hour: Word;	      // Hour of the day
              Minute: Word;     // Minute of the hour
              Second: Word;	    // Second of the minute
              MilliSecond: Word;// Milliseconds in the second
            end;
            */

            if (length >= 16)
            {
                int i = index;

                ushort year = data.ReadUShort(ref i);
                ushort month = data.ReadUShort(ref i);
                i += 2; //ushort dow = data.ReadUShort(ref i);
                ushort day = data.ReadUShort(ref i);
                ushort hour = data.ReadUShort(ref i);
                ushort minute = data.ReadUShort(ref i);
                ushort second = data.ReadUShort(ref i);
                ushort ms = data.ReadUShort(ref i);

                result = new DateTime(year, month, day, hour, minute, second, ms, DateTimeKind.Local);
            }
            else
                result = DateTime.MinValue;

            index += length;
            return result;
        }

        public int WriteBytes(Span<byte> buffer, ReadOnlySpan<byte> source)
        {
            byte lenType = 0xFE;
            MemoryMarshal.Write(buffer, lenType);

            int offset = 1;
            MemoryMarshal.Write(buffer.Slice(offset), -source.Length);
            offset += 4;
            if (source.Length == 0) return offset;

            source.CopyTo(buffer.Slice(offset));
            offset += source.Length;
            return offset;
        }

        public unsafe byte[] ReadBytes()
        {
            bool eof;
            int length = ReadLength(out eof);
            if (eof)
                return null;
            if (length < 0)
                length = -length;

            if (length == 0) return Array.Empty<byte>();

            byte[] result = new byte[length];
            fixed (byte* resultPtr = result)
            {
                Buffer.MemoryCopy(dataPtr + index, resultPtr, length, length);
            }
            index += length;

            return result;
        }

        public bool IsEnd
        {
            get { return index >= data.Length; }
        }

        public List<string> GetStringList()
        {
            List<string> returnList = new List<string>();
            while (!IsEnd)
            {
                string str = ReadString();
                if (str != null)
                    returnList.Add(str);
            }
            return returnList;
        }

        public unsafe string[] GetStringArray()
        {
            byte type = data[index++];
            if (type == 0xFF) // If the type is 0xFF (end), then set eof = true and return 0
                return Array.Empty<string>();

            int byteLength = type switch
            {
                0xFD => data.ReadUShort(ref index), // UTF-8 string length Type
                0xFE => data.ReadInt(ref index), // Unicode/UTF-16 string length Type
                _ => type // Type as the length
            };

            if (byteLength == 0) return Array.Empty<string>();

            bool isUTF16 = type == 0xFE;

            int offset = 0;
            int count = MemoryMarshal.Read<int>(data.Slice(index)) * (isUTF16 ? 2 : 1); // WTF?
            index += 4;
            string[] returnArray = new string[count];

        GetStringArray:
            int stringByteLength = MemoryMarshal.Read<int>(data.Slice(index)) * (isUTF16 ? 2 : 1);
            index += 4;

            string stringResult = isUTF16 ? Encoding.Unicode.GetString(dataPtr + index, stringByteLength)
                                          : Encoding.UTF8.GetString(dataPtr + index, stringByteLength);
            index += stringByteLength;

            returnArray[offset++] = stringResult;
            if (offset < count && *(dataPtr + index) != 0xFF) goto GetStringArray;

            return returnArray;
        }

        public int WriteStringList(Span<byte> buffer, Encoding encoding, List<string> inputString)
        {
            int offset = 0, index = 0;

        WriteStringList:
            offset += WriteString(buffer.Slice(offset), encoding, inputString[index++]);
            if (index < inputString.Count) goto WriteStringList;

            buffer[offset++] = 0xFF;
            return offset;
        }

        public int WriteStringArray(Span<byte> buffer, Encoding encoding, string[] inputString)
        {
            bool isUTF16 = encoding.GetType() == Encoding.Unicode.GetType();
            byte strType = (byte)(isUTF16 ? 0xFE : 0xFD);
            MemoryMarshal.Write(buffer, strType);
            int offset = 1;

            int count = inputString.Length / (isUTF16 ? 2 : 1);
            int totalOfStringSize = inputString.Sum(x => x.Length * (isUTF16 ? 2 : 1));
            int totalOfStringLengthSize = inputString.Length * 4;
            int calculatedStringBufferSize = 4 + totalOfStringLengthSize + totalOfStringSize;
            MemoryMarshal.Write(buffer.Slice(offset), count == 0 ? 0 : -calculatedStringBufferSize);
            offset += 4;

            if (count == 0) goto WriteStringArrayEOF;

            MemoryMarshal.Write(buffer.Slice(offset), count);
            offset += 4;

            int stringArrayIndex = 0;
        WriteStringArray:
            if (isUTF16)
                MemoryMarshal.Write(buffer.Slice(offset), inputString[stringArrayIndex].Length);
            else
                MemoryMarshal.Write(buffer.Slice(offset), (ushort)inputString[stringArrayIndex].Length);
            offset += isUTF16 ? 4 : 2;
            offset += encoding.GetBytes(inputString[stringArrayIndex++], buffer.Slice(offset));
            if (stringArrayIndex < inputString.Length) goto WriteStringArray;

        WriteStringArrayEOF:
            buffer[offset++] = 0xFF;
            return offset;
        }
    }

    public static class Helpers
    {
        public static string ReadString(this Stream stream, byte[] buffer, int size)
        {
            stream.Read(buffer, 0, size);

            int stringLength = 0;
            // clean out 0s
            while (buffer[stringLength] != 0)
                stringLength++;

            return Encoding.ASCII.GetString(buffer, 0, stringLength);
        }

        public static int ReadInt(this Stream stream, byte[] buffer)
        {
            stream.Read(buffer, 0, 4);
            return BitConverter.ToInt32(buffer, 0);
        }

        public static uint ReadUInt(this Stream stream, byte[] buffer)
        {
            stream.Read(buffer, 0, 4);
            return BitConverter.ToUInt32(buffer, 0);
        }

        public static uint ReadUShort(this Stream stream, byte[] buffer)
        {
            stream.Read(buffer, 0, 2);
            return BitConverter.ToUInt16(buffer, 0);
        }

        public static ushort ReadUShort(this ReadOnlySpan<byte> data, ref int index)
        {
            ushort result = MemoryMarshal.Read<ushort>(data.Slice(index));
            index += 2;
            return result;
        }

        public static int ReadInt(this ReadOnlySpan<byte> data, ref int index)
        {
            int result = MemoryMarshal.Read<int>(data.Slice(index));
            index += 4;
            return result;
        }
    }
}