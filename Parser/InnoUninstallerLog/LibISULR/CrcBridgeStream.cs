using Hi3Helper.Data;
using System;
using System.Data;
using System.IO;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Hi3Helper.EncTool.Parser.InnoUninstallerLog
{
    public class CrcBridgeStream : Stream
    {
        private const int _defaultBufferLen = 4 << 10;

        private readonly Stream sourceStream;
        private readonly bool leaveOpen;

        private byte[] blockBuffer = null;
        private bool skipCrcCheck;
        private bool isWriteMode;

        private int dataAvailable;
        private int dataPos;

        public CrcBridgeStream(Stream sourceStream, bool leaveOpen = false, bool skipCrcCheck = false, bool isWriteMode = false)
        {
            this.sourceStream = sourceStream;
            this.leaveOpen = leaveOpen;
            this.skipCrcCheck = skipCrcCheck;
            this.isWriteMode = isWriteMode;
            if (!isWriteMode) FillBuffer();
        }

        ~CrcBridgeStream() => Dispose();

        private void FlushBuffer()
        {
            if (dataPos != 0) FinalizeBlock();

            dataAvailable = _defaultBufferLen;
            blockBuffer = new byte[dataAvailable];

            dataPos = 0;
        }

        public void FinalizeBlock()
        {
            uint crcHash = Crc32.HashToUInt32(blockBuffer.AsSpan(0, dataPos));
            int writtenLen = dataPos;
            int notSize = ~writtenLen;

            TUninstallCrcHeader blockHeader = new TUninstallCrcHeader
            {
                Crc = crcHash,
                NotSize = (uint)notSize,
                Size = (uint)writtenLen
            };

            int offsetDummy = 0;
            int blockHeaderSizeOf = Marshal.SizeOf<TUninstallCrcHeader>();
            byte[] headerBuffer = new byte[blockHeaderSizeOf];
            ConverterTool.TrySerializeStruct(blockHeader, ref offsetDummy, headerBuffer);
            sourceStream.Write(headerBuffer);
            sourceStream.Write(blockBuffer, 0, writtenLen);
        }

        private void FillBuffer()
        {
            // Get the structure of the block info (including its Crc)
            InnoUninstLog.ReadTStructure(sourceStream, out TUninstallCrcHeader blockHeader);
            if (blockBuffer == null || blockBuffer.Length != blockHeader.Size)
                blockBuffer = new byte[blockHeader.Size];

            // SANITY CHECK: Check the buffer size if it's matching or not
            if (!skipCrcCheck && blockHeader.Size != ~blockHeader.NotSize)
                throw new Exception($"File buffer size record is not match from {blockHeader.Size} to ~{blockHeader.NotSize} (or {~blockHeader.NotSize})");

            // Try load the buffer and check for the Crc
            dataPos = 0;
            dataAvailable = sourceStream.ReadAtLeast(blockBuffer, (int)blockHeader.Size);
            uint crcHash = Crc32.HashToUInt32(blockBuffer.AsSpan(0, dataAvailable));

            // Check the Crc
            if (!skipCrcCheck && crcHash != blockHeader.Crc)
                throw new DataException($"Header Crc32 isn't match! Getting {crcHash} while expecting {blockHeader.Crc}");
        }

        private int ReadBytes(byte[] buffer, int offset, int count)
        {
            if (isWriteMode) throw new InvalidOperationException($"You can't do read operation while stream is in write mode!");
            while (count > 0)
            {
                if (dataAvailable == 0)
                    FillBuffer();

                int dataToRead = count;
                if (dataToRead > dataAvailable)
                    dataToRead = dataAvailable;

                Array.Copy(blockBuffer, dataPos, buffer, offset, dataToRead);
                offset += dataToRead;
                count -= dataToRead;
                dataPos += dataToRead;
                dataAvailable -= dataToRead;
            }
            return offset;
        }

        private int ReadBytes(Span<byte> buffer)
        {
            if (isWriteMode) throw new InvalidOperationException($"You can't do read operation while stream is in write mode!");
            int count = buffer.Length;
            int offset = 0;
            while (count > 0)
            {
                if (dataAvailable == 0)
                    FillBuffer();

                int dataToRead = count;
                if (dataToRead > dataAvailable)
                    dataToRead = dataAvailable;

                blockBuffer.AsSpan(dataPos, dataToRead)
                    .CopyTo(buffer.Slice(offset, dataToRead));

                offset += dataToRead;
                count -= dataToRead;
                dataPos += dataToRead;
                dataAvailable -= dataToRead;
            }
            return offset;
        }

        private void WriteBytes(byte[] buffer, int offset, int count)
        {
            if (!isWriteMode) throw new InvalidOperationException($"You can't do write operation while stream isn't in write mode!");
            while (count > 0)
            {
                if (dataAvailable == 0)
                    FlushBuffer();

                int dataToWrite = count;
                if (dataToWrite > dataAvailable)
                    dataToWrite = dataAvailable;

                Array.Copy(buffer, offset, blockBuffer, dataPos, dataToWrite);
                offset += dataToWrite;
                count -= dataToWrite;
                dataPos += dataToWrite;
                dataAvailable -= dataToWrite;
            }
        }

        private void WriteBytes(ReadOnlySpan<byte> buffer)
        {
            if (!isWriteMode) throw new InvalidOperationException($"You can't do write operation while stream isn't in write mode!");
            int count = buffer.Length;
            int offset = 0;
            while (count > 0)
            {
                if (dataAvailable == 0)
                    FlushBuffer();

                int dataToWrite = count;
                if (dataToWrite > dataAvailable)
                    dataToWrite = dataAvailable;

                buffer.Slice(offset, dataToWrite)
                    .CopyTo(blockBuffer.AsSpan(dataPos, dataToWrite));

                offset += dataToWrite;
                count -= dataToWrite;
                dataPos += dataToWrite;
                dataAvailable -= dataToWrite;
            }
        }

        public override int Read(byte[] buffer, int offset, int count) => ReadBytes(buffer, offset, count);
        public override int Read(Span<byte> buffer) => ReadBytes(buffer);
        public override void Write(byte[] buffer, int offset, int count) => WriteBytes(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => WriteBytes(buffer);

        public override bool CanRead
        {
            get { return !isWriteMode; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return isWriteMode; }
        }

        public override void Flush() => sourceStream.Flush();

        public override long Length
        {
            get { return sourceStream.Length; }
        }

        public override long Position
        {
            get { return sourceStream.Position - dataAvailable; }
            set { throw new NotSupportedException(); }
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (!leaveOpen) sourceStream.Dispose();
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (!leaveOpen) await sourceStream.DisposeAsync();
        }
    }
}
