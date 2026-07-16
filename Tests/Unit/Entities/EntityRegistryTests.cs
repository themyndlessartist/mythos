using Mythos.Framework.Entities;

namespace Mythos.Framework.UnitTests.Entities;

public sealed class EntityRegistryTests
{
    private static readonly EntityCategory Character = new("Character");
    private static readonly EntityCategory Region = new("Region");

    [Fact]
    public void CreateRegistersStableIdentity()
    {
        var expectedId = Id(1);
        var registry = new EntityRegistry(new SequenceIdGenerator(expectedId));

        var created = registry.Create(Character, 100);

        Assert.True(created.IsSuccess);
        Assert.Equal(expectedId, created.Value!.Id);
        Assert.Equal(EntityLifecycleState.Active, created.Value.LifecycleState);
        Assert.True(registry.Exists(expectedId));
        Assert.True(registry.IsActive(expectedId));
    }

    [Fact]
    public void GeneratorCannotReuseRegisteredIdentity()
    {
        var repeatedId = Id(1);
        var registry = new EntityRegistry(new RepeatingIdGenerator(repeatedId));
        Assert.True(registry.Create(Character, 0).IsSuccess);

        var duplicate = registry.Create(Character, 1);

        Assert.False(duplicate.IsSuccess);
        Assert.Equal(EntityErrorCodes.DuplicateId, duplicate.Error!.Code);
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void TagsAndComponentsAreIdempotentAndQueryable()
    {
        var id = Id(1);
        var registry = RegistryWith(id);
        var named = new EntityTag("Named");
        var characterData = new ComponentTypeId("CharacterData");

        Assert.True(registry.AddTag(id, named).IsSuccess);
        Assert.True(registry.AddTag(id, named).IsSuccess);
        Assert.True(registry.RegisterComponent(id, characterData).IsSuccess);
        Assert.True(registry.RegisterComponent(id, characterData).IsSuccess);

        var snapshot = registry.Find(id).Value!;
        Assert.Single(snapshot.Tags!);
        Assert.Single(snapshot.ComponentTypes!);
        Assert.Equal(id, Assert.Single(registry.QueryByTag(named)).Id);
    }

    [Fact]
    public void TerminalEntityRemainsReferenceableAndCannotReactivate()
    {
        var id = Id(1);
        var registry = RegistryWith(id);

        Assert.True(registry.Retire(id, 50).IsSuccess);
        var reactivation = registry.ChangeLifecycle(id, EntityLifecycleState.Active, 60);

        Assert.False(reactivation.IsSuccess);
        Assert.Equal(EntityErrorCodes.InvalidLifecycleTransition, reactivation.Error!.Code);
        Assert.True(registry.Exists(id));
        Assert.False(registry.IsActive(id));
        Assert.Equal(50, registry.Find(id).Value!.RetiredAt);
    }

    [Fact]
    public void ParentAssignmentRejectsCyclesAndPreservesPriorState()
    {
        var rootId = Id(1);
        var childId = Id(2);
        var registry = RegistryWith(rootId, childId);
        Assert.True(registry.AssignParent(childId, rootId).IsSuccess);

        var cycle = registry.AssignParent(rootId, childId);

        Assert.False(cycle.IsSuccess);
        Assert.Equal(EntityErrorCodes.HierarchyCycle, cycle.Error!.Code);
        Assert.Null(registry.Find(rootId).Value!.ParentId);
        Assert.Equal(rootId, registry.Find(childId).Value!.ParentId);
    }

    [Fact]
    public void OwnerAssignmentRejectsCycles()
    {
        var firstId = Id(1);
        var secondId = Id(2);
        var registry = RegistryWith(firstId, secondId);
        Assert.True(registry.AssignOwner(secondId, firstId).IsSuccess);

        var cycle = registry.AssignOwner(firstId, secondId);

        Assert.False(cycle.IsSuccess);
        Assert.Equal(EntityErrorCodes.OwnershipCycle, cycle.Error!.Code);
        Assert.Equal(secondId, Assert.Single(registry.EnumerateOwned(firstId)).Id);
    }

    [Fact]
    public void RegionAssignmentRequiresRegisteredRegionCategory()
    {
        var characterId = Id(1);
        var wrongCategoryId = Id(2);
        var regionId = Id(3);
        var registry = new EntityRegistry(new SequenceIdGenerator(characterId, wrongCategoryId, regionId));
        Assert.True(registry.Create(Character, 0).IsSuccess);
        Assert.True(registry.Create(Character, 0).IsSuccess);
        Assert.True(registry.Create(Region, 0).IsSuccess);

        var invalid = registry.AssignRegion(characterId, wrongCategoryId);
        var valid = registry.AssignRegion(characterId, regionId);

        Assert.False(invalid.IsSuccess);
        Assert.Equal(EntityErrorCodes.InvalidReference, invalid.Error!.Code);
        Assert.True(valid.IsSuccess);
        Assert.Equal(characterId, Assert.Single(registry.EnumerateRegion(regionId)).Id);
    }

    [Fact]
    public void RegisterRestoresSnapshotWithoutGeneratingNewIdentity()
    {
        var parentId = Id(1);
        var childId = Id(2);
        var registry = new EntityRegistry();
        Assert.True(registry.Register(Snapshot(parentId)).IsSuccess);
        var child = Snapshot(childId, tags: [new EntityTag("Persistent")], parentId: parentId);

        var restored = registry.Register(child);

        Assert.True(restored.IsSuccess);
        Assert.Equal(childId, restored.Value!.Id);
        Assert.Equal(parentId, restored.Value.ParentId);
        Assert.Equal(childId, Assert.Single(registry.EnumerateChildren(parentId)).Id);
    }

    [Fact]
    public void RegisterRejectsMissingReference()
    {
        var child = Snapshot(Id(1), parentId: Id(2));

        var result = new EntityRegistry().Register(child);

        Assert.False(result.IsSuccess);
        Assert.Equal(EntityErrorCodes.InvalidReference, result.Error!.Code);
    }

    [Fact]
    public void RegisterRejectsRetirementTimestampOnActiveEntity()
    {
        var invalid = Snapshot(Id(1), retiredAt: 10);

        var result = new EntityRegistry().Register(invalid);

        Assert.False(result.IsSuccess);
        Assert.Equal(EntityErrorCodes.InvalidTimestamp, result.Error!.Code);
    }

    [Fact]
    public void RegisterRejectsNegativeCreationTimestamp()
    {
        var result = new EntityRegistry().Register(Snapshot(Id(1), createdAt: -1));

        Assert.False(result.IsSuccess);
        Assert.Equal(EntityErrorCodes.InvalidTimestamp, result.Error!.Code);
    }

    [Fact]
    public void RegisterRejectsRetirementBeforeCreation()
    {
        var result = new EntityRegistry().Register(Snapshot(
            Id(1),
            lifecycle: EntityLifecycleState.Retired,
            createdAt: 10,
            retiredAt: 9));

        Assert.False(result.IsSuccess);
        Assert.Equal(EntityErrorCodes.InvalidTimestamp, result.Error!.Code);
    }

    [Fact]
    public void ExportOrderIsDeterministic()
    {
        var registry = new EntityRegistry(new SequenceIdGenerator(Id(3), Id(1), Id(2)));
        registry.Create(Character, 0);
        registry.Create(Character, 0);
        registry.Create(Character, 0);

        var ids = registry.ExportSnapshots().Select(snapshot => snapshot.Id).ToArray();

        Assert.Equal([Id(1), Id(2), Id(3)], ids);
    }

    [Theory]
    [InlineData(EntityLifecycleState.Retired)]
    [InlineData(EntityLifecycleState.Destroyed)]
    public void RegisterRejectsTerminalSnapshotWithoutRetirementTimestamp(EntityLifecycleState lifecycle)
    {
        var result = new EntityRegistry().Register(Snapshot(Id(1), lifecycle: lifecycle));

        Assert.False(result.IsSuccess);
        Assert.Equal(EntityErrorCodes.InvalidTimestamp, result.Error!.Code);
    }

    [Fact]
    public void RegisterRejectsUndefinedLifecycle()
    {
        var result = new EntityRegistry().Register(Snapshot(Id(1), lifecycle: (EntityLifecycleState)99));

        Assert.False(result.IsSuccess);
        Assert.Equal(EntityErrorCodes.InvalidSnapshot, result.Error!.Code);
    }

    [Fact]
    public void RegisterRejectsUninitializedIdentifiers()
    {
        var registry = new EntityRegistry();

        var category = registry.Register(new EntitySnapshot(Id(1), default, EntityLifecycleState.Active, [], null, null, null, [], 0, null));
        var tag = registry.Register(new EntitySnapshot(Id(2), Character, EntityLifecycleState.Active, [default], null, null, null, [], 0, null));
        var component = registry.Register(new EntitySnapshot(Id(3), Character, EntityLifecycleState.Active, [], null, null, null, [default], 0, null));

        Assert.Equal(EntityErrorCodes.InvalidSnapshot, category.Error!.Code);
        Assert.Equal(EntityErrorCodes.InvalidSnapshot, tag.Error!.Code);
        Assert.Equal(EntityErrorCodes.InvalidSnapshot, component.Error!.Code);
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void RegisterRejectsNullCollectionsWithoutThrowing()
    {
        var registry = new EntityRegistry();

        var tags = registry.Register(new EntitySnapshot(Id(1), Character, EntityLifecycleState.Active, null, null, null, null, [], 0, null));
        var components = registry.Register(new EntitySnapshot(Id(2), Character, EntityLifecycleState.Active, [], null, null, null, null, 0, null));

        Assert.Equal(EntityErrorCodes.InvalidSnapshot, tags.Error!.Code);
        Assert.Equal(EntityErrorCodes.InvalidSnapshot, components.Error!.Code);
    }

    [Fact]
    public void RegisterRejectsEmptyReference()
    {
        var emptyParent = new EntityRegistry().Register(Snapshot(Id(1), parentId: default(EntityId)));

        Assert.Equal(EntityErrorCodes.InvalidReference, emptyParent.Error!.Code);
    }

    [Fact]
    public void SnapshotCollectionsCannotBeMutatedThroughPublicProjection()
    {
        var tags = new[] { new EntityTag("Persistent") };
        var components = new[] { new ComponentTypeId("CharacterData") };
        var snapshot = Snapshot(Id(1), tags: tags, componentTypes: components);

        tags[0] = new EntityTag("Changed");
        components[0] = new ComponentTypeId("ChangedData");

        Assert.Equal("Persistent", Assert.Single(snapshot.Tags!).Value);
        Assert.Equal("CharacterData", Assert.Single(snapshot.ComponentTypes!).Value);
        Assert.Throws<NotSupportedException>(() => ((IList<EntityTag>)snapshot.Tags!)[0] = new EntityTag("Blocked"));
    }

    private static EntityRegistry RegistryWith(params EntityId[] ids)
    {
        var registry = new EntityRegistry(new SequenceIdGenerator(ids));
        foreach (var _ in ids)
        {
            Assert.True(registry.Create(Character, 0).IsSuccess);
        }

        return registry;
    }

    private static EntitySnapshot Snapshot(
        EntityId id,
        EntityCategory? category = null,
        EntityLifecycleState lifecycle = EntityLifecycleState.Active,
        IReadOnlyList<EntityTag>? tags = default,
        EntityId? parentId = null,
        EntityId? ownerId = null,
        EntityId? regionId = null,
        IReadOnlyList<ComponentTypeId>? componentTypes = default,
        long createdAt = 0,
        long? retiredAt = null) => new(
        id,
        category ?? Character,
        lifecycle,
        tags ?? [],
        parentId,
        ownerId,
        regionId,
        componentTypes ?? [],
        createdAt,
        retiredAt);

    private static EntityId Id(int value) => new(new Guid(value, 0, 0, new byte[8]));

    private sealed class SequenceIdGenerator(params EntityId[] ids) : IEntityIdGenerator
    {
        private readonly Queue<EntityId> ids = new(ids);

        public EntityId Create() => ids.Dequeue();
    }

    private sealed class RepeatingIdGenerator(EntityId id) : IEntityIdGenerator
    {
        public EntityId Create() => id;
    }
}
