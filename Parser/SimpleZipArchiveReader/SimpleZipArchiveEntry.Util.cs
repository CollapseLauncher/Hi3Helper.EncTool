using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable UnusedMember.Global

#nullable enable
namespace Hi3Helper.EncTool.Parser.SimpleZipArchiveReader;

// Sources:
// https://github.com/dotnet/runtime/blob/main/src/libraries/System.IO.Compression/src/System/IO/Compression/ZipBlocks.FieldLengths.cs
// https://github.com/dotnet/runtime/blob/main/src/libraries/System.IO.Compression/src/System/IO/Compression/ZipBlocks.FieldLocations.cs
// https://github.com/dotnet/runtime/blob/main/src/libraries/System.IO.Compression/src/System/IO/Compression/ZipBlocks.cs#L218
// https://github.com/dotnet/runtime/blob/main/src/libraries/System.IO.Compression/src/System/IO/Compression/ZipHelper.cs#L36
public partial class SimpleZipArchiveEntry
{
    [Flags]
    public enum BitFlagValues : ushort
    {
        IsEncrypted               = 0x1,
        DataDescriptor            = 0x8,
        UnicodeFileNameAndComment = 0x800
    }

    internal static class Zip64ExtraFieldLengths
    {
        public const int UncompressedSize  = sizeof(long);
        public const int CompressedSize    = sizeof(long);
        public const int LocalHeaderOffset = sizeof(long);
    }

    internal static class FieldLengths
    {
        // Must match the signature constant bytes length, but should stay a const int or sometimes
        // static initialization of FieldLengths and NullReferenceException occurs.
        public const int Signature = 4;
        public const int VersionMadeBySpecification = sizeof(byte);
        public const int VersionMadeByCompatibility = sizeof(byte);
        public const int VersionNeededToExtract = sizeof(ushort);
        public const int GeneralPurposeBitFlags = sizeof(ushort);
        public const int CompressionMethod = sizeof(ushort);
        public const int LastModified = sizeof(ushort) + sizeof(ushort);
        public const int Crc32 = sizeof(uint);
        public const int CompressedSize = sizeof(uint);
        public const int UncompressedSize = sizeof(uint);
        public const int FilenameLength = sizeof(ushort);
        public const int ExtraFieldLength = sizeof(ushort);
        public const int FileCommentLength = sizeof(ushort);
        public const int DiskNumberStart = sizeof(ushort);
        public const int InternalFileAttributes = sizeof(ushort);
        public const int ExternalFileAttributes = sizeof(uint);
        public const int RelativeOffsetOfLocalHeader = sizeof(uint);
    }

    internal static class FieldLocations
    {
        public const int Signature = 0;
        public const int VersionMadeBySpecification = Signature + FieldLengths.Signature;
        public const int VersionMadeByCompatibility = VersionMadeBySpecification + FieldLengths.VersionMadeBySpecification;
        public const int VersionNeededToExtract = VersionMadeByCompatibility + FieldLengths.VersionMadeByCompatibility;
        public const int GeneralPurposeBitFlags = VersionNeededToExtract + FieldLengths.VersionNeededToExtract;
        public const int CompressionMethod = GeneralPurposeBitFlags + FieldLengths.GeneralPurposeBitFlags;
        public const int LastModified = CompressionMethod + FieldLengths.CompressionMethod;
        public const int Crc32 = LastModified + FieldLengths.LastModified;
        public const int CompressedSize = Crc32 + FieldLengths.Crc32;
        public const int UncompressedSize = CompressedSize + FieldLengths.CompressedSize;
        public const int FilenameLength = UncompressedSize + FieldLengths.UncompressedSize;
        public const int ExtraFieldLength = FilenameLength + FieldLengths.FilenameLength;
        public const int FileCommentLength = ExtraFieldLength + FieldLengths.ExtraFieldLength;
        public const int DiskNumberStart = FileCommentLength + FieldLengths.FileCommentLength;
        public const int InternalFileAttributes = DiskNumberStart + FieldLengths.DiskNumberStart;
        public const int ExternalFileAttributes = InternalFileAttributes + FieldLengths.InternalFileAttributes;
        public const int RelativeOffsetOfLocalHeader = ExternalFileAttributes + FieldLengths.ExternalFileAttributes;
        public const int DynamicData = RelativeOffsetOfLocalHeader + FieldLengths.RelativeOffsetOfLocalHeader;
    }

    private static bool TryReadZip64SizeFromExtraField(
        ReadOnlySpan<byte> dataTrailing,
        ref long uncompressedSize,
        ref long compressedSize,
        ref long relativeOffsetOfLocalHeader)
    {
        const ushort tagConstant = 1;
        const int tagSizeFieldLen = sizeof(ushort) * 2;

    TryReadAnother:
        if (dataTrailing.Length < tagSizeFieldLen)
        {
            return false;
        }
        ushort tag = MemoryMarshal.Read<ushort>(dataTrailing);
        ushort size = MemoryMarshal.Read<ushort>(dataTrailing[sizeof(ushort)..]);
        ReadOnlySpan<byte> data = dataTrailing.Slice(tagSizeFieldLen, size);

        if (tag != tagConstant)
        {
            dataTrailing = dataTrailing[(data.Length + tagSizeFieldLen)..];
            goto TryReadAnother;
        }

        switch (data.Length)
        {
            // The spec section 4.5.3:
            //      The order of the fields in the zip64 extended
            //      information record is fixed, but the fields MUST
            //      only appear if the corresponding Local or Central
            //      directory record field is set to 0xFFFF or 0xFFFFFFFF.
            // However, tools commonly write the fields anyway; the prevailing convention
            // is to respect the size, but only actually use the values if their 32 bit
            // values were all 0xFF.
            case < Zip64ExtraFieldLengths.UncompressedSize:
                return true;
            // Advancing the stream (by reading from it) is possible only when:
            // 1. There is an explicit ask to do that (valid files, corresponding boolean flag(s) set to true).
            // 2. When the size indicates that all the information is available ("slightly invalid files").
            case >= Zip64ExtraFieldLengths.UncompressedSize:
                uncompressedSize = MemoryMarshal.Read<long>(data);
                data = data[Zip64ExtraFieldLengths.UncompressedSize..];
                break;
        }

        if (data.Length >= Zip64ExtraFieldLengths.CompressedSize)
        {
            compressedSize = MemoryMarshal.Read<long>(data);
            data = data[Zip64ExtraFieldLengths.CompressedSize..];
        }

        if (data.Length >= Zip64ExtraFieldLengths.LocalHeaderOffset)
        {
            relativeOffsetOfLocalHeader = MemoryMarshal.Read<long>(data);
        }

        return true;
    }

    internal static DateTime DosTimeToDateTime(uint dateTime)
    {
        const int validZipDateYearMin = 1980;

        if (dateTime == 0)
        {
            goto ReturnInvalidDateIndicator;
        }

        // DosTime format 32 bits
        // Year: 7 bits, 0 is ValidZipDate_YearMin, unsigned (ValidZipDate_YearMin = 1980)
        // Month: 4 bits
        // Day: 5 bits
        // Hour: 5
        // Minute: 6 bits
        // Second: 5 bits

        // do the bit shift as unsigned because the fields are unsigned, but
        // we can safely convert to int, because they won't be too big
        int year = (int)(validZipDateYearMin + (dateTime >> 25));
        int month = (int)((dateTime >> 21) & 0xF);
        int day = (int)((dateTime >> 16) & 0x1F);
        int hour = (int)((dateTime >> 11) & 0x1F);
        int minute = (int)((dateTime >> 5) & 0x3F);
        int second = (int)((dateTime & 0x001F) * 2); // only 5 bits for second, so we only have a granularity of 2 sec.

        try
        {
            return new DateTime(year, month, day, hour, minute, second, 0);
        }
        catch (ArgumentOutOfRangeException)
        {
        }
        catch (ArgumentException)
        {
        }

        ReturnInvalidDateIndicator:
        return new DateTime(1980, 1, 1, 0, 0, 0);
    }

    internal static ReadOnlySpan<byte> CreateFromBlockSpan(
        ReadOnlySpan<byte> currentBlockSpan,
        out SimpleZipArchiveEntry entry)
    {
        Unsafe.SkipInit(out entry);

        uint signature = BinaryPrimitives.ReadUInt32LittleEndian(currentBlockSpan);

        if (signature != ZipCentralDirectoryMagic)
            throw new InvalidOperationException("Invalid Central Directory signature.");

        BitFlagValues flags = MemoryMarshal.Read<BitFlagValues>(currentBlockSpan[FieldLocations.GeneralPurposeBitFlags..]);

        ushort compressionType = MemoryMarshal.Read<ushort>(currentBlockSpan[FieldLocations.CompressionMethod..]);
        uint lastModified = MemoryMarshal.Read<uint>(currentBlockSpan[FieldLocations.LastModified..]);
        uint crc32 = MemoryMarshal.Read<uint>(currentBlockSpan[FieldLocations.Crc32..]);
        ushort fileNameLen = MemoryMarshal.Read<ushort>(currentBlockSpan[FieldLocations.FilenameLength..]);
        ushort extraFieldLen = MemoryMarshal.Read<ushort>(currentBlockSpan[FieldLocations.ExtraFieldLength..]);
        ushort fileCommentLen = MemoryMarshal.Read<ushort>(currentBlockSpan[FieldLocations.FileCommentLength..]);

        if (flags.HasFlag(BitFlagValues.IsEncrypted))
        {
            throw new NotSupportedException("Encrypted archive is currently not supported.");
        }

        if (compressionType is not (0 or 8 or 9))
        {
            throw new NotSupportedException("Compression is not supported. It must be either Store, Deflate or Deflate64");
        }

        long compressedSize = MemoryMarshal.Read<uint>(currentBlockSpan[FieldLocations.CompressedSize..]);
        long uncompressedSize = MemoryMarshal.Read<uint>(currentBlockSpan[FieldLocations.UncompressedSize..]);

        long relativeOffsetOfLocalHeader = MemoryMarshal.Read<uint>(currentBlockSpan[FieldLocations.RelativeOffsetOfLocalHeader..]);

        ReadOnlySpan<byte> dynamicRecord = currentBlockSpan[FieldLocations.DynamicData..];

        ReadOnlySpan<byte> fileNameSpan = dynamicRecord[..fileNameLen];
        ReadOnlySpan<byte> extraFieldSpan = dynamicRecord.Slice(fileNameLen, extraFieldLen);
        ReadOnlySpan<byte> fileCommentSpan = dynamicRecord.Slice(fileNameLen + extraFieldLen, fileCommentLen);

        // Parse filename and comment
        string? fileComment = null;
        if (fileCommentLen > 0)
        {
            fileComment = Encoding.UTF8.GetString(fileCommentSpan);
        }

        _ = TryReadZip64SizeFromExtraField(extraFieldSpan,
                                           ref uncompressedSize,
                                           ref compressedSize,
                                           ref relativeOffsetOfLocalHeader);

        string fileName = Encoding.UTF8.GetString(fileNameSpan);
        entry = new SimpleZipArchiveEntry
        {
            Comment = fileComment,
            Filename = fileName,
            Crc32 = crc32,
            Flags = flags,
            IsDeflate64 = compressedSize == Zip64Mask || uncompressedSize == Zip64Mask,
            LastModified = new DateTimeOffset(DosTimeToDateTime(lastModified)),
            LocalBlockOffsetFromStream = relativeOffsetOfLocalHeader,
            Size = uncompressedSize,
            SizeCompressed = compressedSize,
            IsDeflate = compressionType is 8 or 9
        };

        int endOfBlock = FieldLocations.DynamicData + fileNameLen + extraFieldLen + fileCommentLen;
        return currentBlockSpan[endOfBlock..];
    }

    private sealed class SequentialReadStream(Stream stream, long size) : Stream
    {
        private readonly long _size = size;
        private long _remainedToRead = size;

        public override int Read(Span<byte> buffer)
        {
            if (_remainedToRead == 0)
            {
                return 0;
            }

            int toRead = (int)Math.Min(_remainedToRead, buffer.Length);
            int read = stream.Read(buffer[..toRead]);

            _remainedToRead -= read;

            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            if (_remainedToRead == 0)
            {
                return 0;
            }

            int toRead = (int)Math.Min(_remainedToRead, buffer.Length);
            int read = await stream.ReadAsync(buffer[..toRead], token);

            _remainedToRead -= read;

            return read;
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) =>
            await ReadAsync(buffer.AsMemory(offset, count), token);

        public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
        {
            return ValueTask.FromException(new NotSupportedException());
        }

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            return Task.FromException(new NotSupportedException());
        }

        public override bool CanRead => stream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override void Flush() => stream.Flush();

        public override long Length
        {
            get => _size;
        }

        public override long Position
        {
            get => _size - _remainedToRead;
            set => throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            stream.Dispose();
        }

        public override ValueTask DisposeAsync() => stream.DisposeAsync();
    }
}
