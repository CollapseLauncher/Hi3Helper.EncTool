using Hi3Helper.Data;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

#nullable enable
namespace Hi3Helper.EncTool.Enc;

public sealed class MhyEncTool
{
    public static bool TryGetSalt(
        ReadOnlySpan<char>                  keyString,
        Span<byte>                          masterKey,
        Span<byte>                          saltBuffer,
        out                      int        saltBufferWritten,
        [NotNullWhen(false)] out Exception? exception)
    {
        Unsafe.SkipInit(out exception);
        Unsafe.SkipInit(out saltBufferWritten);
        const int saltSize = sizeof(ulong);
        const int dataSize = 56;

        using RSA rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(masterKey, out _);

        Span<byte> hmacKeyEnc = stackalloc byte[keyString.Length / 2];
        Span<byte> hmacKeyDec = stackalloc byte[keyString.Length / 2];
        if (!HexTool.TryHexToBytesUnsafe(keyString, hmacKeyEnc))
        {
            exception = new InvalidOperationException("Input Key is not a hex string!");
            return false;
        }

        if (!rsa.TryDecrypt(hmacKeyEnc, hmacKeyDec, RSAEncryptionPadding.Pkcs1, out int hmacKeyWritten))
        {
            exception = new InvalidOperationException("Cannot decrypt data with the given Master key!");
            return false;
        }

        hmacKeyDec = hmacKeyDec[..hmacKeyWritten];

        // Sanity check
        int saltOffset = hmacKeyDec.Length - saltSize;
        if (hmacKeyDec.Length != dataSize)
        {
            exception = new InvalidOperationException("Data size is not valid!");
            return false;
        }

        ReadOnlySpan<byte> saltData = hmacKeyDec.Slice(saltOffset, saltSize);
        if (saltData.TryCopyTo(saltBuffer))
        {
            saltBufferWritten = saltSize;
            return true;
        }

        exception = new InvalidOperationException("Destination buffer size is too small!");
        return false;
    }
}
