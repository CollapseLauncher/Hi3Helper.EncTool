using System;
using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hi3Helper.EncTool.Parser.Sleepy.JsonConverters
{
    public class NumberStringToXxh64HashBytesConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return GetBytesFromNumberString(reader.ValueSpan);
                case JsonTokenType.Number:
                    if (!reader.TryGetUInt64(out ulong valueAsUlong))
                        throw new InvalidOperationException("Value cannot be parsed to ulong");

                    byte[] bytesFromUlong = new byte[8];
                    if (!BinaryPrimitives.TryWriteUInt64BigEndian(bytesFromUlong, valueAsUlong))
                    {
                        throw new InvalidOperationException("Cannot write ulong value to bytes");
                    }

                    return bytesFromUlong;
                case JsonTokenType.None:
                case JsonTokenType.StartObject:
                case JsonTokenType.EndObject:
                case JsonTokenType.StartArray:
                case JsonTokenType.EndArray:
                case JsonTokenType.PropertyName:
                case JsonTokenType.Comment:
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                default:
                    throw new NotSupportedException($"JsonTokenType.{reader.TokenType} is not supported!");
            }
        }

        private static byte[] GetBytesFromNumberString(ReadOnlySpan<byte> valueSpan)
        {
            if (!ulong.TryParse(valueSpan, out ulong result))
            {
                throw new InvalidOperationException("String is not a valid Number String!");
            }

            byte[] bytes = new byte[8];
            if (!BinaryPrimitives.TryWriteUInt64BigEndian(bytes, result))
            {
                throw new InvalidOperationException("Cannot write ulong value to bytes");
            }

            return bytes;
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
