namespace Mythos.Genesis;

public sealed record LakewoodSettlementSnapshot(
    int Version,
    IReadOnlyDictionary<string, int>? Stockpile,
    int LaborContributed,
    bool ProjectComplete,
    string? CompletionStateId)
{
    public const int CurrentVersion = 1;
}

public readonly record struct LakewoodSettlementResult(string? Error)
{
    public bool IsSuccess => Error is null;
    public static LakewoodSettlementResult Success() => new(null);
    public static LakewoodSettlementResult Failure(string error) => new(error);
}

/// <summary>M-003 title state for the approved Storehouse proof; not a shared construction framework.</summary>
public sealed class LakewoodSettlementState(LakewoodProjectDefinition project)
{
    private readonly Dictionary<string, int> stockpile = new(StringComparer.Ordinal);

    public LakewoodProjectDefinition Project { get; } = project ?? throw new ArgumentNullException(nameof(project));
    public int LaborContributed { get; private set; }
    public bool ProjectComplete { get; private set; }
    public string? CompletionStateId { get; private set; }
    public IReadOnlyDictionary<string, int> Stockpile => new Dictionary<string, int>(stockpile, StringComparer.Ordinal);

    public LakewoodSettlementResult ContributeResource(string resourceId, int amount)
    {
        if (ProjectComplete) return LakewoodSettlementResult.Failure("project.complete");
        if (amount <= 0 || !Project.ResourceRequirements.Any(item => item.ResourceId == resourceId))
            return LakewoodSettlementResult.Failure("resource.invalid");
        int next;
        try { next = checked(stockpile.GetValueOrDefault(resourceId) + amount); }
        catch (OverflowException) { return LakewoodSettlementResult.Failure("resource.overflow"); }
        stockpile[resourceId] = next;
        return LakewoodSettlementResult.Success();
    }

    public LakewoodSettlementResult ContributeLabor(int amount)
    {
        if (ProjectComplete) return LakewoodSettlementResult.Failure("project.complete");
        if (amount <= 0) return LakewoodSettlementResult.Failure("labor.invalid");
        try { LaborContributed = checked(LaborContributed + amount); }
        catch (OverflowException) { return LakewoodSettlementResult.Failure("labor.overflow"); }
        return LakewoodSettlementResult.Success();
    }

    public LakewoodSettlementResult TryComplete()
    {
        if (ProjectComplete) return LakewoodSettlementResult.Failure("project.complete");
        if (LaborContributed < Project.LaborRequired ||
            Project.ResourceRequirements.Any(requirement => stockpile.GetValueOrDefault(requirement.ResourceId) < requirement.Amount))
            return LakewoodSettlementResult.Failure("project.requirements-missing");

        foreach (var requirement in Project.ResourceRequirements)
            stockpile[requirement.ResourceId] -= requirement.Amount;
        ProjectComplete = true;
        CompletionStateId = Project.CompletionStateId;
        return LakewoodSettlementResult.Success();
    }

    public LakewoodSettlementSnapshot ExportSnapshot() => new(
        LakewoodSettlementSnapshot.CurrentVersion,
        new SortedDictionary<string, int>(stockpile, StringComparer.Ordinal),
        LaborContributed,
        ProjectComplete,
        CompletionStateId);

    public LakewoodSettlementResult Restore(LakewoodSettlementSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.Version != LakewoodSettlementSnapshot.CurrentVersion || snapshot.Stockpile is null ||
            snapshot.LaborContributed < 0 || snapshot.Stockpile.Any(item =>
                string.IsNullOrWhiteSpace(item.Key) || item.Value < 0 ||
                !Project.ResourceRequirements.Any(requirement => requirement.ResourceId == item.Key)) ||
            (snapshot.ProjectComplete != (snapshot.CompletionStateId == Project.CompletionStateId)))
            return LakewoodSettlementResult.Failure("snapshot.invalid");

        stockpile.Clear();
        foreach (var item in snapshot.Stockpile.OrderBy(item => item.Key, StringComparer.Ordinal)) stockpile.Add(item.Key, item.Value);
        LaborContributed = snapshot.LaborContributed;
        ProjectComplete = snapshot.ProjectComplete;
        CompletionStateId = snapshot.CompletionStateId;
        return LakewoodSettlementResult.Success();
    }
}
