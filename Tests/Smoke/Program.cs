using Mythos.Framework;
using Mythos.Framework.Characters;
using Mythos.Framework.Entities;
using Mythos.Framework.Npcs;
using Mythos.Framework.Regions;
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
