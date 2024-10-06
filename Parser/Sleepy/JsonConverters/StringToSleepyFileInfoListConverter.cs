using Hi3Helper.EncTool.Parser.Sleepy.Responses;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hi3Helper.EncTool.Parser.Sleepy.JsonConverters
{
    internal class StringToSleepyFileInfoListConverter : JsonConverter<List<SleepyFileInfo>>
    {
        public override List<SleepyFileInfo> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(reader.ValueSpan.ReturnUnescapedData().AsSpan().StripTabsAndNewlinesUtf8(), SleepyContext.Default.ListSleepyFileInfo);
        }

        public override void Write(Utf8JsonWriter writer, List<SleepyFileInfo> value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
