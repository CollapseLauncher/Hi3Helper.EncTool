using System;
using System.Buffers;
using System.Data;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
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
        {
            read = 0;
            int lenOfArrayT = Marshal.SizeOf<T>() * input.Length;
            if (output.Length < lenOfArrayT) return false;

            for (int i = 0; i < input.Length; i++)
            {
                if (!TrySerializeStruct(input[i], ref read, output)) return false;
            }
            return true;
        }

        public static bool TrySerializeStruct<T>(T input, ref int pos, byte[] output)
        {
            int lenOfT = Marshal.SizeOf<T>();
            if (pos + lenOfT > output.Length) return false;

            nint dataPtr = Marshal.AllocHGlobal(lenOfT);
            Marshal.StructureToPtr(input, dataPtr, true);
            Marshal.Copy(dataPtr, output, pos, lenOfT);
            Marshal.FreeHGlobal(dataPtr);
            pos += lenOfT;
            return true;
        }

        public static bool TryDeserializeStruct<T>(byte[] data, int count, out T[] output)
        {
            int lenOfArrayT = Marshal.SizeOf<T>() * count;
            output = default!;
            if (data.Length < lenOfArrayT) return false;

            output = new T[count];
            for (int i = 0, pos = 0; i < count; i++)
            {
                if (!TryDeserializeStruct(data, ref pos, out output[i])) return false;
            }
            return true;
        }

        public static bool TryDeserializeStruct<T>(byte[] data, ref int pos, out T output)
        {
            output = default;
            int lenOfT = Marshal.SizeOf<T>();
            if (data.Length < lenOfT || data.Length - lenOfT < pos) return false;

            nint bufferPtr = Marshal.AllocHGlobal(lenOfT);
            Marshal.Copy(data, pos, bufferPtr, lenOfT);

#pragma warning disable IL2091 // Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' in target method or type. The generic parameter of the source method or type does not have matching annotations.
            output = Marshal.PtrToStructure<T>(bufferPtr);
#pragma warning restore IL2091 // Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' in target method or type. The generic parameter of the source method or type does not have matching annotations.
            Marshal.FreeHGlobal(bufferPtr);
            pos += lenOfT;
            return true;
        }

        public static unsafe void GetListOfPaths(ReadOnlySpan<byte> input, out string[] outlist, long count)
        {
            outlist = new string[count];
            int inLen = input.Length;

            int idx = 0, strIdx = 0;
            fixed (byte* inputPtr = input)
            {
                sbyte* inputSignedPtr = (sbyte*)inputPtr;
                do
                {
                    ReadOnlySpan<byte> inputSpanned = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(inputPtr + idx);
                    idx += inputSpanned.Length + 1;
                    outlist[strIdx++] = Encoding.UTF8.GetString(inputSpanned);
                } while (idx < inLen);
            }
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

        private static readonly SpanAction<char, nint> s_normalizePathReplaceCore = NormalizePathUnsafeCore;
        public static unsafe string NormalizePath(ReadOnlySpan<char> source)
        {
            ReadOnlySpan<char> sourceTrimmed = source.TrimStart('/');
            fixed (char* ptr = sourceTrimmed)
            {
                return string.Create(sourceTrimmed.Length, (nint)ptr, s_normalizePathReplaceCore);
            }
        }

        // Reference: https://github.com/dotnet/aspnetcore/blob/c65dac77cf6540c81860a42fff41eb11b9804367/src/Shared/QueryStringEnumerable.cs#L169
        private static unsafe void NormalizePathUnsafeCore(Span<char> buffer, nint state)
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

        private static WindowsIdentity CurrentWindowsIdentity = WindowsIdentity.GetCurrent();
        public static bool IsUserHasPermission(string input)
        {
            // Assign the type
            bool isFileExist = File.Exists(input);
            bool isDirectoryExist = Directory.Exists(input);

            // If both types do not exist, then return false
            if (!isFileExist && !isDirectoryExist) return false;

            // Check the path type
            bool isFileType = isFileExist && !isDirectoryExist;

            // Check for directory access
            AuthorizationRuleCollection pathAcl;
            FileSystemSecurity pathSecurity;
            if (!isFileType)
            {
                // Get directory ACL
                DirectoryInfo directoryInfo = new DirectoryInfo(input);
                pathSecurity = directoryInfo.GetAccessControl();
            }
            else
            {
                // Get file ACL
                FileInfo fileInfo = new FileInfo(input);
                pathSecurity = fileInfo.GetAccessControl();
            }

            // If the path security is null, then return false (as not permitted)
            if (pathSecurity == null) return false;

            // If the path ACL is null, then return false (as not permitted)
            pathAcl = pathSecurity.GetAccessRules(true, true, typeof(NTAccount));
            if (pathAcl == null) return false;

            // Get current Windows User Identity principal
            WindowsPrincipal principal = new WindowsPrincipal(CurrentWindowsIdentity);

            // Do LINQ to check across available ACLs and ensure that the exact user has the rights to
            // access the file
            bool isHasAccess = pathAcl
                .Cast<FileSystemAccessRule>()
                .Where(x => IsPrincipalHasFileSystemAccess(principal, x) ?? false)
                .FirstOrDefault() != null;

            return isHasAccess;
        }

        private static bool? IsPrincipalHasFileSystemAccess(this WindowsPrincipal user, FileSystemAccessRule rule) => rule switch
        {
            { FileSystemRights: FileSystemRights FileSystemRights }
                when (FileSystemRights & (FileSystemRights.WriteData | FileSystemRights.Write)) == 0 => null,
            { IdentityReference: { Value: string value } }
                when value.StartsWith("S-1-") && !user.IsInRole(new SecurityIdentifier(rule.IdentityReference.Value)) => null,
            { IdentityReference: { Value: string value } }
                when value.StartsWith("S-1-") == false && !user.IsInRole(rule.IdentityReference.Value) => null,
            { AccessControlType: AccessControlType.Deny } => false,
            { AccessControlType: AccessControlType.Allow } => true,
            _ => null
        };

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

        // Reference:
        // https://stackoverflow.com/questions/3702216/how-to-convert-integer-to-binary-string-in-c#:~:text=Simple%20.NET%208%2B%20Version
        public static string ToBinaryString(uint u)
        {
            Span<byte> ascii = stackalloc byte[32];
            for (int i = 0; i < 32; i += 4)
            {
                // we want the MSB to be on the left, so we need to reverse everything
                // other than that we simply grab the ith bit (from the LSB) 
                // and simply OR that to the ASCII character '0' (0x30).
                // if the bit was 0 the result is '0' itself, otherwise
                // if the bit was 1 then the result is '0' | 1 (0x30 | 1) which 
                // yields 0x31 which is also conveniently the ASCII code for '1'.
                ascii[31 - (i + 3)] = (byte)((u & (1u << (i + 3))) >> (i + 3) | 0x30);
                ascii[31 - (i + 2)] = (byte)((u & (1u << (i + 2))) >> (i + 2) | 0x30);
                ascii[31 - (i + 1)] = (byte)((u & (1u << (i + 1))) >> (i + 1) | 0x30);
                ascii[31 - i] = (byte)((u & (1u << i)) >> i | 0x30);
            }
            return Encoding.ASCII.GetString(ascii);
        }
    }
}
