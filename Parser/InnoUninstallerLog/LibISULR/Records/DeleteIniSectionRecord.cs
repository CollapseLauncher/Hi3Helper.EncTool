using LibISULR.Flags;
using System;

namespace LibISULR.Records
{
    public class DeleteIniSectionRecord : BaseRecord<IniFlags>
    {
        private string filename;
        private string section;

        public DeleteIniSectionRecord(int flags, byte[] data)
          : base(flags)
        {
            StringSplitter splitter = new StringSplitter(data);
            Init(ref splitter);
        }

        protected virtual void Init(ref StringSplitter splitter)
        {
            filename = splitter.ReadString();
            section = splitter.ReadString();
        }

        public override int UpdateContent(Span<byte> buffer)
        {
            throw new NotImplementedException();
        }

        public string Filename
        {
            get { return filename; }
        }

        public string Section
        {
            get { return section; }
        }

        public override RecordType Type
        {
            get { return RecordType.IniDeleteSection; }
        }

        public override string Description
        {
            get { return $"File: \"{filename}\"; Section: \"{section}\"; Flags: {Flags}"; }
        }
    }
}
