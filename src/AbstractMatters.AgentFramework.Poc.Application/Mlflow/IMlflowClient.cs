using LanguageExt;

namespace AbstractMatters.AgentFramework.Poc.Application.Mlflow;

public interface IMlflowClient
{
    Task<Fin<MlflowExperiment>> CreateExperimentAsync(string name, CancellationToken cancellationToken);
    Task<Fin<MlflowExperiment>> GetExperimentAsync(string experimentId, CancellationToken cancellationToken);
    Task<Fin<MlflowExperiment>> GetExperimentByNameAsync(string name, CancellationToken cancellationToken);
    Task<Fin<Unit>> SetExperimentTagAsync(string experimentId, string key, string value, CancellationToken cancellationToken);
    Task<Fin<MlflowRun>> CreateRunAsync(string experimentId, CancellationToken cancellationToken);
    Task<Fin<Unit>> LogMetricAsync(string runId, string key, double value, CancellationToken cancellationToken);
    Task<Fin<Unit>> LogParamAsync(string runId, string key, string value, CancellationToken cancellationToken);
    Task<Fin<MlflowRunInfo>> UpdateRunAsync(string runId, string status, CancellationToken cancellationToken);
    Task<Fin<Seq<MlflowMetric>>> GetMetricHistoryAsync(string runId, string metricKey, CancellationToken cancellationToken);
}

public record MlflowExperiment(
    string ExperimentId,
    string Name,
    string? ArtifactLocation,
    string LifecycleStage);

public record MlflowRun(
    string RunId,
    string ExperimentId,
    string Status,
    long StartTime);

public record MlflowRunInfo(
    string RunId,
    string Status,
    long? EndTime);

public record MlflowMetric(
    string Key,
    double Value,
    long Timestamp,
    int Step);
