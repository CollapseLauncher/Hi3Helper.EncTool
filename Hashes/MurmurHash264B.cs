namespace Hi3Helper.EncTool.Hashes
{
    public static class MurmurHash264B
    {
        private const uint  M_32 = 0x5bd1e995;
        private const int   R_32 = 24;
        private const ulong A_M  = 0xc6a4a7935bd1e995;
        private const int   A_R  = 47;
        private const uint  B_M  = 0x5bd1e995;
        private const int   B_R  = 24;

        // TODO: Use SIMD and add support for Stream
        public static unsafe ulong ComputeHashUnsafe(byte* ptr, int len, ulong seed)
        {
            if (ptr == null || len == 0)
            {
                return seed;
            }

            uint h1 = ((uint)seed) ^ (uint)len;
            uint h2 = (uint)(seed >> 32);

            byte* end = ptr + len;
            while (ptr + 8 <= end)
            {
                uint k1 = *(uint*)ptr;
                k1 *= B_M;
                k1 ^= k1 >> B_R;
                k1 *= B_M;
                h1 *= B_M;
                h1 ^= k1;

                ptr += 4;

                uint k2 = *(uint*)ptr;
                k2 *= B_M;
                k2 ^= k2 >> B_R;
                k2 *= B_M;
                h2 *= B_M;
                h2 ^= k2;

                ptr += 4;
            }

            if (end >= ptr + 4)
            {
                uint k1 = *(uint*)ptr;
                k1 *= B_M;
                k1 ^= k1 >> B_R;
                k1 *= B_M;
                h1 *= B_M;
                h1 ^= k1;
                ptr += 4;
            }

            int remain = (int)(end - ptr);
            if (remain > 0)
            {
                switch (remain)
                {
                    case 3:
                        h2 ^= (uint)*(ptr + 2) << 16;
                        h2 ^= (uint)*(ptr + 1) << 8;
                        h2 ^= *ptr;
                        break;
                    case 2:
                        h2 ^= (uint)*(ptr + 1) << 8;
                        h2 ^= *ptr;
                        break;
                    case 1:
                        h2 ^= *ptr;
                        break;
                }
                ;
                h2 *= B_M;
            }

            h1 ^= h2 >> 18;
            h1 *= B_M;
            h2 ^= h1 >> 22;
            h2 *= B_M;
            h1 ^= h2 >> 17;
            h1 *= B_M;
            h2 ^= h1 >> 19;
            h2 *= B_M;

            ulong h = h1;

            h = (h << 32) | h2;

            return h;
        }
    }
}
