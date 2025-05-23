﻿#nullable enable
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
#if !NET9_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Hi3Helper.Data
{
    public
#if !NET9_0_OR_GREATER
        unsafe
#endif
        class HexTool
    {
#if !NET9_0_OR_GREATER
        private static readonly byte[] _lookupFromHexTable = new byte[] {
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 0,   1,
            2,   3,   4,   5,   6,   7,   8,   9,   255, 255,
            255, 255, 255, 255, 255, 10,  11,  12,  13,  14,
            15,  255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 10,  11,  12,
            13,  14,  15
        };

        private static readonly byte[] _lookupFromHexTable16 = new byte[] {
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 0,   16,
            32,  48,  64,  80,  96,  112, 128, 144, 255, 255,
            255, 255, 255, 255, 255, 160, 176, 192, 208, 224,
            240, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 160, 176, 192,
            208, 224, 240
        };

        private static readonly uint[] _lookup32Unsafe = new uint[]
        {
            0x300030, 0x310030, 0x320030, 0x330030, 0x340030, 0x350030, 0x360030, 0x370030, 0x380030, 0x390030, 0x610030, 0x620030,
            0x630030, 0x640030, 0x650030, 0x660030, 0x300031, 0x310031, 0x320031, 0x330031, 0x340031, 0x350031, 0x360031, 0x370031,
            0x380031, 0x390031, 0x610031, 0x620031, 0x630031, 0x640031, 0x650031, 0x660031, 0x300032, 0x310032, 0x320032, 0x330032,
            0x340032, 0x350032, 0x360032, 0x370032, 0x380032, 0x390032, 0x610032, 0x620032, 0x630032, 0x640032, 0x650032, 0x660032,
            0x300033, 0x310033, 0x320033, 0x330033, 0x340033, 0x350033, 0x360033, 0x370033, 0x380033, 0x390033, 0x610033, 0x620033,
            0x630033, 0x640033, 0x650033, 0x660033, 0x300034, 0x310034, 0x320034, 0x330034, 0x340034, 0x350034, 0x360034, 0x370034,
            0x380034, 0x390034, 0x610034, 0x620034, 0x630034, 0x640034, 0x650034, 0x660034, 0x300035, 0x310035, 0x320035, 0x330035,
            0x340035, 0x350035, 0x360035, 0x370035, 0x380035, 0x390035, 0x610035, 0x620035, 0x630035, 0x640035, 0x650035, 0x660035,
            0x300036, 0x310036, 0x320036, 0x330036, 0x340036, 0x350036, 0x360036, 0x370036, 0x380036, 0x390036, 0x610036, 0x620036,
            0x630036, 0x640036, 0x650036, 0x660036, 0x300037, 0x310037, 0x320037, 0x330037, 0x340037, 0x350037, 0x360037, 0x370037,
            0x380037, 0x390037, 0x610037, 0x620037, 0x630037, 0x640037, 0x650037, 0x660037, 0x300038, 0x310038, 0x320038, 0x330038,
            0x340038, 0x350038, 0x360038, 0x370038, 0x380038, 0x390038, 0x610038, 0x620038, 0x630038, 0x640038, 0x650038, 0x660038,
            0x300039, 0x310039, 0x320039, 0x330039, 0x340039, 0x350039, 0x360039, 0x370039, 0x380039, 0x390039, 0x610039, 0x620039,
            0x630039, 0x640039, 0x650039, 0x660039, 0x300061, 0x310061, 0x320061, 0x330061, 0x340061, 0x350061, 0x360061, 0x370061,
            0x380061, 0x390061, 0x610061, 0x620061, 0x630061, 0x640061, 0x650061, 0x660061, 0x300062, 0x310062, 0x320062, 0x330062,
            0x340062, 0x350062, 0x360062, 0x370062, 0x380062, 0x390062, 0x610062, 0x620062, 0x630062, 0x640062, 0x650062, 0x660062,
            0x300063, 0x310063, 0x320063, 0x330063, 0x340063, 0x350063, 0x360063, 0x370063, 0x380063, 0x390063, 0x610063, 0x620063,
            0x630063, 0x640063, 0x650063, 0x660063, 0x300064, 0x310064, 0x320064, 0x330064, 0x340064, 0x350064, 0x360064, 0x370064,
            0x380064, 0x390064, 0x610064, 0x620064, 0x630064, 0x640064, 0x650064, 0x660064, 0x300065, 0x310065, 0x320065, 0x330065,
            0x340065, 0x350065, 0x360065, 0x370065, 0x380065, 0x390065, 0x610065, 0x620065, 0x630065, 0x640065, 0x650065, 0x660065,
            0x300066, 0x310066, 0x320066, 0x330066, 0x340066, 0x350066, 0x360066, 0x370066, 0x380066, 0x390066, 0x610066, 0x620066,
            0x630066, 0x640066, 0x650066, 0x660066
        };

        private static readonly uint* _lookup32UnsafeP = (uint*)GCHandle.Alloc(_lookup32Unsafe, GCHandleType.Pinned).AddrOfPinnedObject();

        public static string LongToHexUnsafe(long number)
        {
            uint* lookupP = &_lookup32UnsafeP[0];
            ReadOnlySpan<char> result = stackalloc char[8];
            byte* bytesP = (byte*)&number;
            fixed (char* resultP = &result[0])
            {
                uint* resultP2 = (uint*)resultP;
                for (int i = 0; i < 8; i++)
                {
                    resultP2[i] = lookupP[bytesP[i]];
                }
            }
            return new string(result);
        }
#endif

        public static string? BytesToHexUnsafe(ReadOnlySpan<byte> bytes)
        {
#if NET9_0_OR_GREATER
            if (bytes.IsEmpty)
                return null;

            int returnLen = bytes.Length * 2;
            {
                char[] returnChar = returnLen > (2 << 10) / 2 ? GC.AllocateUninitializedArray<char>(returnLen) : new char[returnLen];
                if (!TryBytesToHexUnsafe(bytes, returnChar, out _))
                {
                    throw new InvalidOperationException($"Cannot convert {nameof(bytes)} to Hex string");
                }

                return new string(returnChar);
            }
#else
            if (bytes.Length == 0)
                return null;

            uint* lookupP = &_lookup32UnsafeP[0];
            ReadOnlySpan<char> result = stackalloc char[bytes.Length * 2];
            fixed (byte* bytesP = &bytes[0])
                fixed (char* resultP = &result[0])
                {
                    uint* resultP2 = (uint*)resultP;
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        resultP2[i] = lookupP[bytesP[i]];
                    }
                }
            return new string(result);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryBytesToHexUnsafe(ReadOnlySpan<byte> bytes, Span<char> result)
#if !NET9_0_OR_GREATER
        {
            uint* lookupP = &_lookup32UnsafeP[0];
            fixed (byte* bytesP = &bytes[0])
            fixed (char* resultP = &result[0])
            {
                uint* resultP2 = (uint*)resultP;
                for (int i = 0; i < bytes.Length; i++)
                {
                    resultP2[i] = lookupP[bytesP[i]];
                }
            }
            return true;
        }
#else
            => TryBytesToHexUnsafe(bytes, result, out _);
#endif

#if NET9_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryBytesToHexUnsafe(ReadOnlySpan<byte> bytes, Span<char> result, out int bytesWritten)
            => Convert.TryToHexStringLower(bytes, result, out bytesWritten);
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] HexToBytesUnsafe(ReadOnlySpan<char> source)
        {
            if (source.IsEmpty) return [];
            if (source.Length % 2 == 1)
                throw new IndexOutOfRangeException($"The length of the {nameof(source)} must be even!");

#if NET9_0_OR_GREATER
            int    returnLen   = source.Length / 2;
            byte[] returnBytes = returnLen > 2 << 10 ? GC.AllocateUninitializedArray<byte>(returnLen) : new byte[returnLen];

            OperationStatus result = TryHexToBytesUnsafe(source, returnBytes, out _, out _);
            if (OperationStatus.Done != result)
                throw new InvalidOperationException($"Cannot decode Hex to Bytes with operation status: {result}");

            return returnBytes;
#else
            int index = 0;
            int len = source.Length >> 1;

            fixed (char* sourceRef = &source[0])
            {
                if (*(int*)sourceRef == 7864368)
                {
                    if (source.Length == 2)
                    {
                        throw new ArgumentException();
                    }

                    index += 2;
                    len -= 1;
                }

                byte add = 0;
                byte[] result = new byte[len];

                fixed (byte* hiRef = &_lookupFromHexTable16[0])
                fixed (byte* lowRef = &_lookupFromHexTable[0])
                fixed (byte* resultRef = &result[0])
                {
                    char* s = &sourceRef[index];
                    byte* r = &resultRef[0];

                    while (*s != 0)
                    {
                        if (*s > 102 || (*r = hiRef[*s++]) == 255 || *s > 102 || (add = lowRef[*s++]) == 255)
                        {
                            throw new ArgumentException();
                        }
                        *r++ += add;
                    }
                    return result;
                }
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryHexToBytesUnsafe(ReadOnlySpan<char> source, Span<byte> buffer)
#if !NET9_0_OR_GREATER
        {
            int index = 0;
            int len = buffer.Length;

            fixed (char* sourceRef = &source[0])
            {
                if (*(int*)sourceRef == 7864368)
                {
                    if (source.Length == 2)
                    {
                        return false;
                    }

                    index += 2;
                    len -= 1;
                }

                byte add = 0;

                fixed (byte* hiRef = &_lookupFromHexTable16[0])
                fixed (byte* lowRef = &_lookupFromHexTable[0])
                fixed (byte* resultRef = &buffer[0])
                {
                    char* s = &sourceRef[index];
                    byte* r = &resultRef[0];

                    while (*s != 0)
                    {
                        if (*s > 102 || (*r = hiRef[*s++]) == 255 || *s > 102 || (add = lowRef[*s++]) == 255)
                        {
                            return false;
                        }
                        *r++ += add;
                    }
                    return true;
                }
            }
        }
#else
            => OperationStatus.Done == TryHexToBytesUnsafe(source, buffer, out _, out _);
#endif

#if NET9_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OperationStatus TryHexToBytesUnsafe(ReadOnlySpan<char> source, Span<byte> buffer, out int charsConsumed, out int bytesWritten)
            => Convert.FromHexString(source, buffer, out charsConsumed, out bytesWritten);
#endif
    }
}
