using LibISULR.Flags;
using System;
using System.Text;

namespace LibISULR.Records
{
    public class RunRecord : BaseRecord<RunFlags>
    {
        private string filename;
        private string args;
        private string workingDir;
        private string runOnceId;
        private string verb;

        public RunRecord(int flags, byte[] data)
          : base(flags)
        {
            BufferTools spliiter = new BufferTools(data);
            filename = spliiter.ReadString();
            args = spliiter.ReadString();
            workingDir = spliiter.ReadString();
            runOnceId = spliiter.ReadString();
            verb = spliiter.ReadString();
        }

        public override int UpdateContent(Span<byte> buffer)
        {
            BufferTools stringWriter = new BufferTools(buffer);
            int offset = stringWriter.WriteString(buffer, Encoding.Unicode, filename);
            offset += stringWriter.WriteString(buffer.Slice(offset), Encoding.Unicode, args);
            offset += stringWriter.WriteString(buffer.Slice(offset), Encoding.Unicode, workingDir);
            offset += stringWriter.WriteString(buffer.Slice(offset), Encoding.Unicode, runOnceId);
            offset += stringWriter.WriteString(buffer.Slice(offset), Encoding.Unicode, verb);
            buffer[offset++] = 0xFF;
            return offset;
        }

        public override RecordType Type
        {
            get { return RecordType.Run; }
        }

        public override string Description
        {
            get { return $"File: \"{filename}\" Args: \"{args}\"; At \"{workingDir}\"; Flags: {Flags}"; }
        }

        public string Filename
        {
            get { return filename; }
        }

        public string Args
        {
            get { return args; }
        }

        public string WorkingDir
        {
            get { return workingDir; }
        }
    }
}
