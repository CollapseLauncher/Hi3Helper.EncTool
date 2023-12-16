using System;

namespace LibISULR.Records
{
    public abstract class BaseRecord
    {
        public BaseRecord(int flagsNum)
        {
            this.FlagsNum = flagsNum;
        }
        public int FlagsNum { get; private set; }

        public abstract RecordType Type { get; }

        public abstract string Description { get; }

        public abstract int UpdateContent(Span<byte> buffer);

        public override string ToString()
        {
            return $"Type: {Type}. Desc: {Description}";
        }
    }

    public abstract class BaseRecord<TFlags> : BaseRecord
        where TFlags : Enum 
    {
        protected BaseRecord(int flags)
            : base(flags)
        {
            this.Flags = (TFlags)Enum.ToObject(typeof(TFlags), flags);
        }

        public TFlags Flags { get; private set; }
    }
}
