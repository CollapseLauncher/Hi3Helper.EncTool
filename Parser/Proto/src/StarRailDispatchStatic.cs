using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace Hi3Helper.EncTool.Proto.StarRail
{
    public sealed partial class StarRailDispatchStatic : IMessage<StarRailDispatchStatic>, IBufferMessage
    {
        public static MessageParser<StarRailDispatchStatic> Parser { get; } = new MessageParser<StarRailDispatchStatic>(() => new StarRailDispatchStatic());
        private UnknownFieldSet _unknownFields;

        MessageDescriptor IMessage.Descriptor => null;

        public StarRailDispatchStatic() { }

        public StarRailDispatchStatic(StarRailDispatchStatic other) : this()
        {
            RegionList = other.RegionList.Clone();
            _unknownFields = UnknownFieldSet.Clone(other._unknownFields);
        }

        public StarRailDispatchStatic Clone() => new StarRailDispatchStatic(this);

        private FieldCodec<RegionInfoStatic> _repeated_regionList_codec { get; } = GetFieldCodec();

        private static FieldCodec<RegionInfoStatic> GetFieldCodec()
        {
            StarRailDispatchGatewayProps.EnsureProtoIdInitialized();
            KeyValuePair<string, uint> key = StarRailDispatchGatewayProps.ProtoIDs.dispatchInfo.FirstOrDefault();
            if (string.IsNullOrEmpty(key.Key) || key.Value == 0)
                throw new KeyNotFoundException($"The region list key isn't found! Please report this issue to our GitHub repository!");

            uint tag = (key.Value << 3) + 4;
            return FieldCodec.ForMessage(tag, RegionInfoStatic.Parser);
        }

        public RepeatedField<RegionInfoStatic> RegionList { get; set; } = new RepeatedField<RegionInfoStatic>();

        public override bool Equals(object other) => Equals(other as StarRailDispatchStatic);

        public bool Equals(StarRailDispatchStatic other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(other, this)) return true;

            if (!RegionList.Equals(other.RegionList)) return false;
            return Equals(_unknownFields, other._unknownFields);
        }

        public override int GetHashCode()
        {
            int hash = 1;
            hash ^= RegionList.GetHashCode();
            if (_unknownFields != null) hash ^= _unknownFields.GetHashCode();
            return hash;
        }

        public override string ToString() => JsonFormatter.ToDiagnosticString(this);

        public void WriteTo(CodedOutputStream output) => output.WriteRawMessage(this);

        void IBufferMessage.InternalWriteTo(ref WriteContext output)
        {
            RegionList.WriteTo(ref output, _repeated_regionList_codec);
            if (_unknownFields != null) _unknownFields.WriteTo(ref output);
        }

        public int CalculateSize()
        {
            int size = 0;
            size += RegionList.CalculateSize(_repeated_regionList_codec);
            if (_unknownFields != null) size += _unknownFields.CalculateSize();
            return size;
        }

        public void MergeFrom(StarRailDispatchStatic other)
        {
            if (other == null) return;
            RegionList.Add(other.RegionList);
            _unknownFields = UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
        }

        public void MergeFrom(CodedInputStream input) => input.ReadRawMessage(this);

        void IBufferMessage.InternalMergeFrom(ref ParseContext input)
        {
            StarRailDispatchGatewayProps.EnsureProtoIdInitialized();
            uint tag;
            while ((tag = input.ReadTag()) != 0)
            {
                // Get the raw value of the protoId and find the id from the dictionary
                uint revIdTag = tag >> 3 | (int)WIRETYPE.VARINT;
                KeyValuePair<string, uint> protoPairs = StarRailDispatchGatewayProps.ProtoIDs.dispatchInfo.Where(x => x.Value == revIdTag).FirstOrDefault();

                // If the pair is found, then set the regionList
                if (!string.IsNullOrEmpty(protoPairs.Key) && protoPairs.Value != 0)
                {
                    RegionList.AddEntriesFrom(ref input, _repeated_regionList_codec);
                    continue;
                }

                // Otherwise, read unknown field
                _unknownFields = UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            }
        }
    }

    public sealed partial class RegionInfoStatic : IMessage<RegionInfoStatic>, IBufferMessage
    {
        private UnknownFieldSet _unknownFields;
        public static MessageParser<RegionInfoStatic> Parser { get; } = new MessageParser<RegionInfoStatic>(() => new RegionInfoStatic());

        public Dictionary<string, string> ValuePairs = new Dictionary<string, string>
        {
            { "name", "" },
            { "title", "" },
            { "dispatch_url", "" },
            { "env_type", "" },
            { "display_name", "" },
            { "msg", "" },
        };

        MessageDescriptor IMessage.Descriptor => null;

        public RegionInfoStatic()
        {
            OnConstruction();
        }

        partial void OnConstruction();

        public RegionInfoStatic(RegionInfoStatic other) : this()
        {
            ValuePairs = new Dictionary<string, string>(other.ValuePairs);
            _unknownFields = UnknownFieldSet.Clone(other._unknownFields);
        }

        public RegionInfoStatic Clone() => new RegionInfoStatic(this);

        public override bool Equals(object other) => Equals(other as RegionInfoStatic);

        public bool Equals(RegionInfoStatic other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(other, this)) return true;

            bool valuePairsEquality = ValuePairs.All(x =>
            {
                if (!other.ValuePairs.ContainsKey(x.Key)) return false;
                return other.ValuePairs[x.Key] == x.Value;
            });
            return Equals(_unknownFields, other._unknownFields) && valuePairsEquality;
        }

        public override int GetHashCode()
        {
            int hash = 1;
            foreach (KeyValuePair<string, string> keyValuePair in ValuePairs)
            {
                if (keyValuePair.Value.Length != 0) hash ^= keyValuePair.Value.GetHashCode();
            }
            if (_unknownFields != null) hash ^= _unknownFields.GetHashCode();
            return hash;
        }

        public override string ToString() => JsonFormatter.ToDiagnosticString(this);

        public void WriteTo(CodedOutputStream output) => output.WriteRawMessage(this);

        void IBufferMessage.InternalWriteTo(ref WriteContext output) { }

        public int CalculateSize()
        {
            int size = 0;
            foreach (KeyValuePair<string, string> keyValuePair in ValuePairs)
            {
                if (keyValuePair.Value.Length == 0) continue;
                _ = StarRailDispatchGatewayProps.GetIdsInfo(keyValuePair.Key, StarRailDispatchGatewayProps.ProtoIDs.dispatchInfo, out uint inc);
                size += (int)inc + CodedOutputStream.ComputeStringSize(keyValuePair.Value);
            }

            if (_unknownFields != null) size += _unknownFields.CalculateSize();
            return size;
        }

        public void MergeFrom(RegionInfoStatic other)
        {
            if (other == null) return;

            foreach (KeyValuePair<string, string> keyValuePair in other.ValuePairs)
            {
                if (!ValuePairs.ContainsKey(keyValuePair.Key)) continue;
                if (keyValuePair.Value.Length != 0) ValuePairs[keyValuePair.Key] = keyValuePair.Value;
            }

            _unknownFields = UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
        }

        public void MergeFrom(CodedInputStream input) => input.ReadRawMessage(this);

        void IBufferMessage.InternalMergeFrom(ref ParseContext input)
        {
            StarRailDispatchGatewayProps.EnsureProtoIdInitialized();
            uint tag;
            while ((tag = input.ReadTag()) != 0)
            {
                // Get the raw value of the protoId and find the id from the dictionary
                uint revIdTag = tag >> 3 | (int)WIRETYPE.VARINT;
                KeyValuePair<string, uint> protoPairs = StarRailDispatchGatewayProps.ProtoIDs.dispatchInfo.Where(x => x.Value == revIdTag).FirstOrDefault();

                // If the pair is found, then set the ValuePairs
                if (!string.IsNullOrEmpty(protoPairs.Key) && protoPairs.Value != 0)
                {
                    ValuePairs[protoPairs.Key] = input.ReadString();
                    continue;
                }

                // Otherwise, read unknown field
                _unknownFields = UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            }
        }
    }
}
