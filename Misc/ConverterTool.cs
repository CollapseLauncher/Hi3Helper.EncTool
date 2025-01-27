using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global
// ReSharper disable CheckNamespace

namespace Hi3Helper.Data
{
    public delegate ValueTask<TResult> GetSelectorSignedAsync<in TFrom, TResult>(TFrom item, CancellationToken token)
        where TResult : struct, ISignedNumber<TResult>;

    public static class ConverterTool
    {
        public static string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

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

        public static bool TryDeserializeStruct<[DynamicallyAccessedMembers(
              DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.NonPublicConstructors
            )] T>(byte[] data, int count, out T[] output)
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

        public static bool TryDeserializeStruct<[DynamicallyAccessedMembers(
              DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.NonPublicConstructors
            )]T>(byte[] data, ref int pos, out T output)
        {
            output = default;
            int lenOfT = Marshal.SizeOf<T>();
            if (data.Length < lenOfT || data.Length - lenOfT < pos) return false;

            nint bufferPtr = Marshal.AllocHGlobal(lenOfT);
            Marshal.Copy(data, pos, bufferPtr, lenOfT);

            output = Marshal.PtrToStructure<T>(bufferPtr);
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
                do
                {
                    ReadOnlySpan<byte> inputSpanned = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(inputPtr + idx);
                    idx += inputSpanned.Length + 1;
                    outlist[strIdx++] = Encoding.UTF8.GetString(inputSpanned);
                } while (idx < inLen);
            }
        }

        public static T[] EnsureLengthCopyLast<T>(T[] array, int toLength)
        {
            if (array.Length == 0) throw new IndexOutOfRangeException("Array has no content in it");
            if (array.Length >= toLength) return array;

            T   lastArray = array[^1];
            T[] newArray  = new T[toLength];
            Array.Copy(array, newArray, array.Length);

            for (int i = array.Length; i < newArray.Length; i++)
            {
                newArray[i] = lastArray;
            }

            return newArray;
        }

        /// <summary>
        /// Calculates the remaining time based on total bytes, current bytes, and speed.
        /// </summary>
        /// <param name="totalBytes">The total number of bytes.</param>
        /// <param name="currentBytes">The current number of bytes processed.</param>
        /// <param name="speed">The speed of processing in bytes per second.</param>
        /// <returns>A TimeSpan representing the remaining time.</returns>
        public static TimeSpan ToTimeSpanRemain(double totalBytes, double currentBytes, double speed)
            => TimeSpan.FromSeconds((totalBytes - currentBytes) / Math.Max(speed, 1d));

        /// <summary>
        /// Calculates the remaining time based on total bytes, current bytes, and speed.
        /// </summary>
        /// <param name="totalBytes">The total number of bytes.</param>
        /// <param name="currentBytes">The current number of bytes processed.</param>
        /// <param name="speed">The speed of processing in bytes per second.</param>
        /// <returns>A TimeSpan representing the remaining time.</returns>
        public static TimeSpan ToTimeSpanRemain(long totalBytes, long currentBytes, double speed)
            => TimeSpan.FromSeconds((totalBytes - currentBytes) / Math.Max(speed, 1d));

        /// <summary>
        /// Calculates the remaining time based on total bytes, current bytes, and speed.
        /// </summary>
        /// <param name="totalBytes">The total number of bytes.</param>
        /// <param name="currentBytes">The current number of bytes processed.</param>
        /// <param name="speed">The speed of processing in bytes per second.</param>
        /// <returns>A TimeSpan representing the remaining time.</returns>
        public static TimeSpan ToTimeSpanRemain(float totalBytes, float currentBytes, float speed)
            => TimeSpan.FromSeconds((totalBytes - currentBytes) / Math.Max(speed, 1f));

        /// <summary>
        /// Calculates the remaining time based on total bytes, current bytes, and speed.
        /// </summary>
        /// <param name="totalBytes">The total number of bytes.</param>
        /// <param name="currentBytes">The current number of bytes processed.</param>
        /// <param name="speed">The speed of processing in bytes per second.</param>
        /// <returns>A TimeSpan representing the remaining time.</returns>
        public static TimeSpan ToTimeSpanRemain(int totalBytes, int currentBytes, float speed)
            => TimeSpan.FromSeconds((totalBytes - currentBytes) / Math.Max(speed, 1f));

        /// <summary>
        /// Calculates the percentage of progress.
        /// </summary>
        /// <param name="toProgress">The total progress value.</param>
        /// <param name="fromProgress">The current progress value.</param>
        /// <param name="decimalDigits">The number of decimal digits to round the result to. Default is 2.</param>
        /// <returns>A double representing the percentage of progress.</returns>
        public static double ToPercentage(double toProgress, double fromProgress, int decimalDigits = 2)
            => Math.Round(fromProgress / toProgress * 100, decimalDigits, MidpointRounding.ToEven);

        private static readonly SpanAction<char, nint> s_normalizePathReplaceCore = NormalizePathUnsafeCore;
        public static unsafe string NormalizePath(ReadOnlySpan<char> source, bool trimStart = true)
        {
            ReadOnlySpan<char> sourceTrimmed = trimStart ? source.TrimStart('/') : source;
            fixed (char* ptr = &MemoryMarshal.GetReference(sourceTrimmed))
            {
                return string.Create(sourceTrimmed.Length, (nint)ptr, s_normalizePathReplaceCore);
            }
        }

        public static unsafe void NormalizePathInplaceNoTrim(ReadOnlySpan<char> source)
        {
            fixed (char* ptr = &MemoryMarshal.GetReference(source))
            {
                Span<char> unlockedSource = new(ptr, source.Length);
                s_normalizePathReplaceCore(unlockedSource, (nint)ptr);
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

            return $"{Math.Round(value / (1L << (mag * 10)), decimalPlaces)} {SizeSuffixes[mag]}";
        }
        
        public static double SummarizeSizeDouble(double value, byte clampSize = byte.MaxValue)
        {
            byte maxClamp = (byte)Math.Log(value, 1000);
            if (clampSize == byte.MaxValue) clampSize = maxClamp;
            
            return value / (1L << (clampSize * 10));
        }

        public static int GetUnixTimestamp(bool isUtc = false) => (int)Math.Truncate(isUtc ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);

        private static readonly WindowsIdentity CurrentWindowsIdentity = WindowsIdentity.GetCurrent();
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
            FileSystemSecurity pathSecurity;
            if (!isFileType)
            {
                // Get directory ACL
                DirectoryInfo directoryInfo = new(input);
                pathSecurity = directoryInfo.GetAccessControl();
            }
            else
            {
                // Get file ACL
                FileInfo fileInfo = new(input);
                pathSecurity = fileInfo.GetAccessControl();
            }

            // If the path ACL is null, then return false (as not permitted)
            AuthorizationRuleCollection pathAcl = pathSecurity.GetAccessRules(true, true, typeof(NTAccount));

            // Get current Windows User Identity principal
            WindowsPrincipal principal = new(CurrentWindowsIdentity);

            // Do LINQ to check across available ACLs and ensure that the exact user has the rights to
            // access the file
            bool isHasAccess = pathAcl
                              .Cast<FileSystemAccessRule>()
                              .FirstOrDefault(x => IsPrincipalHasFileSystemAccess(principal, x) ?? false) != null;

            return isHasAccess;
        }

        private static bool? IsPrincipalHasFileSystemAccess(this WindowsPrincipal user, FileSystemAccessRule rule) => rule switch
        {
            { FileSystemRights: var fileSystemRights }
                when (fileSystemRights & (FileSystemRights.WriteData | FileSystemRights.Write)) == 0 => null,
            { IdentityReference: { Value: { } value } }
                when value.StartsWith("S-1-") && !user.IsInRole(new SecurityIdentifier(rule.IdentityReference.Value)) => null,
            { IdentityReference: { Value: { } value } }
                when value.StartsWith("S-1-") == false && !user.IsInRole(rule.IdentityReference.Value) => null,
            { AccessControlType: AccessControlType.Deny } => false,
            { AccessControlType: AccessControlType.Allow } => true,
            _ => null
        };

        public static float ConvertRangeValue(float sMin, float sMax, float sValue, float tMin, float tMax) => (sValue - sMin) * (tMax - tMin) / (sMax - sMin) + tMin;

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

        /// <summary>
        /// Asynchronously sums the results of a selector function applied to each element in a collection.
        /// </summary>
        /// <typeparam name="TFrom">The type of the elements in the collection.</typeparam>
        /// <typeparam name="TResult">The type of the result, which must be a struct implementing ISignedNumber.</typeparam>
        /// <param name="enums">The collection of elements to process.</param>
        /// <param name="selector">The asynchronous selector function to apply to each element.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A ValueTask representing the asynchronous operation, with a TResult result containing the sum of the selected values.</returns>
        /// <exception cref="NotSupportedException">Thrown if TResult is not a member of <typeparamref name="TResult"/>.</exception>
        public static async ValueTask<TResult> SumParallelAsync<TFrom, TResult>(
            this ICollection<TFrom> enums,
            GetSelectorSignedAsync<TFrom, TResult> selector,
            CancellationToken token = default)
            where TResult : struct, ISignedNumber<TResult>
        {
            // Allocate buffer to calculate
            int elementLen = enums.Count;
            TResult[] chunks = ArrayPool<TResult>.Shared.Rent(elementLen);

            // Clear the previous data
            Array.Clear(chunks);

            try
            {
                // If the element length is less than defined limit, then
                // use normal iteration to assign the value
                if (elementLen < 512)
                {
                    foreach ((int Index, TFrom Item) chunk in enums.Index())
                    {
                        chunks[chunk.Index] = await selector(chunk.Item, token);
                    }
                }
                // Otherwise, perform it in parallel
                else
                {
                    await Parallel.ForEachAsync(enums.Index(), new ParallelOptions
                    {
                        CancellationToken = token
                    },
                    async (chunk, ctx) =>
                    {
                        chunks[chunk.Index] = await selector(chunk.Item, ctx);
                    });
                }

                // Calculate the chunks using SIMD's .Sum() methods.
                switch (chunks)
                {
                    case int[] chunksAsInts:
                        {
                            object result = chunksAsInts.Sum();
                            return (TResult)result;
                        }
                    case long[] chunksAsLongs:
                        {
                            object result = chunksAsLongs.Sum();
                            return (TResult)result;
                        }
                    case float[] chunksAsFloats:
                        {
                            object result = chunksAsFloats.Sum();
                            return (TResult)result;
                        }
                    case double[] chunksAsDoubles:
                        {
                            object result = chunksAsDoubles.Sum();
                            return (TResult)result;
                        }
                    case decimal[] chunksAsDecimals:
                        {
                            object result = chunksAsDecimals.Sum();
                            return (TResult)result;
                        }
                    // If the type is not supported, throw
                    default:
                        throw new NotSupportedException($"Type of {typeof(TResult)} is not a member of ISignedNumber<T> or there is no overload on .Sum() method");
                }
            }
            finally
            {
                // Return the buffer
                ArrayPool<TResult>.Shared.Return(chunks);
            }
        }
    }
}
