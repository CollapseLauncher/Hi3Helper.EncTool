using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

#nullable enable
namespace Hi3Helper.EncTool.Parser.Senadina
{
    public class SenadinaFileIdentifier
    {
        public string? lastIdentifier { get; set; }
        public long fileTime { get; set; }
        public Dictionary<string, byte[]>? stringStore { get; set; }

        public bool IsKeyStoreExist(string key) => stringStore?.ContainsKey(key) ?? false;
        public bool TryReadStringStoreArrayAs<T>(string key, out T[]? result)
            where T : struct
        {
            result = null;
            if (IsKeyStoreExist(key))
            {
                ReadOnlySpan<byte> dataSpan = stringStore?[key];
                int sizeOfStruct = Marshal.SizeOf<T>();
                int count = dataSpan.Length / sizeOfStruct;

                if (count <= 0) return false;
                if (dataSpan.Length % sizeOfStruct != 0) return false;

                result = new T[count];
                int offset = 0;
                for (int i = 0; i < count; i++)
                    result[i] = ReadTInner<T>(dataSpan, sizeOfStruct, ref offset);
            }
            return false;
        }

        public bool TryReadStringStoreAs(string key, out string? result)
        {
            result = null;
            if (IsKeyStoreExist(key))
            {
                ReadOnlySpan<byte> dataSpan = stringStore?[key];
                result = Encoding.UTF8.GetString(dataSpan);
                return true;
            }
            return false;
        }

        public bool TryReadStringStoreAs<T>(string key, out T? result)
            where T : struct
        {
            result = null;
            if (IsKeyStoreExist(key))
            {
                ReadOnlySpan<byte> dataSpan = stringStore?[key];
                int sizeOfStruct = Marshal.SizeOf<T>();
                if (dataSpan.Length < sizeOfStruct)
                    return false;

                int offset = 0;
                result = ReadTInner<T>(dataSpan, sizeOfStruct, ref offset);
                return true;
            }
            return false;
        }

        private T ReadTInner<T>(ReadOnlySpan<byte> span, int structSizeOf, ref int offset)
            where T : struct
        {
            T value = MemoryMarshal.Read<T>(span.Slice(offset));
            offset += structSizeOf;
            return value;
        }

        public static byte[] GenerateMothKey(string inputKey)
        {
            SHA256 sha256 = SHA256.Create();
            byte[] keyRaw = Encoding.UTF8.GetBytes(inputKey);
            byte[] returnKey = sha256.ComputeHash(keyRaw);
            return returnKey;
        }

        public static byte[] GenerateMothIV(int seed)
        {
            Random random = new Random(seed);
            long randomLong1 = random.NextInt64();
            long randomLong2 = random.NextInt64();

            randomLong1 ^= randomLong1 << 16;
            randomLong2 |= randomLong2 >> 32 | randomLong1;

            byte[] ivByte = new byte[16];
            MemoryMarshal.Write(ivByte, randomLong1);
            MemoryMarshal.Write(ivByte.AsSpan(8), randomLong2);

            SHA1 sha = SHA1.Create();
            byte[] returnIv = sha.ComputeHash(ivByte);
            return returnIv[..16];
        }
    }
}
