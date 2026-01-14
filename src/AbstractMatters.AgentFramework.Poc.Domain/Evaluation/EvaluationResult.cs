namespace AbstractMatters.AgentFramework.Poc.Domain.Evaluation;

public class EvaluationResult
{
    public Guid Id { get; private init; }
    public string RunId { get; private init; } = string.Empty;
    public string ScorerName { get; private init; } = string.Empty;
    public double Score { get; private init; }
    public string? Reasoning { get; private init; }
    public IReadOnlyDictionary<string, string> Metadata { get; private init; } = new Dictionary<string, string>();
    public DateTime EvaluatedAt { get; private init; }

    private EvaluationResult() { }

    public static EvaluationResult Create(string runId, string scorerName, double score)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Run ID cannot be empty.", nameof(runId));
        if (string.IsNullOrWhiteSpace(scorerName))
            throw new ArgumentException("Scorer name cannot be empty.", nameof(scorerName));
        if (score < 0 || score > 1)
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 0 and 1.");

        return new EvaluationResult
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            ScorerName = scorerName,
            Score = score,
            EvaluatedAt = DateTime.UtcNow
        };
    }

    public EvaluationResult WithReasoning(string reasoning)
    {
        return CloneWith(reasoning: reasoning);
    }

    public EvaluationResult WithMetadata(string key, string value)
    {
        var newMetadata = new Dictionary<string, string>(Metadata)
        {
            [key] = value
        };
        return CloneWith(metadata: newMetadata);
    }

    public bool IsPassing(double threshold = 0.8)
    {
        return Score >= threshold;
    }

    private EvaluationResult CloneWith(
        string? reasoning = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new EvaluationResult
        {
            Id = Id,
            RunId = RunId,
            ScorerName = ScorerName,
            Score = Score,
            Reasoning = reasoning ?? Reasoning,
            Metadata = metadata ?? Metadata,
            EvaluatedAt = EvaluatedAt
        };
    }
}
