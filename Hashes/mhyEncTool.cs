using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;


namespace Hi3Helper.EncTool
{
    public class MhyEncTool
    {
        public    RSA                  Ooh;
        public    string               _778;
        protected HMACSHA1             Sha;
        protected RSA                  MasterKeyRsa;
        protected string               MasterKey = "";
        protected int                  MasterKeyBitLength;
        protected RSAEncryptionPadding MasterKeyPadding;

        protected readonly Dictionary<char, byte> __951 = new()
        {
            { 'a', 0xA },{ 'b', 0xB },{ 'c', 0xC },{ 'd', 0xD },
            { 'e', 0xE },{ 'f', 0xF },{ 'A', 0xA },{ 'B', 0xB },
            { 'C', 0xC },{ 'D', 0xD },{ 'E', 0xE },{ 'F', 0xF },
            { '0', 0x0 },{ '1', 0x1 },{ '2', 0x2 },{ '3', 0x3 },
            { '4', 0x4 },{ '5', 0x5 },{ '6', 0x6 },{ '7', 0x7 },
            { '8', 0x8 },{ '9', 0x9 }
        };

        protected readonly byte[] SKey =
        [
            232, 170, 135, 231,
            189, 170, 227, 130,
            134, 227, 129, 132
        ];

        public MhyEncTool() { }

        public MhyEncTool(string i)
        {
            _778 = i;
            Ooh = RSA.Create();
        }

        public MhyEncTool(string i, string masterKey)
        {
            _778 = i;
            Ooh = RSA.Create();
            MasterKey = masterKey;
        }

        public MhyEncTool(string i, byte[] masterKey)
        {
            _778 = i;
            Ooh = RSA.Create();
            Ooh.ImportRSAPrivateKey(masterKey, out int _);
        }

        public string GetMasterKey() => MasterKey;

        public void InitMasterKey(string key, int keyBitLength, RSAEncryptionPadding keyPadding)
        {
            MasterKeyRsa = RSA.Create();
            MasterKey = Encoding.UTF8.GetString(_f8j51(key));
            MasterKeyBitLength = keyBitLength;
            MasterKeyPadding = keyPadding;
            FromXmlStringA(MasterKeyRsa, MasterKey);
        }

        public byte[] GetSalt()
        {
            byte[] cyA = null;
            byte[] hR  = null;
            byte[] at  = null;
            while (true)
            {
                int num = -379967069;
                while (true)
                {
                    uint num2;
                    switch ((num2 = unchecked((uint)num) ^ 0xD91CA180u) % 8u)
                    {
                        case 0u:
                            break;
                        case 3u:
                            cyA = new byte[8];
                            num = (int)((num2 * 265579718) ^ 0x2AB6A14D);
                            continue;
                        case 2u:
                            num = (int)((num2 * 1194515468) ^ 0x6BE031F1);
                            continue;
                        case 5u:
                            hR = cyA;
                            num = ((int)num2 * -1066675674) ^ 0xD7A97C;
                            continue;
                        case 6u:
                            num = (int)(num2 * 1342003605) ^ -355963254;
                            continue;
                        case 4u:
                            Array.Copy(Ooh.Decrypt(at!, RSAEncryptionPadding.Pkcs1), 48, cyA!, 0, 8);
                            num = ((int)num2 * -1711149688) ^ -181350819;
                            continue;
                        case 7u:
                            at = HTb(_778);
                            num = ((int)num2 * -1995578406) ^ 0x57CB1D8;
                            continue;
                        default:
                            return hR;
                    }
                    break;
                }
            }
        }

        internal void FromXmlStringA(in RSA rsa, string xmlString = null)
        {
            if (string.IsNullOrEmpty(xmlString)) xmlString = _778;
            rsa.FromXmlString(xmlString);
        }

        internal static byte[] DecryptRsaContent(in RSA rsa, string contentBase64, int encBitLength, RSAEncryptionPadding padding)
        {
            byte[] encContent = Convert.FromBase64String(contentBase64);
            MemoryStream decContent = new MemoryStream();

            int j = 0;

            while (j < encContent.Length)
            {
                byte[] chunk = new byte[encBitLength];
                Array.Copy(encContent, j, chunk, 0, encBitLength);
                byte[] chunkDec = rsa.Decrypt(chunk, padding);
                decContent.Write(chunkDec, 0, chunkDec.Length);
                j += encBitLength;
            }

            return decContent.ToArray();
        }

        private byte[] HTb(string a)
        {
            byte[] p49 = new byte[a.Length / 2];
            bool f = false;
            int n94 = 0;
            int _001 = 0;
            while (true)
            {
                int kk1 = -1675277297;
                while (true)
                {
                    uint lo051;
                    switch ((lo051 = unchecked((uint)kk1) ^ 0x8D7A7B5Fu) % 9u)
                    {
                        case 2u:
                            break;
                        case 5u:
                            {
                                int _0051;
                                if (!f)
                                {
                                    _0051 = -1380326733;
                                }
                                else
                                {
                                    _0051 = -189009401;
                                }
                                kk1 = _0051 ^ (int)(lo051 * 2073484105);
                                continue;
                            }
                        case 3u:
                            n94 = 0;
                            kk1 = (int)((lo051 * 2059650746) ^ 0x70BDC4E2);
                            continue;
                        case 8u:
                            {
                                char c = a[n94];
                                char c2 = a[n94 + 1];
                                p49[_001] = (byte)((__951[c] << 4) | __951[c2]);
                                kk1 = (int)((lo051 * 1839013216) ^ 0x5C090E6D);
                                continue;
                            }
                        case 1u:
                            _001 = 0;
                            kk1 = (int)((lo051 * 316152874) ^ 0x4220ABE6);
                            continue;
                        case 6u:
                            f = n94 < a.Length;
                            kk1 = -7386813;
                            continue;
                        case 0u:
                            n94 += 2;
                            _001++;
                            kk1 = (int)((lo051 * 196220108) ^ 0x5B6DC890);
                            continue;
                        case 7u:
                            kk1 = -742839246;
                            continue;
                        default:
                            return p49;
                    }
                    break;
                }
            }
        }

        internal byte[] _f8j51(string c)
        {
            byte[] ar84 = new byte[c.Length / 2];
            int _445 = 0;
            int nudE = 0;
            byte[] r = null;
            while (true)
            {
                int kj9A = -415293042;
                while (true)
                {
                    uint _99Jm1;
                    switch ((_99Jm1 = unchecked((uint)kj9A) ^ 0xD88AD053u) % 8u)
                    {
                        case 3u:
                            break;
                        case 5u:
                            _445 = 0;
                            nudE = 0;
                            kj9A = ((int)_99Jm1 * -1420181188) ^ 0x2336EBD;
                            continue;
                        case 2u:
                            kj9A = ((int)_99Jm1 * -336838899) ^ -1483897312;
                            continue;
                        case 0u:
                            nudE++;
                            kj9A = (int)(_99Jm1 * 1698348363) ^ -1786813222;
                            continue;
                        case 4u:
                            r = ar84;
                            kj9A = (int)(_99Jm1 * 1245963323) ^ -23762559;
                            continue;
                        case 1u:
                            {
                                if (_445 >= c.Length)
                                {
                                    kj9A = -481382921;
                                }
                                else
                                {
                                    kj9A = -1714879556;
                                }
                                continue;
                            }
                        case 7u:
                            {
                                byte b = (byte)((__951[c[_445]] << 4) | __951[c[_445 + 1]]);
                                ar84[nudE] = (byte)(b ^ SKey[nudE % SKey.Length]);
                                _445 += 2;
                                kj9A = -1908881005;
                                continue;
                            }
                        default:
                            return r;
                    }
                    break;
                }
            }
        }
    }
}
