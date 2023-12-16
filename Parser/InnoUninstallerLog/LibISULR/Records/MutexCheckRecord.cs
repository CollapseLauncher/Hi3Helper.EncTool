using System;
using System.Text;

namespace LibISULR.Records
{
    public class MutexCheckRecord : BaseRecord
    {
        public MutexCheckRecord(int flags, byte[] data)
            : base(flags)
        {
            MutexName = new BufferTools(data).ReadString();
        }

        public override int UpdateContent(Span<byte> buffer)
        {
            BufferTools writter = new BufferTools(buffer);
            int offset = writter.WriteString(buffer, Encoding.Unicode, MutexName);
            buffer[offset++] = 0xFF;
            return offset;
        }

        public string MutexName { get; }

        public override RecordType Type
        {
            get { return RecordType.MutexCheck; }
        }

        public override string Description
        {
            get { return $"Mutex Name: {MutexName}"; }
        }
    }
}
