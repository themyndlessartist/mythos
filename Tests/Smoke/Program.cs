using Mythos.Framework;
using Mythos.Framework.Characters;
using Mythos.Framework.Entities;
using Mythos.Framework.Information;
using Mythos.Framework.History;
using Mythos.Framework.Npcs;
using Mythos.Framework.Persistence;
using Mythos.Framework.Regions;
using Mythos.Framework.Relationships;
using Mythos.Framework.Reputation;
using Mythos.Framework.Properties;
using Mythos.Framework.Organizations;
using Mythos.Framework.Economy;
using Mythos.Framework.DynamicEvents;
using Mythos.Framework.Time;

if (FrameworkAssembly.Name != "Mythos.Framework")
{
    Console.Error.WriteLine("Framework assembly identity is invalid.");
    return 1;
}

var calendar = CalendarModel.Create(new CalendarDefinition(
    new CalendarId("smoke"),
    1,
    1,
    [new CalendarPeriodDefinition("period", 1)],
    [])).Value!;
var clock = new WorldClock(calendar);
clock.Scheduler.ScheduleAfter(new ScheduleId("smoke-task"), new WorldDuration(1), clock.Timestamp, "smoke");
var advance = clock.Advance(new WorldDuration(1));
if (!advance.IsSuccess || advance.CurrentTimestamp.Value != 1 || advance.DueTasks.Count != 1)
{
    Console.Error.WriteLine("Time Framework smoke validation failed.");
    return 1;
}

var entities = new EntityRegistry();
var regions = new RegionFramework(entities);
var root = regions.CreateRoot(new RegionCategory("WorldScope"), clock.Timestamp.Value);
var child = root.IsSuccess
    ? regions.CreateRegion(new RegionCategory("NeutralArea"), root.Value!.Id, clock.Timestamp.Value)
    : default;
var actor = entities.Create(new EntityCategory("Character"), clock.Timestamp.Value);
if (!root.IsSuccess || !child.IsSuccess || !actor.IsSuccess ||
    !regions.AssignEntity(actor.Value!.Id, child.Value!.Id).IsSuccess ||
    regions.QueryAssignedEntities(child.Value.Id).Count != 1)
{
    Console.Error.WriteLine("Region Framework smoke validation failed.");
    return 1;
}

var characterEntity = entities.Create(new EntityCategory("Character"), clock.Timestamp.Value).Value!;
var characters = new CharacterRegistry(entities, new SmokeCharacterReferences());
var character = characters.Register(new CharacterProfileSnapshot(
    characterEntity.Id,
    new CharacterIdentity("neutral-fixture"),
    new CharacterStatusId("available"),
    new LifeStageId("established")));
if (!character.IsSuccess || !characters.ValidateReferences().IsSuccess)
{
    Console.Error.WriteLine("Character Framework smoke validation failed.");
    return 1;
}

if (!regions.AssignEntity(characterEntity.Id, child.Value!.Id).IsSuccess)
{
    Console.Error.WriteLine("NPC fixture Region assignment failed.");
    return 1;
}

var npcReferences = new SmokeNpcReferences();
var npcs = new NpcFramework(entities, characters, regions, npcReferences);
var npc = npcs.Register(new NpcProfileSnapshot(
    characterEntity.Id,
    npcReferences.Purpose,
    npcReferences.Schedule.Id,
    npcReferences.Schedule.Entries![0].StateId,
    0,
    new WorldTimestamp(2),
    NpcSimulationTier.Abstract,
    0));
var npcUpdate = npcs.Update(characterEntity.Id, new WorldTimestamp(3), 4);
if (!npc.IsSuccess || !npcUpdate.IsSuccess || npcUpdate.Value!.ProcessedTransitions != 2 || !npcs.ValidateReferences().IsSuccess)
{
    Console.Error.WriteLine("NPC Framework smoke validation failed.");
    return 1;
}

var storage = new InMemorySaveStorage();
var persistence = new WorldPersistence(storage);
var relationships = new RelationshipFramework(entities);
var relationship = relationships.Create(characterEntity.Id, root.Value!.Id, new RelationshipKindId("fixture-link"), clock.Timestamp);
if (!relationship.IsSuccess || !relationships.SetDimension(relationship.Value!.Id, new RelationshipDimensionId("trust"), 10, clock.Timestamp).IsSuccess)
{
    Console.Error.WriteLine("Relationship Framework smoke validation failed.");
    return 1;
}
var information = new InformationFramework(entities);
var proposition = information.Create(new InformationTypeId("fixture-location"), characterEntity.Id, child.Value.Id,
    new Dictionary<string, string> { ["state"] = "present" }, clock.Timestamp).Value!;
var fact = information.DeclareFact(proposition.Id, clock.Timestamp);
var awareness = information.SetAwareness(characterEntity.Id, proposition.Id, EpistemicStance.Known, 1000, clock.Timestamp);
if (!fact.IsSuccess || !awareness.IsSuccess)
{
    Console.Error.WriteLine("Information Framework smoke validation failed.");
    return 1;
}
var history = new WorldHistoryFramework(entities, regions);
var historyEntry = history.Record(new HistoryTypeId("fixture-created"), clock.Timestamp,
    [characterEntity.Id], child.Value.Id, 100, new Dictionary<string, string> { ["state"] = "created" }, "event:fixture");
if (!historyEntry.IsSuccess)
{
    Console.Error.WriteLine("World History Framework smoke validation failed.");
    return 1;
}
var reputation = new ReputationFramework(entities);
var reputationRecord = reputation.Create(characterEntity.Id, new ReputationAudienceTypeId("regional"), child.Value.Id,
    new ReputationDimensionId("standing"), 0, clock.Timestamp);
if (!reputationRecord.IsSuccess)
{
    Console.Error.WriteLine("Reputation Framework smoke validation failed.");
    return 1;
}
var properties = new PropertyFramework(entities);
var property = properties.Register(characterEntity.Id, new PropertyKindId("fixture-asset"), clock.Timestamp);
if (!property.IsSuccess)
{
    Console.Error.WriteLine("Property Framework smoke validation failed.");
    return 1;
}
var organizationEntity = entities.Create(new EntityCategory("Organization"), clock.Timestamp.Value).Value!;
var organizations = new OrganizationFramework(entities);
var organization = organizations.Register(organizationEntity.Id, new OrganizationKindId("fixture-group"), clock.Timestamp);
var membership = organizations.AddMembership(organizationEntity.Id, characterEntity.Id,
    [new OrganizationRoleId("member")], clock.Timestamp);
if (!organization.IsSuccess || !membership.IsSuccess)
{
    Console.Error.WriteLine("Organization Framework smoke validation failed.");
    return 1;
}
var economy = new EconomyFramework(entities);
var characterAccount = economy.OpenAccount(characterEntity.Id, new CurrencyId("fixture-unit"), 10, clock.Timestamp).Value!;
var organizationAccount = economy.OpenAccount(organizationEntity.Id, new CurrencyId("fixture-unit"), 0, clock.Timestamp).Value!;
var economyTransfer = economy.Transfer(characterAccount.Id, organizationAccount.Id, 4, clock.Timestamp);
if (!economyTransfer.IsSuccess)
{
    Console.Error.WriteLine("Economy Framework smoke validation failed.");
    return 1;
}
var dynamicEvents = new DynamicWorldEventFramework(entities, regions);
var dynamicEvent = dynamicEvents.Create(new DynamicWorldEventTypeId("fixture-situation"), clock.Timestamp, null,
    true, child.Value.Id, [characterEntity.Id, organizationEntity.Id], new Dictionary<string, string> { ["state"] = "active" });
if (!dynamicEvent.IsSuccess)
{
    Console.Error.WriteLine("Dynamic World Event Framework smoke validation failed.");
    return 1;
}
var world = new PersistentWorldState(entities, clock, regions, characters, npcs, relationships, information, history,
    reputation, properties, organizations, economy, dynamicEvents);
var saved = persistence.Save("smoke-slot", "neutral-smoke-world", world);
var loaded = persistence.Load("smoke-slot", new PersistenceLoadContext(calendar, new SmokeCharacterReferences(), npcReferences));
if (!saved.IsSuccess || !loaded.IsSuccess || loaded.Value!.Clock.Timestamp != clock.Timestamp ||
    loaded.Value.Entities.Find(characterEntity.Id).Value!.RegionId != child.Value!.Id ||
    loaded.Value.Npcs.Find(characterEntity.Id).Value!.CompletedTransitions != npcUpdate.Value.Profile.CompletedTransitions ||
    loaded.Value.Relationships.Find(relationship.Value.Id).Value!.Dimensions!["trust"] != 10 ||
    !loaded.Value.Information.IsAuthoritative(proposition.Id) ||
    loaded.Value.History.Find(historyEntry.Value!.Id).Value!.RegionEntityId != child.Value.Id ||
    loaded.Value.Reputation.Find(reputationRecord.Value!.Id).Value!.Value != 0 ||
    loaded.Value.Properties.Find(characterEntity.Id).Value!.KindId != new PropertyKindId("fixture-asset") ||
    loaded.Value.Organizations.FindActiveMembership(organizationEntity.Id, characterEntity.Id).Value!.Id != membership.Value!.Id ||
    loaded.Value.Economy.FindAccount(organizationAccount.Id).Value!.Balance != 4 ||
    loaded.Value.DynamicEvents.Find(dynamicEvent.Value!.Id).Value!.RegionEntityId != child.Value.Id)
{
    Console.Error.WriteLine($"Persistence Framework smoke validation failed: {saved.Error?.Message ?? loaded.Error?.Message}");
    return 1;
}

Console.WriteLine("Mythos framework smoke test passed.");
return 0;

file sealed class SmokeCharacterReferences : ICharacterReferenceValidator
{
    public bool IsKnownStatus(CharacterStatusId statusId) => statusId == new CharacterStatusId("available");

    public bool IsKnownLifeStage(LifeStageId lifeStageId) => lifeStageId == new LifeStageId("established");
}

file sealed class SmokeNpcReferences : INpcReferenceProvider
{
    public NpcPurposeId Purpose { get; } = new("neutral-participant");
    public NpcScheduleDefinition Schedule { get; } = new(
        new NpcScheduleId("neutral-cycle"),
        [
            new NpcScheduleEntry(new NpcScheduleStateId("state-a"), new WorldDuration(1)),
            new NpcScheduleEntry(new NpcScheduleStateId("state-b"), new WorldDuration(1)),
        ]);

    public bool IsKnownPurpose(NpcPurposeId purposeId) => purposeId == Purpose;
    public NpcScheduleDefinition? FindSchedule(NpcScheduleId scheduleId) => scheduleId == Schedule.Id ? Schedule : null;
}
