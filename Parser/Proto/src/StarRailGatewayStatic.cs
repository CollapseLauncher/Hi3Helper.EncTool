using Google.Protobuf;
using Google.Protobuf.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hi3Helper.EncTool.Proto.StarRail
{
    [JsonSerializable(typeof(DispatchGatewayProtoIdsInfo))]
    public partial class DispatchGatewayContext : JsonSerializerContext { }

#nullable enable
    public class DispatchGatewayProtoIdsInfo
    {
        public Dictionary<string, uint>? dispatchInfo { get; set; }
        public Dictionary<string, uint>? gatewayInfo { get; set; }
    }
#nullable disable

    [Flags]
    internal enum WIRETYPE
    {
        VARINT = 0b000,
        I64 = 0b001,
        LEN = 0b010,
        I32 = 0b101
    }

    public static class StarRailDispatchGatewayProps
    {
        internal static DispatchGatewayProtoIdsInfo ProtoIDs { get; set; }

        public static void Deserialize(string jsonResponse)
        {
            ProtoIDs = JsonSerializer.Deserialize(jsonResponse, DispatchGatewayContext.Default.DispatchGatewayProtoIdsInfo);
        }

        internal static void EnsureProtoIdInitialized()
        {
            if (ProtoIDs == null)
            {
                throw new NullReferenceException("Proto ID is not initialized! Please report this issue to our GitHub repository!");
            }
        }

        internal static uint GetIdsInfo(string key, Dictionary<string, uint> protoIdDictionary, out uint inc)
        {
            // Get the raw proto id reference
            uint id = protoIdDictionary[key];
            // Add the increment value to 1u, increment that using id divided by 16, then floor it
            inc = 1u + (uint)Math.Floor(id / 16d);
            return id;
        }
    }

    public sealed partial class StarRailGatewayStatic : IMessage<StarRailGatewayStatic>, IBufferMessage
    {
        public static MessageParser<StarRailGatewayStatic> Parser { get; } = new MessageParser<StarRailGatewayStatic>(() => new StarRailGatewayStatic());

        private UnknownFieldSet _unknownFields { get; set; }
        MessageDescriptor IMessage.Descriptor => null;

        public Dictionary<string, string> ValuePairs = new Dictionary<string, string>
        {
            { "AssetBundleVersionUpdateUrl", "" },
            { "LuaBundleVersionUpdateUrl", "" },
            { "LuaPatchVersion", "" },
            { "DesignDataBundleVersionUpdateUrl", "" },
            { "IFixPatchVersionUpdateUrl", "" },
            { "IFixPatchRevision", "" },
        };

        public StarRailGatewayStatic() { }

        public StarRailGatewayStatic(StarRailGatewayStatic other) : this()
        {
            ValuePairs = new Dictionary<string, string>(other.ValuePairs);
            _unknownFields = UnknownFieldSet.Clone(other._unknownFields);
        }

        public StarRailGatewayStatic Clone() => new StarRailGatewayStatic(this);

        public override bool Equals(object other) => Equals(other as StarRailGatewayStatic);

        public bool Equals(StarRailGatewayStatic other)
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

        public void WriteTo(CodedOutputStream output) => output.WriteRawMessage(this);

        void IBufferMessage.InternalWriteTo(ref WriteContext output) { }

        public int CalculateSize()
        {
            int size = 0;
            foreach (KeyValuePair<string, string> keyValuePair in ValuePairs)
            {
                if (keyValuePair.Value.Length == 0) continue;

                // Ensure proto ID information is retrieved correctly
                bool idExists = StarRailDispatchGatewayProps.GetIdsInfo(keyValuePair.Key, StarRailDispatchGatewayProps.ProtoIDs.gatewayInfo, out uint inc) != 0;
                if (!idExists)
                {
                    Console.WriteLine($"Warning: No ID found for key: {keyValuePair.Key}");
                    continue;
                }

                // Compute the size for the current key-value pair
                size += CodedOutputStream.ComputeTagSize((int)inc); // Tag size
                size += CodedOutputStream.ComputeStringSize(keyValuePair.Value); // Value size
            }

            // Include size for unknown fields if any
            if (_unknownFields != null) size += _unknownFields.CalculateSize();
    
            return size;
        }


        public void MergeFrom(StarRailGatewayStatic other)
        {
            if (other == null) return;

            foreach (KeyValuePair<string, string> keyValuePair in other.ValuePairs)
            {
                // Add or update value in ValuePairs if it is not empty
                if (!ValuePairs.ContainsKey(keyValuePair.Key))
                {
                    ValuePairs[keyValuePair.Key] = keyValuePair.Value;
                }
                else if (keyValuePair.Value.Length != 0)
                {
                    ValuePairs[keyValuePair.Key] = keyValuePair.Value;
                }
            }

            // Merge unknown fields
            _unknownFields = UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
        }

        public void MergeFrom(CodedInputStream input) => input.ReadRawMessage(this);

        uint                       parserTag;
        Dictionary<string, uint>   gatewayPairs = StarRailDispatchGatewayProps.ProtoIDs.gatewayInfo;
        
        void IBufferMessage.InternalMergeFrom(ref ParseContext input)
        {
            StarRailDispatchGatewayProps.EnsureProtoIdInitialized();
            
            while ((parserTag = input.ReadTag()) != 0)
            {
                try
                {
                    parseTag(parserTag, ref input);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error when trying to parse tag {parserTag}");
                }
            }
        #if DEBUG
            Console.WriteLine("SR Static Parser output:");
            foreach (KeyValuePair<string, string> kvp in ValuePairs)
            {
                Console.WriteLine($"Key: {kvp.Key} - Val: {kvp.Value}");
            }
        #endif
        }

        void parseTag(uint tag, ref ParseContext input)
        {
            uint     fieldNumber = tag >> 3;
            WIRETYPE wireType    = (WIRETYPE)(tag & 0x7);
            uint     revIdTag    = tag >> 3 | (int)WIRETYPE.VARINT;
            
            KeyValuePair<string, uint> protoPairs   = gatewayPairs.FirstOrDefault(x => x.Value == revIdTag);

        #if DEBUG
            Console.WriteLine($"Reading tag {tag} - Field Num: {fieldNumber}");
        #endif
            if (!string.IsNullOrEmpty(protoPairs.Key))
            {
            #if DEBUG
                Console.WriteLine($"Reading key {protoPairs.Key} as field num {protoPairs.Value}");
            #endif

                string valueAsString;

                switch (wireType)
                {
                    case WIRETYPE.VARINT:
                        // Trying various possible types for VARINT
                        valueAsString = input.ReadInt32().ToString();
                        break;
                    case WIRETYPE.I64:
                        valueAsString = input.ReadFixed64().ToString();
                        break;
                    case WIRETYPE.LEN:
                        valueAsString = input.ReadString();
                        break;
                    case WIRETYPE.I32:
                        valueAsString = input.ReadFixed32().ToString();
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported wire type: {wireType}");
                }

                ValuePairs[protoPairs.Key] = valueAsString;

            #if DEBUG
                Console.WriteLine($"[{wireType.ToString()}]Got : {protoPairs.Key} - {valueAsString}");
            #endif
            }
            else
            {
                #if DEBUG
                    Console.WriteLine("Got unknown field!");
                #endif
                _unknownFields = UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            }
        }
    }
}
