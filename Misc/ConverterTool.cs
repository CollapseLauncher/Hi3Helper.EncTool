using System;
using System.Buffers;
using System.IO;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;

namespace Hi3Helper.Data
{
    public static class ConverterTool
    {
        private static readonly MD5 MD5Hash = MD5.Create();
        private static readonly Crc32 crc32 = new Crc32();
        private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        public static string BytesToCRC32Simple(Stream buffer)
        {
            crc32.Append(buffer);
            byte[] arrayResult = crc32.GetHashAndReset();
            Array.Reverse(arrayResult);

            return HexTool.BytesToHexUnsafe(arrayResult);
        }

        public static string BytesToCRC32Simple(string str)
        {
            byte[] strBytes = Encoding.UTF8.GetBytes(str);
            byte[] hashSpan = Crc32.Hash(strBytes);
            Array.Reverse(hashSpan);

            return HexTool.BytesToHexUnsafe(hashSpan);
        }

        public static int BytesToCRC32Int(Stream buffer)
        {
            crc32.Append(buffer);
            byte[] arrayResult = crc32.GetHashAndReset();
            Array.Reverse(arrayResult);

            return BitConverter.ToInt32(arrayResult);
        }

        public static bool TrySerializeStruct<T>(T[] input, byte[] output, out int read)
            where T : struct
        {
            read = 0;
            int lenOfArrayT = Marshal.SizeOf(typeof(T)) * input.Length;
            if (output.Length < lenOfArrayT) return false;

            for (int i = 0; i < input.Length; i++)
            {
                if (!TrySerializeStruct(input[i], ref read, output)) return false;
            }
            return true;
        }

        public static bool TrySerializeStruct<T>(T input, ref int pos, byte[] output)
            where T : struct
        {
            int lenOfT = Marshal.SizeOf(typeof(T));
            if (pos + lenOfT > output.Length) return false;

            IntPtr dataPtr = Marshal.AllocHGlobal(lenOfT);
            Marshal.StructureToPtr(input, dataPtr, true);
            Marshal.Copy(dataPtr, output, pos, lenOfT);
            Marshal.FreeHGlobal(dataPtr);
            pos += lenOfT;
            return true;
        }

        public static bool TryDeserializeStruct<T>(byte[] data, int count, out T[] output)
            where T : struct
        {
            int lenOfArrayT = Marshal.SizeOf(typeof(T)) * count;
            output = default;
            if (data.Length < lenOfArrayT) return false;

            output = new T[count];
            for (int i = 0, pos = 0; i < count; i++)
            {
                if (!TryDeserializeStruct(data, ref pos, out output[i])) return false;
            }
            return true;
        }

        public static bool TryDeserializeStruct<T>(byte[] data, ref int pos, out T output)
            where T : struct
        {
            output = default;
            int lenOfT = Marshal.SizeOf(typeof(T));
            if (data.Length < lenOfT || data.Length - lenOfT < pos) return false;

            IntPtr bufferPtr = Marshal.AllocHGlobal(lenOfT);
            Marshal.Copy(data, pos, bufferPtr, lenOfT);

            output = (T)Marshal.PtrToStructure(bufferPtr, typeof(T));
            Marshal.FreeHGlobal(bufferPtr);
            pos += lenOfT;
            return true;
        }

        public static int BytesToCRC32Int(string str)
        {
            byte[] strBytes = Encoding.UTF8.GetBytes(str);
            byte[] hashSpan = Crc32.Hash(strBytes);
            Array.Reverse(hashSpan);

            return BitConverter.ToInt32(hashSpan);
        }

        public static string CreateMD5Shared(Stream fs)
        {
            MD5Hash.Initialize();
            ReadOnlySpan<byte> res = MD5Hash.ComputeHash(fs);
            return HexTool.BytesToHexUnsafe(res);
        }

        public static double Unzeroed(double i) => Math.Max(i, 1);

        public static double GetPercentageNumber(double cur, double max, int round = 2) => Math.Round((100 * cur) / max, round);

        private static readonly SpanAction<char, IntPtr> s_normalizePathReplaceCore = NormalizePathUnsafeCore;
        public static unsafe string NormalizePath(ReadOnlySpan<char> source)
        {
            ReadOnlySpan<char> sourceTrimmed = source.TrimStart('/');
            fixed (char* ptr = sourceTrimmed)
            {
                return string.Create(sourceTrimmed.Length, (IntPtr)ptr, s_normalizePathReplaceCore);
            }
        }

        // Reference: https://github.com/dotnet/aspnetcore/blob/c65dac77cf6540c81860a42fff41eb11b9804367/src/Shared/QueryStringEnumerable.cs#L169
        private static unsafe void NormalizePathUnsafeCore(Span<char> buffer, IntPtr state)
        {
            fixed (char* ptr = buffer)
            {
                var input = (ushort*)state.ToPointer();
                var output = (ushort*)ptr;

                var i = (nint)0;
                var n = (nint)(uint)buffer.Length;

                if (Sse41.IsSupported && n >= Vector128<ushort>.Count)
                {
                    var vecPlus = Vector128.Create((ushort)'/');
                    var vecSpace = Vector128.Create((ushort)'\\');

                    do
                    {
                        var vec = Sse2.LoadVector128(input + i);
                        var mask = Sse2.CompareEqual(vec, vecPlus);
                        var res = Sse41.BlendVariable(vec, vecSpace, mask);

                        Sse2.Store(output + i, res);

                        i += Vector128<ushort>.Count;

                    } while (i <= n - Vector128<ushort>.Count);
                }

                for (; i < n; ++i)
                {
                    if (input[i] != '/')
                    {
                        output[i] = input[i];
                    }
                    else
                    {
                        output[i] = '\\';
                    }
                }
            }
        }

        public static string SummarizeSizeSimple(double value, int decimalPlaces = 2)
        {
            byte mag = (byte)Math.Log(value, 1000);

            return string.Format("{0} {1}", Math.Round(value / (1L << (mag * 10)), decimalPlaces), SizeSuffixes[mag]);
        }

        public static int GetUnixTimestamp(bool isUTC = false) => (int)Math.Truncate(isUTC ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);

        public static bool IsUserHasPermission(string input)
        {
            try
            {
                if (!Directory.Exists(input))
                    Directory.CreateDirectory(input);

                File.Create(Path.Combine(input, "write_test"), 1, FileOptions.DeleteOnClose).Close();
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            return true;
        }

        public static float ConvertRangeValue(float sMin, float sMax, float sValue, float tMin, float tMax) => ((sValue - sMin) * (tMax - tMin) / (sMax - sMin)) + tMin;

        public static string CombineURLFromString(ReadOnlySpan<char> baseURL, params string[] segments)
        {
            StringBuilder builder = new StringBuilder().Append(baseURL.TrimEnd('/'));

            foreach (ReadOnlySpan<char> a in segments)
            {
                if (a.Length == 0) continue;

                bool isMacros = a.StartsWith("?");
                if (!isMacros)
                {
                    builder.Append('/');
                }
                builder.Append(a.Trim('/'));
            }

            return builder.ToString();
        }
    }
}
