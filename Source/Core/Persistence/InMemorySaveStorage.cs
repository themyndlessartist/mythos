namespace Mythos.Framework.Persistence;

/// <summary>Replaceable M-001 transactional storage adapter. Data becomes visible only at commit.</summary>
public sealed class InMemorySaveStorage : ISaveStorage
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, byte[]>> slots = new(StringComparer.Ordinal);

    public bool FailNextCommit { get; set; }

    public PersistenceResult<IReadOnlyDictionary<string, byte[]>> Read(string slotId)
    {
        if (!Valid(slotId)) return PersistenceResult<IReadOnlyDictionary<string, byte[]>>.Failure(PersistenceErrorCodes.InvalidData, "Save slot ID is invalid.");
        if (!slots.TryGetValue(slotId, out var data))
            return PersistenceResult<IReadOnlyDictionary<string, byte[]>>.Failure(PersistenceErrorCodes.MissingPartition, "Save slot was not found.");
        return PersistenceResult<IReadOnlyDictionary<string, byte[]>>.Success(Clone(data));
    }

    public ISaveWriteTransaction BeginWrite(string slotId) => new Transaction(this, slotId);

    private static bool Valid(string value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
    private static IReadOnlyDictionary<string, byte[]> Clone(IReadOnlyDictionary<string, byte[]> source) =>
        source.ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.Ordinal);

    private sealed class Transaction(InMemorySaveStorage owner, string slotId) : ISaveWriteTransaction
    {
        private readonly Dictionary<string, byte[]> staged = new(StringComparer.Ordinal);
        private bool completed;

        public PersistenceResult Write(string partitionId, ReadOnlyMemory<byte> data)
        {
            if (completed || !Valid(slotId) || !Valid(partitionId) || data.IsEmpty)
                return PersistenceResult.Failure(PersistenceErrorCodes.StorageFailure, "Write transaction or partition is invalid.", partitionId);
            staged[partitionId] = data.ToArray();
            return PersistenceResult.Success();
        }

        public PersistenceResult Commit()
        {
            if (completed) return PersistenceResult.Failure(PersistenceErrorCodes.StorageFailure, "Write transaction is already complete.");
            completed = true;
            if (owner.FailNextCommit)
            {
                owner.FailNextCommit = false;
                return PersistenceResult.Failure(PersistenceErrorCodes.StorageFailure, "Injected commit failure left the prior save unchanged.");
            }
            owner.slots[slotId] = Clone(staged);
            return PersistenceResult.Success();
        }

        public void Dispose() => completed = true;
    }
}
