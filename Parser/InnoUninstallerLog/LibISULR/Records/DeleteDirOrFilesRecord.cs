using LibISULR.Flags;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LibISULR.Records
{
    public class DeleteDirOrFilesRecord : BaseRecord<DeleteDirOrFilesFlags>
    {
        public DeleteDirOrFilesRecord(int flags, byte[] data)
          : base(flags)
        {
            Paths = new StringSplitter(data).GetStringList();
        }

        public override int UpdateContent(Span<byte> buffer)
        {
            StringSplitter writter = new StringSplitter(buffer);
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
