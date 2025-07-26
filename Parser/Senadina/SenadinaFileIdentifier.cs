using Hi3Helper.Data;
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
        public string? relativePath   { get; set; }
        public string? lastIdentifier { get; set; }
        public long    fileTime       { get; set; }

        public BrotliStream? fileStream { get; set; }
        public Dictionary<string, byte[]>? stringStore { get; set; }

        ~SenadinaFileIdentifier() => Dispose();

        public void Dispose()
        {
            stringStore?.Clear();
            fileStream?.Dispose();

            GC.SuppressFinalize(this);
        }

        public bool IsKeyStoreExist(string key) => stringStore?.ContainsKey(key) ?? false;
        public unsafe bool TryReadStringStoreArrayAs<T>(string key, out T[]? result)
            where T : unmanaged
        {
            result = null;
            if (!IsKeyStoreExist(key))
            {
                return false;
            }

            ReadOnlySpan<byte> dataSpan     = stringStore?[key];
            int                sizeOfStruct = sizeof(T);
            int                count        = dataSpan.Length / sizeOfStruct;

            if (count <= 0) return false;
            if (dataSpan.Length % sizeOfStruct != 0) return false;

            result = new T[count];
            int offset = 0;
            for (int i = 0; i < count; i++)
                result[i] = ReadTInner<T>(dataSpan, ref offset);
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

        public unsafe bool TryReadStringStoreAs<T>(string key, out T? result)
            where T : unmanaged
        {
            result = null;
            if (!IsKeyStoreExist(key))
            {
                return false;
            }

            ReadOnlySpan<byte> dataSpan     = stringStore?[key];
            int                sizeOfStruct = sizeof(T);
            if (dataSpan.Length < sizeOfStruct)
                return false;

            int offset = 0;
            result = ReadTInner<T>(dataSpan, ref offset);
            return true;
        }

        private static unsafe T ReadTInner<T>(ReadOnlySpan<byte> span, ref int offset)
            where T : unmanaged
        {
            T value = MemoryMarshal.Read<T>(span[offset..]);
            offset += sizeof(T);
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
            byte[] keyRaw = Encoding.UTF8.GetBytes(inputKey);
            byte[] returnKey = SHA256.HashData(keyRaw);
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

            byte[] returnIv = SHA1.HashData(ivByte);
            return returnIv[..16];
        }

        public Stream CreateKangBakso() => CreateKangBakso(fileStream!, lastIdentifier!, relativePath!, (int)fileTime);

        public static BrotliStream CreateKangBakso(Stream bihun, string koentji, string alamatKangBakso, int jadwal)
        {
            Aes aesInstance = Aes.Create();
            aesInstance.Mode = CipherMode.CFB;
            aesInstance.Key = GenerateMothKoentji(koentji + alamatKangBakso);
            aesInstance.IV = GenerateMothAngkaDadoe(jadwal);
            aesInstance.Padding = PaddingMode.ISO10126;

            Stream superSemar = new CryptoStream(bihun, aesInstance.CreateDecryptor(), CryptoStreamMode.Read, false);
            BrotliStream petrus = new BrotliStream(superSemar, CompressionMode.Decompress, false);

            return petrus;
        }

        public string GetOriginalFileUrl()
        {
            const string dictKey = "origUrl";
            if (!TryReadStringStoreAs(dictKey, out string? result))
            {
                throw new
                    KeyNotFoundException("origUrl from pustaka's store is not exist. Please report this issue to our Discord Server!");
            }

            if (string.IsNullOrEmpty(result))
                throw new NullReferenceException("origUrl from pustaka's store is null or just an empty string. Please report this issue to our Discord Server!");

            return result;
        }

        public async ValueTask<HttpResponseMessage> GetOriginalFileHttpResponse(HttpClient client, HttpMethod? method = null, CancellationToken token = default)
        {
            HttpRequestMessage?  message  = null;
            HttpResponseMessage? response = null;

            string originalUrl = GetOriginalFileUrl();

            bool isFail = false;
            try
            {
                message  = new HttpRequestMessage(method ?? HttpMethod.Get, originalUrl);
                response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Error while retrieving the original URL: {originalUrl} with status: {response.StatusCode} ({(int)response.StatusCode})", null, response.StatusCode);
                }

                return response;
            }
            catch
            {
                isFail = true;
            }
            finally
            {
                if (isFail)
                {
                    message?.Dispose();
                    response?.Dispose();
                }
            }

            throw new InvalidOperationException("This code shouldn't expect to be executed!");
        }
    }
}
