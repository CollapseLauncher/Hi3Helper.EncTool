using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Hi3Helper.EncTool
{
    public class MhyEncTool
    {
        public    RSA      Ooh;
        public    string   _778;
        protected HMACSHA1 Sha;

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

        protected MhyEncTool() { }

        public MhyEncTool(string i, byte[] masterKey)
        {
            _778 = i;
            Ooh = RSA.Create();
            Ooh.ImportRSAPrivateKey(masterKey, out int _);
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
    }
}
