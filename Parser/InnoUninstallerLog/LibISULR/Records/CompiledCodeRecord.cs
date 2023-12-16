using System;
using System.Text;

namespace LibISULR.Records
{
    public class CompiledCodeRecord : BaseRecord
    {
        public CompiledCodeRecord(int flags, byte[] data)
            : base(flags)
        {
            StringSplitter splitter = new StringSplitter(data);
            Code = splitter.ReadBytes();
            LeadBytes = splitter.ReadBytes();
            ExpandedApp = splitter.ReadString();
            ExpandedGroup = splitter.ReadString();
            WizardGroup = splitter.ReadString();
            Language = splitter.ReadString();
            LanguageData = splitter.GetStringArray();
        }

        public override int UpdateContent(Span<byte> buffer)
        {
            StringSplitter writter = new StringSplitter(buffer);
            int offset = writter.WriteBytes(buffer, Code);
            offset += writter.WriteBytes(buffer.Slice(offset), LeadBytes);
            offset += writter.WriteString(buffer.Slice(offset), Encoding.Unicode, ExpandedApp);
            offset += writter.WriteString(buffer.Slice(offset), Encoding.Unicode, ExpandedGroup);
            offset += writter.WriteString(buffer.Slice(offset), Encoding.Unicode, WizardGroup);
            offset += writter.WriteString(buffer.Slice(offset), Encoding.Unicode, Language);
            offset += writter.WriteStringArray(buffer.Slice(offset), Encoding.Unicode, LanguageData);
            return offset;
        }

        public byte[] Code { get; }

        public byte[] LeadBytes { get; }

        public string ExpandedApp { get; }

        public string ExpandedGroup { get; }

        public string WizardGroup { get; }

        public string Language { get; }
        public string[] LanguageData { get; }

        public override RecordType Type
        {
            get { return RecordType.CompiledCode; }
        }

        public override string Description
        {
            get { return $"Code: {Code?.Length}; App: {ExpandedApp}; Group: {ExpandedGroup}; Wizard group: {WizardGroup}; Language: {Language}"; }
        }
    }
}
