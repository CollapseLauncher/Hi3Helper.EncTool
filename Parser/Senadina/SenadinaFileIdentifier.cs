﻿using Hi3Helper.Data;
using Hi3Helper.Http.Legacy;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable IdentifierTypo
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedMember.Global
#nullable enable
namespace Hi3Helper.EncTool.Parser.Senadina
{
    public enum SenadinaKind { bricksBase, bricksCurrent, platformBase, wandCurrent, chiptunesCurrent, chiptunesPreload, wonderland }
    public partial class SenadinaFileIdentifier : IDisposable
    {
        public string? relativePath { get; set; }
        public string? lastIdentifier { get; set; }
        public long fileTime { get; set; }
        public Stream? fileStream { get; set; }
        public Dictionary<string, byte[]>? stringStore { get; set; }

        ~SenadinaFileIdentifier() => Dispose();

        public void Dispose()
        {
            stringStore?.Clear();
            fileStream?.Dispose();

            GC.SuppressFinalize(this);
        }

        public bool IsKeyStoreExist(string key) => stringStore?.ContainsKey(key) ?? false;
        public bool TryReadStringStoreArrayAs<T>(string key, out T[]? result)
            where T : struct
        {
            result = null;
            if (!IsKeyStoreExist(key))
            {
                return false;
            }

            ReadOnlySpan<byte> dataSpan     = stringStore?[key];
            int                sizeOfStruct = Marshal.SizeOf<T>();
            int                count        = dataSpan.Length / sizeOfStruct;

            if (count <= 0) return false;
            if (dataSpan.Length % sizeOfStruct != 0) return false;

            result = new T[count];
            int offset = 0;
            for (int i = 0; i < count; i++)
                result[i] = ReadTInner<T>(dataSpan, sizeOfStruct, ref offset);
            return false;
        }

        public bool TryReadStringStoreAs(string key, out string? result)
        {
            result = null;
            if (!IsKeyStoreExist(key))
            {
                return false;
            }

            ReadOnlySpan<byte> dataSpan = stringStore?[key];
            result = Encoding.UTF8.GetString(dataSpan);
            return true;
        }

        public bool TryReadStringStoreAs<T>(string key, out T? result)
            where T : struct
        {
            result = null;
            if (!IsKeyStoreExist(key))
            {
                return false;
            }

            ReadOnlySpan<byte> dataSpan     = stringStore?[key];
            int                sizeOfStruct = Marshal.SizeOf<T>();
            if (dataSpan.Length < sizeOfStruct)
                return false;

            int offset = 0;
            result = ReadTInner<T>(dataSpan, sizeOfStruct, ref offset);
            return true;
        }

        private static T ReadTInner<T>(ReadOnlySpan<byte> span, int structSizeOf, ref int offset)
            where T : struct
        {
            T value = MemoryMarshal.Read<T>(span[offset..]);
            offset += structSizeOf;
            return value;
        }

        public static string GetHashedString(string input)
        {
            Span<byte> data = stackalloc byte[32];
            Span<byte> nameBuffer = stackalloc byte[4 << 10];
            if (input.Length > nameBuffer.Length)
                throw new IndexOutOfRangeException($"Input length is more than allowed size: {nameBuffer.Length} bytes!");

            if (!Encoding.UTF8.TryGetBytes(input, nameBuffer, out int written))
                throw new InvalidDataException($"Failed while getting hash name for input: {input}");

            SHA256.HashData(nameBuffer[..written], data);
            return HexTool.BytesToHexUnsafe(data) ?? "";
        }

        private static byte[] GenerateMothKoentji(string inputKey)
        {
            SHA256 sha256 = SHA256.Create();
            byte[] keyRaw = Encoding.UTF8.GetBytes(inputKey);
            byte[] returnKey = sha256.ComputeHash(keyRaw);
            return returnKey;
        }

        private static byte[] GenerateMothAngkaDadoe(int seed)
        {
            Random random      = new(seed);
            long   randomLong1 = random.NextInt64();
            long   randomLong2 = random.NextInt64();

            randomLong1 ^= randomLong1 << 16;
            randomLong2 |= randomLong2 >> 32 | randomLong1;

            byte[] ivByte = new byte[16];
            MemoryMarshal.Write(ivByte, randomLong1);
            MemoryMarshal.Write(ivByte.AsSpan(8), randomLong2);

            SHA1 sha = SHA1.Create();
            byte[] returnIv = sha.ComputeHash(ivByte);
            return returnIv[..16];
        }

        public Stream CreateKangBakso() => CreateKangBakso(fileStream!, lastIdentifier!, relativePath!, (int)fileTime);

        public static Stream CreateKangBakso(Stream bihun, string koentji, string alamatKangBakso, int jadwal)
        {
            Aes aesInstance = Aes.Create();
            aesInstance.Mode = CipherMode.CFB;
            aesInstance.Key = GenerateMothKoentji(koentji + alamatKangBakso);
            aesInstance.IV = GenerateMothAngkaDadoe(jadwal);
            aesInstance.Padding = PaddingMode.ISO10126;

            Stream superSemar = new CryptoStream(bihun, aesInstance.CreateDecryptor(), CryptoStreamMode.Read);
            Stream petrus = new BrotliStream(superSemar, CompressionMode.Decompress);

            return petrus;
        }

        public async Task<Stream?> GetOriginalFileStream(HttpClient client, CancellationToken token = default)
        {
            const string dictKey = "origUrl";
            if (!TryReadStringStoreAs(dictKey, out string? result))
            {
                throw new
                    KeyNotFoundException("origUrl from pustaka's store is not exist. Please report this issue to our Discord Server!");
            }

            if (string.IsNullOrEmpty(result))
                throw new NullReferenceException("origUrl from pustaka's store is null or just an empty string. Please report this issue to our Discord Server!");

            Stream networkStream = await HttpResponseInputStream.CreateStreamAsync(client, result, 0, null, token);
            return networkStream;
        }
    }
}
