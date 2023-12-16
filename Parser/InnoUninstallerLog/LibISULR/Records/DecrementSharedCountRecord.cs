using LibISULR.Flags;
using System;

namespace LibISULR.Records
{
    public class DecrementSharedCountRecord : BaseRecord<DecrementSharedCountFlags>
    {
        public DecrementSharedCountRecord(int extra, byte[] data)
          : base(extra)
        {
            Path = new StringSplitter(data).ReadString();
        }

        public override int UpdateContent(Span<byte> buffer)
        {
            throw new NotImplementedException();
        }

        public string Path { get; }

        public override RecordType Type
        {
            get { return RecordType.DecrementSharedCount; }
        }

        public override string Description
        {
            get { return $"Path: {Path}"; }
        }
    }
}
