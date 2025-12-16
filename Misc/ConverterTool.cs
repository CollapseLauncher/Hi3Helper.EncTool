using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
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
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable GrammarMistakeInComment
// ReSharper disable InconsistentNaming

#nullable enable
namespace Hi3Helper.Data
{
    public delegate ValueTask<TResult> GetSelectorSignedAsync<in TFrom, TResult>(TFrom item, CancellationToken token)
        where TResult : struct, ISignedNumber<TResult>;

    public static class ConverterTool
    {
        private const  double   ScOneSecond   = 1000;
        private static string[] _sizeSuffixes = ["B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];

        public static void SetSizeSuffixes(string[] suffixes)
        {
            if (suffixes.Length != _sizeSuffixes.Length)
            {
                throw new ArgumentException($"Suffixes must be in the same size, which is {_sizeSuffixes.Length} strings!");
            }

            _sizeSuffixes = suffixes;
        }

        public static unsafe bool TrySerializeStruct<T>(Span<byte> outputBuffer, out int bytesWritten, params ReadOnlySpan<T> input)
            where T : unmanaged
        {
            bytesWritten = 0;

            int inputLen = sizeof(T) * input.Length;
            if (outputBuffer.Length < inputLen)
            {
                return false;
            }

            ref T startOf = ref MemoryMarshal.GetReference(input);
            ref T endOf   = ref Unsafe.Add(ref startOf, input.Length);

            while (Unsafe.IsAddressLessThan(ref startOf, ref endOf))
            {
                if (!TrySerializeStruct(in startOf, ref bytesWritten, outputBuffer))
                {
                    return false;
                }

                startOf = ref Unsafe.Add(ref startOf, 1);
            }

            return true;
        }

        public static unsafe bool TrySerializeStruct<T>(in T input, ref int writeOffset, Span<byte> outputBuffer)
            where T : unmanaged
        {
            int sizeOf = sizeof(T);
            if (writeOffset + sizeOf > outputBuffer.Length)
            {
                return false;
            }

            MemoryMarshal.Write(outputBuffer[writeOffset..], in input);
            writeOffset += sizeOf;
            return true;
        }

        public static unsafe bool TryDeserializeStruct<T>(ReadOnlySpan<byte> data, out T[] result)
            where T : unmanaged
        {
            int dataLen = data.Length;
            int sizeOf  = sizeof(T);

            if (dataLen % sizeOf == 0)
            {
                return TryDeserializeStruct(data, dataLen / sizeOf, out result);
            }

            Unsafe.SkipInit(out result);
            return false;
        }

        public static unsafe bool TryDeserializeStruct<T>(ReadOnlySpan<byte> data, int elementCount, out T[] result)
            where T : unmanaged
        {
            int lengthOfResult = sizeof(T) * elementCount;
            if (data.Length < lengthOfResult)
            {
                Unsafe.SkipInit(out result);
                return false;
            }

            // Allocate uninitialized array (don't mind about arbitrary data, it should be overriden anyway)
            result = GC.AllocateUninitializedArray<T>(elementCount);

            ref T startOf = ref MemoryMarshal.GetArrayDataReference(result);
            ref T endOf   = ref Unsafe.Add(ref startOf, elementCount);

            ref byte dataStartOf = ref MemoryMarshal.GetReference(data);
            ref byte dataEndOf   = ref Unsafe.Add(ref dataStartOf, lengthOfResult);

            while (Unsafe.IsAddressLessThan(ref startOf, ref endOf) &&
                   Unsafe.IsAddressLessThan(ref dataStartOf, ref dataEndOf))
            {
                if (!TryDeserializeStruct(ref dataStartOf, out startOf))
                {
                    return false;
                }

                startOf     = ref Unsafe.Add(ref startOf, 1);
                dataStartOf = ref Unsafe.Add(ref dataStartOf, sizeof(T));
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int TryDeserializeStruct<T>(ReadOnlySpan<byte> data, int dataOffset, out T result)
            where T : unmanaged
        {
            ref byte dataRef = ref MemoryMarshal.GetReference(data[dataOffset..]);
            return !TryDeserializeStruct(ref dataRef, out result) ? -1 : sizeof(T);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TryDeserializeStruct<T>(scoped ref byte data, out T result)
            where T : allows ref struct
        {
            result = Unsafe.Read<T>(Unsafe.AsPointer(ref data));
            return true;
        }

        public static unsafe void GetListOfPaths(ReadOnlySpan<byte> input, out string[] outlist, long count)
        {
            outlist = GC.AllocateUninitializedArray<string>((int)count);
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

        // ReSharper disable once InconsistentNaming
        private static readonly SpanAction<char, nint> s_normalizePathReplaceCore = NormalizePathUnsafeCore;
        public static unsafe string NormalizePath(ReadOnlySpan<char> source, bool trimStart = true)
        {
            ReadOnlySpan<char> sourceTrimmed = trimStart ? source.TrimStart('/') : source;
            fixed (char* ptr = &MemoryMarshal.GetReference(sourceTrimmed))
            {
                return string.Create(sourceTrimmed.Length, (nint)ptr, s_normalizePathReplaceCore);
            }
        }

        public static unsafe void NormalizePathInplaceNoTrim(ReadOnlySpan<char> source, char replaceFrom = '/', char replaceTo = '\\')
        {
            Span<char> unlockedSource = source.UnsafeUnlockSpan(out void* ptr);
            NormalizePathUnsafeCore(unlockedSource, replaceFrom, replaceTo, ptr);
        }

        public static unsafe Span<T> UnsafeUnlockSpan<T>(this ReadOnlySpan<T> span)
            where T : unmanaged
            => new Span<T>(Unsafe.AsPointer(ref MemoryMarshal.GetReference(span)), span.Length);

        public static unsafe Span<T> UnsafeUnlockSpan<T>(this ReadOnlySpan<T> span, out void* ptr)
            where T : unmanaged
            => new Span<T>(ptr = Unsafe.AsPointer(ref MemoryMarshal.GetReference(span)), span.Length);

        // Reference: https://github.com/dotnet/aspnetcore/blob/c65dac77cf6540c81860a42fff41eb11b9804367/src/Shared/QueryStringEnumerable.cs#L169
        private static unsafe void NormalizePathUnsafeCore(Span<char> buffer, char replaceFrom, char replaceTo, void* state)
        {
            fixed (char* ptr = buffer)
            {
                ushort* input  = (ushort*)state;
                ushort* output = (ushort*)ptr;

                nint i = 0;
                nint n = (nint)(uint)buffer.Length;

                if (Sse41.IsSupported && n >= Vector128<ushort>.Count)
                {
                    Vector128<ushort> vecPlus  = Vector128.Create((ushort)replaceFrom);
                    Vector128<ushort> vecSpace = Vector128.Create((ushort)replaceTo);

                    do
                    {
                        Vector128<ushort> vec  = Sse2.LoadVector128(input + i);
                        Vector128<ushort> mask = Sse2.CompareEqual(vec, vecPlus);
                        Vector128<ushort> res  = Sse41.BlendVariable(vec, vecSpace, mask);

                        Sse2.Store(output + i, res);

                        i += Vector128<ushort>.Count;

                    } while (i <= n - Vector128<ushort>.Count);
                }

                for (; i < n; ++i)
                {
                    if (input[i] != replaceFrom)
                    {
                        output[i] = input[i];
                    }
                    else
                    {
                        output[i] = replaceTo;
                    }
                }
            }
        }

        private static unsafe void NormalizePathUnsafeCore(Span<char> buffer, nint state)
            => NormalizePathUnsafeCore(buffer, '/', '\\', (void*)state);

        public static string SummarizeSizeSimple(double value, int decimalPlaces = 2)
        {
            int mag = (int)Math.Log(value, 1000);
            mag = Math.Clamp(mag, 0, _sizeSuffixes.Length - 1);

            return $"{Math.Round(value / (1L << (mag * 10)), decimalPlaces)} {_sizeSuffixes[mag]}";
        }
        
        public static double SummarizeSizeDouble(double value, byte clampSize = byte.MaxValue)
        {
            byte maxClamp = (byte)Math.Log(value, 1000);
            if (clampSize == byte.MaxValue) clampSize = maxClamp;
            
            return value / (1L << (clampSize * 10));
        }

        public static int GetUnixTimestamp(bool isUtc = false) => (int)Math.Truncate(isUtc ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);

#pragma warning disable CA1416
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
            { IdentityReference.Value: { } value }
                when value.StartsWith("S-1-") && !user.IsInRole(new SecurityIdentifier(rule.IdentityReference.Value)) => null,
            { IdentityReference.Value: { } value }
                when !value.StartsWith("S-1-") && !user.IsInRole(rule.IdentityReference.Value) => null,
            { AccessControlType: AccessControlType.Deny } => false,
            { AccessControlType: AccessControlType.Allow } => true,
            _ => null
        };
#pragma warning restore CA1416

        public static float ConvertRangeValue(float sMin, float sMax, float sValue, float tMin, float tMax) => (sValue - sMin) * (tMax - tMin) / (sMax - sMin) + tMin;

        public static string CombineURLFromString(this string? baseUrl, params ReadOnlySpan<string?> segments)
            => CombineURLFromString(baseUrl.AsSpan(), segments);

        public static unsafe string CombineURLFromString(ReadOnlySpan<char> baseUrl, params ReadOnlySpan<string?> segments)
        {
            // Assign the size of a char as constant
            const uint sizeOfChar = sizeof(char);

            // Get the base URL length and decrement by 1 if the end of the index (^1)
            // is a '/' character. Otherwise, nothing to decrement.
            // 
            // Once we get a length of the base URL, get a sum of all lengths
            // of the segment's span.
            int baseUrlLen = baseUrl.Length - (baseUrl[^1] == '/' ? 1 : 0);
            int bufferLen = baseUrlLen + SumSegmentsLength(segments);
            uint toWriteBase = (uint)baseUrlLen;

            // Allocate temporary buffer from the shared ArrayPool<T>
            char[] buffer = ArrayPool<char>.Shared.Rent(bufferLen);

            // Here we start to do something UNSAFE >:)
            // Get the base and last (to written position) pointers of the buffer array.
            fixed (char* bufferPtr = &MemoryMarshal.GetArrayDataReference(buffer))
            {
                char* bufferWrittenPtr = bufferPtr;

                // Get a base pointer of the baseUrl span
                fixed (char* baseUrlPtr = &MemoryMarshal.GetReference(baseUrl))
                {
                    // Perform intrinsic copy for the specific block of memory from baseUrlPtr
                    // into the buffer pointer.
                    Unsafe.CopyBlock(bufferWrittenPtr, baseUrlPtr, toWriteBase * sizeOfChar);
                    bufferWrittenPtr += toWriteBase;
                    try
                    {
                        // Set the initial position of the segment index
                        int i = 0;

                    // Perform the segment copy loop routine
                    CopySegments:
                        // If the index is equal to the length of the segment, which means...
                        // due to i being 0, it should expect the length of the segments span as well.
                        // Means, if 0 == 0, then quit from CopySegments routine and jump right
                        // into CreateStringFast routine.
                        if (i == segments.Length)
                            goto CreateStringFast;

                        // Get a span of the current segment while in the meantime, trim '/' character
                        // from the start and the end of the span. In the meantime, increment
                        // the index of the segments span.
                        ReadOnlySpan<char> segment = segments[i++].AsSpan().Trim('/');
                        // If the segment span is actually empty, (means either the initial value or
                        // after it's getting trimmed [for example, "//"]), then move to another
                        // segment to merge.
                        if (segment.IsEmpty) goto CopySegments;

                        // Check if the segment starts with '?' character (means the segment is a query
                        // and not a relative path), then write a '/' character into the buffer and moving
                        // by 1 byte of the index.
                        bool isQuery = segment[0] == '?';
                        if (!isQuery)
                            *bufferWrittenPtr++ = '/';

                        // Get a base pointer of the current segment and get its length.
                        uint segmentLen = (uint)segment.Length;
                        fixed (void* segmentPtr = &MemoryMarshal.GetReference(segment))
                        {
                            // Perform the intrinsic copy for the specific block of memory from the
                            // current segment pointer into the buffer pointer.
                            Unsafe.CopyBlock(bufferWrittenPtr, segmentPtr, segmentLen * sizeOfChar);
                            // Move the position of the written buffer pointer
                            bufferWrittenPtr += segmentLen;
                            // Back to the start of the loop routine
                            goto CopySegments;
                        }

                        CreateStringFast:
                        // Perform a return string creation by how much data being written into the buffer by decrementing
                        // bufferWrittenPtr with initial base pointer, bufferPtr.
                        string returnString = new string(bufferPtr, 0, (int)(bufferWrittenPtr - bufferPtr));
                        // Then return the string
                        return returnString;
                    }
                    finally
                    {
                        // Return the write buffer to save memory from being unnecessarily allocated.
                        ArrayPool<char>.Shared.Return(buffer);
                    }
                }
            }

            static int SumSegmentsLength(ReadOnlySpan<string?> segmentsInner)
            {
                // If the span is empty, then return 0 (as no segments to be merged)
                if (segmentsInner.IsEmpty)
                    return 0;

                // Start incrementing sum in backward
                int sum = 0;
                int i = segmentsInner.Length;

            // Do the loop.
            LenSum:
                // ?? as means if the current index of span is null, nothing to increment (0).
                // Also, decrement the index as we are summing the length backwards.
                sum += segmentsInner[--i]?.Length ?? 0;
                if (i > 0)
                    // Back to the loop if the index is not yet zero.
                    goto LenSum;

                // If no routines left, return the total sum.
                return sum;
            }
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

        public static double CalculateSpeed(long receivedBytes, ref double lastSpeedToUse, ref long lastReceivedBytesToUse, ref long lastTickToUse)
        {
            long nowTicks = Environment.TickCount64;

            long   currentTick           = nowTicks - lastTickToUse + 1;
            long   totalReceivedInSecond = Interlocked.Add(ref lastReceivedBytesToUse, receivedBytes);
            double speed                 = totalReceivedInSecond * ScOneSecond / currentTick;

            if (!(currentTick > ScOneSecond))
            {
                return lastSpeedToUse;
            }

            _ = Interlocked.Exchange(ref lastSpeedToUse,         speed);
            _ = Interlocked.Exchange(ref lastTickToUse,          nowTicks);
            _ = Interlocked.Exchange(ref lastReceivedBytesToUse, 0);
            return lastSpeedToUse;
        }

        public static bool IsPastOneSecond(ref long lastTick)
        {
            long now = DateTimeOffset.Now.Ticks;

            if (now - lastTick < 10000000)
            {
                return false;
            }

            lastTick = now;
            return true;
        }

        public static void WriteLeftRightMessage(string leftMessage, string rightMessage)
        {
            int consoleSpaceRemain = Math.Max(Console.WindowWidth - (leftMessage.Length + rightMessage.Length + 1), 0);
            Console.Write(leftMessage + new string(' ', consoleSpaceRemain) + rightMessage + '\r');
        }

        public static Uri GetStringAsUri(this string asStringSource)
        {
            // Try to create URL with absolute path.
            // If not (assume it's a relative local path), then try to get the fully qualified local path.
            if (Uri.TryCreate(asStringSource, UriKind.Absolute, out Uri? sourceUri) ||
                Path.IsPathFullyQualified(asStringSource))
            {
                return sourceUri ?? new Uri(asStringSource);
            }

            string             currentWorkingDir  = Directory.GetCurrentDirectory();
            ReadOnlySpan<char> asStringSourceSpan = asStringSource.Trim("/\\");
            asStringSource = Path.Join(currentWorkingDir, asStringSourceSpan);

            return sourceUri ?? new Uri(asStringSource);
        }

        public static double TryGetDouble(this object? obj)
        {
            return obj switch
            {
                sbyte asSbyte   => asSbyte,
                byte asByte     => asByte,
                ushort asUshort => asUshort,
                short asShort   => asShort,
                uint asUint     => asUint,
                int asInt       => asInt,
                ulong asUlong   => asUlong,
                long asLong     => asLong,
                float asFloat   => asFloat,
                double asDouble => asDouble,
                _               => double.NaN
            };
        }
    }
}
