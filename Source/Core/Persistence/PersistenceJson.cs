using System.Text.Json;
using System.Text.Json.Serialization;
using Mythos.Framework.Characters;
using Mythos.Framework.Entities;
using Mythos.Framework.Npcs;
using Mythos.Framework.Regions;
using Mythos.Framework.Time;

namespace Mythos.Framework.Persistence;

internal static class PersistenceJson
{
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
        options.Converters.Add(new EntityIdConverter());
        options.Converters.Add(new StringValueConverter<EntityCategory>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<EntityTag>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<ComponentTypeId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<CalendarId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<PauseReason>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<ScheduleId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<RegionCategory>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<CharacterIdentity>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<CharacterStatusId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<LifeStageId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<NpcPurposeId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<NpcScheduleId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<NpcScheduleStateId>(v => new(v), v => v.Value));
        options.Converters.Add(new LongValueConverter<WorldTimestamp>(v => new(v), v => v.Value));
        options.Converters.Add(new LongValueConverter<WorldDuration>(v => new(v), v => v.Value));
        options.Converters.Add(new TimeScaleConverter());
        return options;
    }

    private sealed class StringValueConverter<T>(Func<string, T> create, Func<T, string> read) : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => create(reader.GetString()!);
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer.WriteStringValue(read(value));
    }

    private sealed class LongValueConverter<T>(Func<long, T> create, Func<T, long> read) : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => create(reader.GetInt64());
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer.WriteNumberValue(read(value));
    }

    private sealed class EntityIdConverter : JsonConverter<EntityId>
    {
        public override EntityId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            EntityId.TryParse(reader.GetString(), out var value) ? value : throw new JsonException("Entity ID is invalid.");
        public override void Write(Utf8JsonWriter writer, EntityId value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
    }

    private sealed class TimeScaleConverter : JsonConverter<TimeScale>
    {
        public override TimeScale Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            return new TimeScale(root.GetProperty("numerator").GetInt64(), root.GetProperty("denominator").GetInt64());
        }
        public override void Write(Utf8JsonWriter writer, TimeScale value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("numerator", value.Numerator);
            writer.WriteNumber("denominator", value.Denominator);
            writer.WriteEndObject();
        }
    }
}
