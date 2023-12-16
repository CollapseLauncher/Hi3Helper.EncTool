using LibISULR.Flags;
using System;
using System.Collections.Generic;
using System.Text;

namespace LibISULR.Records
{
    public class DeleteDirOrFilesRecord : BaseRecord<DeleteDirOrFilesFlags>
    {
        public DeleteDirOrFilesRecord(int flags, byte[] data)
          : base(flags)
        {
            Paths = new BufferTools(data).GetStringList();
        }

        public override int UpdateContent(Span<byte> buffer)
        {
            BufferTools writter = new BufferTools(buffer);
            int offset = writter.WriteStringList(buffer, Encoding.Unicode, Paths);
            return offset;
        }

        public List<string> Paths { get; }

        public override string Description
        {
            get { return $"{string.Join(", ", Paths)}; {Flags}"; }
        }

        public override RecordType Type
        {
            get { return RecordType.DeleteDirOrFiles; }
        }
    }
}
