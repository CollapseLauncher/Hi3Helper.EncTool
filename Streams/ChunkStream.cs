﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.EncTool
{
    public sealed class ChunkStream : Stream
    {
        private long _start { get; set; }
        private long _end { get; set; }
        private long _size { get => _end - _start; }
        private long _curPos { get; set; }
        private long _remain { get => _size - _curPos; }
        private readonly Stream _stream;
        private bool _isDisposing { get; set; }

        public ChunkStream(Stream stream, long start, long end, bool isDisposing = false)
            : base()
        {
            _stream = stream;

            if (_stream.Length == 0)
            {
                throw new Exception("The stream must not have 0 bytes!");
            }

            if (_stream.Length < start || end > _stream.Length)
            {
                throw new ArgumentOutOfRangeException("Offset is out of stream size range!");
            }

            _stream.Position = start;
            _start = start;
            _end = end;
            _curPos = 0;
            _isDisposing = isDisposing;
        }

        ~ChunkStream() => this.Dispose(_isDisposing);

        public override int Read(Span<byte> buffer)
        {
            if (_remain == 0) return 0;

            int toSlice = (int)(buffer.Length > _remain ? _remain : buffer.Length);
            _stream.Position = _start + _curPos;
            int read = _stream.Read(buffer.Slice(0, toSlice));
            _curPos += read;

            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
        {
            if (_remain == 0) return 0;

            int toSlice = (int)(buffer.Length > _remain ? _remain : buffer.Length);
            _stream.Position = _start + _curPos;
            int read = await _stream.ReadAsync(buffer.Slice(0, toSlice), token);
            _curPos += read;

            return read;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remain == 0) return 0;

            int toRead = (int)(_remain < count ? _remain : count);
            _stream.Position = _start + _curPos;
            int read = _stream.Read(buffer, offset, toRead);
            _curPos += read;
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            if (_remain == 0) return 0;

            int toRead = (int)(_remain < count ? _remain : count);
            _curPos += toRead;
            _stream.Position = _start + _curPos;

            return await _stream.ReadAsync(buffer, offset, toRead, token);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_remain == 0) return;

            int toSlice = (int)(buffer.Length > _remain ? _remain : buffer.Length);
            _curPos += toSlice;

            _stream.Write(buffer.Slice(0, toSlice));
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
        {
            if (_remain == 0) return;

            int toSlice = (int)(buffer.Length > _remain ? _remain : buffer.Length);
            _curPos += toSlice;

            await _stream.WriteAsync(buffer.Slice(0, toSlice), token);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            int toRead = (int)(_remain < count ? _remain : count);
            int toOffset = offset > _remain ? 0 : offset;
            _stream.Position += toOffset;
            _curPos += toOffset + toRead;

            _stream.Write(buffer, offset, toRead);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            int toRead = (int)(_remain < count ? _remain : count);
            int toOffset = offset > _remain ? 0 : offset;
            _stream.Position += toOffset;
            _curPos += toOffset + toRead;

            await _stream.WriteAsync(buffer, offset, toRead, token);
        }

        public override void CopyTo(Stream destination, int bufferSize)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead
        {
            get { return _stream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _stream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _stream.CanWrite; }
        }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override long Length
        {
            get { return _size; }
        }

        public override long Position
        {
            get
            {
                return _curPos;
            }
            set
            {
                if (value > _size)
                {
                    throw new IndexOutOfRangeException();
                }

                _curPos = value;
                _stream.Position = _curPos + _start;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    {
                        if (offset > _size)
                        {
                            throw new ArgumentOutOfRangeException();
                        }
                        return _stream.Seek(offset + _start, SeekOrigin.Begin) - _start;
                    }
                case SeekOrigin.Current:
                    {
                        long pos = _stream.Position - _start;
                        if (pos + offset > _size)
                        {
                            throw new ArgumentOutOfRangeException();
                        }
                        return _stream.Seek(offset, SeekOrigin.Current) - _start;
                    }
                default:
                    {
                        _stream.Position = _end;
                        _stream.Position -= offset;

                        return Position;
                    }
            }
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) base.Dispose(disposing);
            if (_isDisposing) _stream.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}