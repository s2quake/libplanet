﻿using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.Converters;

public sealed class TimeSpanJsonConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => TimeSpan.FromTicks(reader.GetInt64());

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Ticks);
}
