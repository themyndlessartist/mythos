using System.Text.Json;
using System.Text.Json.Serialization;
using Mythos.Framework.Characters;
using Mythos.Framework.Entities;
using Mythos.Framework.Information;
using Mythos.Framework.History;
using Mythos.Framework.Npcs;
using Mythos.Framework.Regions;
using Mythos.Framework.Relationships;
using Mythos.Framework.Reputation;
using Mythos.Framework.Properties;
using Mythos.Framework.Organizations;
using Mythos.Framework.Economy;
using Mythos.Framework.DynamicEvents;
using Mythos.Framework.Time;

namespace Mythos.Framework.Persistence;

internal static class PersistenceJson
{
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new OrdinalStringDictionaryConverter());
        options.Converters.Add(new OrdinalIntegerDictionaryConverter());
        options.Converters.Add(new EntityIdConverter());
        options.Converters.Add(new RelationshipIdConverter());
        options.Converters.Add(new InformationIdConverter());
        options.Converters.Add(new FactIdConverter());
        options.Converters.Add(new HistoryEntryIdConverter());
        options.Converters.Add(new ReputationIdConverter());
        options.Converters.Add(new MembershipIdConverter());
        options.Converters.Add(new EconomyAccountIdConverter());
        options.Converters.Add(new EconomyTransferIdConverter());
        options.Converters.Add(new DynamicWorldEventIdConverter());
        options.Converters.Add(new StringValueConverter<EntityCategory>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<EntityTag>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<ComponentTypeId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<CalendarId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<PauseReason>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<ScheduleId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<SimulationLayerId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<RegionCategory>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<CharacterIdentity>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<CharacterStatusId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<LifeStageId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<NpcPurposeId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<NpcScheduleId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<NpcScheduleStateId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<RelationshipKindId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<InformationTypeId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<HistoryTypeId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<ReputationAudienceTypeId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<ReputationDimensionId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<PropertyKindId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<OrganizationKindId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<OrganizationRoleId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<CurrencyId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<DynamicWorldEventTypeId>(v => new(v), v => v.Value));
        options.Converters.Add(new StringValueConverter<DynamicWorldEventOutcomeId>(v => new(v), v => v.Value));
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

    private sealed class RelationshipIdConverter : JsonConverter<RelationshipId>
    {
        public override RelationshipId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            Guid.TryParse(reader.GetString(), out var value) && value != Guid.Empty
                ? new RelationshipId(value) : throw new JsonException("Relationship ID is invalid.");
        public override void Write(Utf8JsonWriter writer, RelationshipId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }

    private sealed class InformationIdConverter : JsonConverter<InformationId>
    {
        public override InformationId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            Guid.TryParse(reader.GetString(), out var value) && value != Guid.Empty
                ? new InformationId(value) : throw new JsonException("Information ID is invalid.");
        public override void Write(Utf8JsonWriter writer, InformationId value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
    }

    private sealed class FactIdConverter : JsonConverter<FactId>
    {
        public override FactId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            Guid.TryParse(reader.GetString(), out var value) && value != Guid.Empty
                ? new FactId(value) : throw new JsonException("Fact ID is invalid.");
        public override void Write(Utf8JsonWriter writer, FactId value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
    }

    private sealed class HistoryEntryIdConverter : JsonConverter<HistoryEntryId>
    {
        public override HistoryEntryId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            Guid.TryParse(reader.GetString(), out var value) && value != Guid.Empty
                ? new HistoryEntryId(value) : throw new JsonException("History Entry ID is invalid.");
        public override void Write(Utf8JsonWriter writer, HistoryEntryId value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
    }

    private sealed class ReputationIdConverter : JsonConverter<ReputationId>
    {
        public override ReputationId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            Guid.TryParse(reader.GetString(), out var value) && value != Guid.Empty
                ? new ReputationId(value) : throw new JsonException("Reputation ID is invalid.");
        public override void Write(Utf8JsonWriter writer, ReputationId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }

    private sealed class MembershipIdConverter : JsonConverter<MembershipId>
    {
        public override MembershipId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            Guid.TryParse(reader.GetString(), out var value) && value != Guid.Empty
                ? new MembershipId(value) : throw new JsonException("Membership ID is invalid.");
        public override void Write(Utf8JsonWriter writer, MembershipId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }

    private sealed class EconomyAccountIdConverter : JsonConverter<EconomyAccountId>
    {
        public override EconomyAccountId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            Guid.TryParse(reader.GetString(), out var value) && value != Guid.Empty
                ? new EconomyAccountId(value) : throw new JsonException("Economy Account ID is invalid.");
        public override void Write(Utf8JsonWriter writer, EconomyAccountId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }

    private sealed class EconomyTransferIdConverter : JsonConverter<EconomyTransferId>
    {
        public override EconomyTransferId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            Guid.TryParse(reader.GetString(), out var value) && value != Guid.Empty
                ? new EconomyTransferId(value) : throw new JsonException("Economy Transfer ID is invalid.");
        public override void Write(Utf8JsonWriter writer, EconomyTransferId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }

    private sealed class DynamicWorldEventIdConverter : JsonConverter<DynamicWorldEventId>
    {
        public override DynamicWorldEventId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            Guid.TryParse(reader.GetString(), out var value) && value != Guid.Empty
                ? new DynamicWorldEventId(value) : throw new JsonException("Dynamic World Event ID is invalid.");
        public override void Write(Utf8JsonWriter writer, DynamicWorldEventId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }

    private sealed class TimeScaleConverter : JsonConverter<TimeScale>
    {
        public override TimeScale Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2 ||
                !root.TryGetProperty("numerator", out var numerator) || !root.TryGetProperty("denominator", out var denominator))
                throw new JsonException("Time scale contains unknown or missing properties.");
            return new TimeScale(numerator.GetInt64(), denominator.GetInt64());
        }
        public override void Write(Utf8JsonWriter writer, TimeScale value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("numerator", value.Numerator);
            writer.WriteNumber("denominator", value.Denominator);
            writer.WriteEndObject();
        }
    }

    /// <summary>Canonicalizes every persisted metadata map by ordinal key, independent of insertion order.</summary>
    private sealed class OrdinalStringDictionaryConverter : JsonConverter<IReadOnlyDictionary<string, string>>
    {
        public override IReadOnlyDictionary<string, string> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Metadata must be an object.");
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Metadata property is malformed.");
                var key = reader.GetString()!;
                if (!reader.Read() || reader.TokenType != JsonTokenType.String || !values.TryAdd(key, reader.GetString()!))
                    throw new JsonException("Metadata values must be strings with unique keys.");
            }
            return values;
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, string> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var item in value.OrderBy(item => item.Key, StringComparer.Ordinal)) writer.WriteString(item.Key, item.Value);
            writer.WriteEndObject();
        }
    }

    private sealed class OrdinalIntegerDictionaryConverter : JsonConverter<IReadOnlyDictionary<string, int>>
    {
        public override IReadOnlyDictionary<string, int> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Dimensions must be an object.");
            var values = new SortedDictionary<string, int>(StringComparer.Ordinal);
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Dimension property is malformed.");
                var key = reader.GetString()!;
                if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var value) || !values.TryAdd(key, value))
                    throw new JsonException("Dimension values must be integers with unique keys.");
            }
            return values;
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, int> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var item in value.OrderBy(item => item.Key, StringComparer.Ordinal)) writer.WriteNumber(item.Key, item.Value);
            writer.WriteEndObject();
        }
    }
}
