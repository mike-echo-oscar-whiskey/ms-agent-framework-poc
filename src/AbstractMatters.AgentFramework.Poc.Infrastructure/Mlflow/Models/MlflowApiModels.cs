using System.Text.Json.Serialization;

namespace AbstractMatters.AgentFramework.Poc.Infrastructure.Mlflow.Models;

// Request models
internal record CreateExperimentRequest(
    [property: JsonPropertyName("name")] string Name);

internal record CreateRunRequest(
    [property: JsonPropertyName("experiment_id")] string ExperimentId);

internal record LogMetricRequest(
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] double Value,
    [property: JsonPropertyName("timestamp")] long Timestamp);

internal record LogParamRequest(
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] string Value);

internal record UpdateRunRequest(
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("end_time")] long EndTime);

internal record SetExperimentTagRequest(
    [property: JsonPropertyName("experiment_id")] string ExperimentId,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] string Value);

// Response models
internal record CreateExperimentResponse(
    [property: JsonPropertyName("experiment_id")] string ExperimentId);

internal record GetExperimentResponse(
    [property: JsonPropertyName("experiment")] ExperimentDto Experiment);

internal record ExperimentDto(
    [property: JsonPropertyName("experiment_id")] string ExperimentId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("artifact_location")] string? ArtifactLocation,
    [property: JsonPropertyName("lifecycle_stage")] string LifecycleStage);

internal record CreateRunResponse(
    [property: JsonPropertyName("run")] RunDto Run);

internal record RunDto(
    [property: JsonPropertyName("info")] RunInfoDto Info);

internal record RunInfoDto(
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("experiment_id")] string ExperimentId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("start_time")] long StartTime,
    [property: JsonPropertyName("end_time")] long? EndTime);

internal record UpdateRunResponse(
    [property: JsonPropertyName("run_info")] UpdatedRunInfoDto RunInfo);

internal record UpdatedRunInfoDto(
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("end_time")] long? EndTime);

internal record GetMetricHistoryResponse(
    [property: JsonPropertyName("metrics")] MetricDto[] Metrics);

internal record MetricDto(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] double Value,
    [property: JsonPropertyName("timestamp")] long Timestamp,
    [property: JsonPropertyName("step")] int Step);

internal record MlflowErrorResponse(
    [property: JsonPropertyName("error_code")] string ErrorCode,
    [property: JsonPropertyName("message")] string Message);
