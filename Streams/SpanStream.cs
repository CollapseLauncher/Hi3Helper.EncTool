using System;
using System.IO;
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable PartialTypeWithSinglePart

namespace Hi3Helper.EncTool
{
    public sealed partial class SpanStream(Memory<byte> @base) : Stream
    {
        private          long         _position;

        ~SpanStream() => Dispose();

        public override int Read(Span<byte> buffer)
        {
            buffer = @base.Slice((int)_position, buffer.Length).Span;
            _position += buffer.Length;
            return buffer.Length;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            _position += offset;
            int remain = @base.Length - (int)_position;
            int toRead = remain < count ? remain : count;

            _ = @base.Slice((int)_position, toRead).ToArray(); // buffer

            return toRead;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            @base.Span.Clear();
        }

        public override long Length
        {
            get { return @base.Length; }
        }

        public override long Position
        {
            get
            {
                return _position;
            }
            set
            {
                _position = value;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _position = offset;
                    break;
                case SeekOrigin.Current:
                    _position += offset;
                    break;
                case SeekOrigin.End:
                    _position = @base.Length - offset;
                    break;
            }
            return _position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            @base.Span.Clear();
        }
    }
}
