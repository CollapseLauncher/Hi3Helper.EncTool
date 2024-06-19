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
            { "AssetBundleVersionUpdateUrl", "$/asb/" },
            { "LuaBundleVersionUpdateUrl", "$/lua/" },
            { "LuaPatchVersion", "$NUMREF|LuaBundleVersionUpdateUrl|output" },
            { "DesignDataBundleVersionUpdateUrl", "$/design_data/" },
            { "IFixPatchVersionUpdateUrl", "$/ifix/" },
            { "IFixPatchRevision", "$NUMREF|IFixPatchVersionUpdateUrl|output" },
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
            List<string> values = new List<string>();
            
            while ((parserTag = input.ReadTag()) != 0)
            {
                if ((parserTag & 7) == 4)
                    return; // As per autogenerated code, if the tag is EOF, then return.
                try
                {
                    parseTag(parserTag, ref input, values);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error when trying to parse tag {parserTag}\r\n{ex}");
                }
            }

        #if DEBUG
            Console.WriteLine("SR Static Parser output:");
        #endif
            foreach (KeyValuePair<string, string> kvp in ValuePairs)
            {
                // If the kvp value has $ (Macro sign), which means the data is empty,
                // then try to naively look up the value from the result list.
                if (kvp.Value[0] == '$')
                {
                    if (TryFindNaivePairsByMacro(ValuePairs, kvp, values))
                #if DEBUG
                        Console.WriteLine($"The metadata does not have a correct KVP, a naive approach has found a correct value for field: {kvp.Key}!");
                #else
                        return;
                #endif
                    else
                        throw new NotSupportedException($"The KVP of field: {kvp.Key} has no valid values!");
                }

            #if DEBUG
                Console.WriteLine($"Key: {kvp.Key} - Val: {ValuePairs[kvp.Key]}");
            #endif
            }
        }

        bool TryFindNaivePairsByMacro(Dictionary<string, string> dict,
            KeyValuePair<string, string> kvp, List<string> results)
        {
            const string NUMREF_MACRO = "NUMREF";
            ReadOnlySpan<char> macroSpan = kvp.Value.TrimStart('$');
            return macroSpan.StartsWith(NUMREF_MACRO) ?
                TryAssignKvpFromMacroNumref(macroSpan.TrimStart(NUMREF_MACRO), kvp, dict) :
                TryAssignKvpFromMacroLookup(dict, macroSpan, kvp, results);
        }

        bool TryAssignKvpFromMacroNumref(ReadOnlySpan<char> key, KeyValuePair<string, string> kvp, Dictionary<string, string> dict)
        {
            const char SEPARATOR = '_';
            const char HEAD = '|';
            Span<Range> splitRanges = stackalloc Range[3];

            if (!dict.ContainsKey(kvp.Key))
                return false;

            key = key.TrimStart(HEAD);
            int keySplitLen = key.Split(splitRanges, HEAD, StringSplitOptions.RemoveEmptyEntries);
            if (keySplitLen != 2)
                return false;

            string keyString = key[splitRanges[0]].ToString();
            string firstWord = key[splitRanges[1]].ToString();
            if (!dict.ContainsKey(keyString))
                return false;

            ReadOnlySpan<char> resStr = dict[keyString];
            int lastIndexOf = resStr.LastIndexOf(firstWord);
            if (lastIndexOf < 0)
                return false;

            ReadOnlySpan<char> lastPath = resStr.Slice(lastIndexOf);
            int splitLen = lastPath.Split(splitRanges, SEPARATOR, StringSplitOptions.RemoveEmptyEntries);
            if (splitLen != 3)
                return false;

            if (!int.TryParse(lastPath[splitRanges[1]], out int refResult))
                return false;

            dict[kvp.Key] = $"{refResult}";
            return true;
        }

        bool TryAssignKvpFromMacroLookup(Dictionary<string, string> dict, ReadOnlySpan<char> key,
            KeyValuePair<string, string> kvp, List<string> results)
        {
            int listLen = results.Count;
            for (int i = 0; i < listLen; i++)
            {
                ReadOnlySpan<char> resStr = results[i];
                int lastIndexOf = resStr.LastIndexOf(key);
                if (lastIndexOf < 0)
                    continue;

                if (!dict.ContainsKey(kvp.Key))
                    continue;

                dict[kvp.Key] = results[i];
                return true;
            }

            return false;
        }

        void parseTag(uint tag, ref ParseContext input, List<string> dataResults)
        {
            uint     fieldNumber = tag >> 3;
            WIRETYPE wireType    = (WIRETYPE)(tag & 0x7);
            uint     revIdTag    = tag >> 3 | (int)WIRETYPE.VARINT;

            string valueAsString = null;
            KeyValuePair<string, uint> protoPairs   = gatewayPairs.FirstOrDefault(x => x.Value == revIdTag);

        #if DEBUG
            Console.WriteLine($"Reading tag {tag} - Field Num: {fieldNumber}");
            if (protoPairs.Value != 0) Console.WriteLine($"Reading key {protoPairs.Key} as field num {protoPairs.Value}");
        #endif

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
#               if DEBUG
                    Console.WriteLine("Got unknown field!");
#               endif
                    _unknownFields = UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
                    return;
            }
            if (!string.IsNullOrEmpty(valueAsString))
                dataResults.Add(valueAsString);

            if (!string.IsNullOrEmpty(protoPairs.Key))
            {
                ValuePairs[protoPairs.Key] = valueAsString;

            #if DEBUG
                Console.WriteLine($"[{wireType.ToString()}]Got : {protoPairs.Key} - {valueAsString}");
            #endif
            }
        }
    }
}
