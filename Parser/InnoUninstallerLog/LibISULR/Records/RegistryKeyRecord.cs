using LibISULR.Flags;

using Microsoft.Win32;
using System;
using System.Text;

namespace LibISULR.Records
{
    public class RegistryKeyRecord : BaseRecord
    {
        private RecordType type;
        private string path;
        private RegistryHive hive;
        private RegistryView view;

        public RegistryKeyRecord(RecordType type, int flags, byte[] data)
            :base(flags)
        {
            this.type = type;

            StringSplitter spliiter = new StringSplitter(data);
            Init(ref spliiter);

            RegFlags f = (RegFlags)flags;
            view = (f & RegFlags.Reg_64BitKey) != 0 ? RegistryView.Registry64 : RegistryView.Registry32;
            hive = (RegistryHive)(f & RegFlags.Reg_KeyHandleMask);
        }

        public override int UpdateContent(Span<byte> buffer)
        {
            int offset = new StringSplitter(buffer).WriteString(buffer, Encoding.Unicode, path);
            buffer[offset++] = 0xFF;
            return offset;
        }

        protected virtual void Init(ref StringSplitter splitter)
        {
            path = splitter.ReadString();
        }

        public string Path
        {
            get { return path; }
        }

        public RegistryHive Hive
        {
            get { return hive; }
        }

        public RegistryView View
        {
            get { return view; }
        }

        public override RecordType Type
        {
            get { return type; }
        }

        public override string Description
        {
            get { return $"View: {view}; Hive: {hive}; Path: {path}"; }
        }
    }
}
