using System.IO;

namespace Hi3Helper.EncTool.Misc
{
    internal static class StreamExtension
    {
        public static int ReadExactly(this Stream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = stream.Read(buffer, offset, count);
                if (read == 0) return totalRead;

                totalRead += read;
            }

            return totalRead;
        }
    }
}
