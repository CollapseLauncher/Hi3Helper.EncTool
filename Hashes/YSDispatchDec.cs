using System;
using System.IO;
using System.Security.Cryptography;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.EncTool
{
    public class YSDispatchDec : MhyEncTool
    {
        public static byte[] DecryptYSDispatch(string contentBase64, int encBitLength, string encKey)
        {
            RSA rsa = RSA.Create();
            rsa.FromXmlString(encKey);

            byte[] encContent = Convert.FromBase64String(contentBase64);
            byte[] decContent = new byte[encContent.Length];

            if (encContent.Length % encBitLength != 0)
                throw new InvalidDataException($"The data length does not respect the expected size of the bit length, which is {encBitLength} bit. "
                                               + $"Size: {encContent.Length} % {encBitLength} = {encContent.Length % encBitLength}");

            int offsetEnc = 0;
            int offsetDec = 0;

            while (offsetEnc < encContent.Length)
            {
                offsetDec += rsa.Decrypt(encContent.AsSpan(offsetEnc, encBitLength), decContent.AsSpan(offsetDec),
                                         RSAEncryptionPadding.Pkcs1);
                offsetEnc += encBitLength;
            }

            return decContent[..offsetDec];
        }
    }
}
