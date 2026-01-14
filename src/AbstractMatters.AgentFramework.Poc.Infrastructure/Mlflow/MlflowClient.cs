using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AbstractMatters.AgentFramework.Poc.Application.Mlflow;
using AbstractMatters.AgentFramework.Poc.Infrastructure.Mlflow.Models;
using LanguageExt;
using LanguageExt.Common;

namespace AbstractMatters.AgentFramework.Poc.Infrastructure.Mlflow;

public class MlflowClient : IMlflowClient
{
    private const string ApiBasePath = "/api/2.0/mlflow";
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public MlflowClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions();
    }

    public async Task<Fin<MlflowExperiment>> CreateExperimentAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Error.New("Experiment name cannot be empty");

        var request = new CreateExperimentRequest(name);
        var response = await PostAsync<CreateExperimentResponse>(
            $"{ApiBasePath}/experiments/create",
            request,
            cancellationToken);

        return response.Match(
            Succ: r => Fin<MlflowExperiment>.Succ(new MlflowExperiment(r.ExperimentId, name, null, "active")),
            Fail: e => Fin<MlflowExperiment>.Fail(e));
    }

    public async Task<Fin<MlflowExperiment>> GetExperimentAsync(string experimentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(experimentId))
            return Error.New("Experiment ID cannot be empty");

        var response = await GetAsync<GetExperimentResponse>(
            $"{ApiBasePath}/experiments/get?experiment_id={experimentId}",
            cancellationToken);

        return response.Match(
            Succ: r => Fin<MlflowExperiment>.Succ(new MlflowExperiment(
                r.Experiment.ExperimentId,
                r.Experiment.Name,
                r.Experiment.ArtifactLocation,
                r.Experiment.LifecycleStage)),
            Fail: e => Fin<MlflowExperiment>.Fail(e));
    }

    public async Task<Fin<MlflowExperiment>> GetExperimentByNameAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Error.New("Experiment name cannot be empty");

        var response = await GetAsync<GetExperimentResponse>(
            $"{ApiBasePath}/experiments/get-by-name?experiment_name={Uri.EscapeDataString(name)}",
            cancellationToken);

        return response.Match(
            Succ: r => Fin<MlflowExperiment>.Succ(new MlflowExperiment(
                r.Experiment.ExperimentId,
                r.Experiment.Name,
                r.Experiment.ArtifactLocation,
                r.Experiment.LifecycleStage)),
            Fail: e => Fin<MlflowExperiment>.Fail(e));
    }

    public async Task<Fin<Unit>> SetExperimentTagAsync(string experimentId, string key, string value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(experimentId))
            return Error.New("Experiment ID cannot be empty");
        if (string.IsNullOrWhiteSpace(key))
            return Error.New("Tag key cannot be empty");

        var request = new SetExperimentTagRequest(experimentId, key, value);
        return await PostEmptyResponseAsync(
            $"{ApiBasePath}/experiments/set-experiment-tag",
            request,
            cancellationToken);
    }

    public async Task<Fin<MlflowRun>> CreateRunAsync(string experimentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(experimentId))
            return Error.New("Experiment ID cannot be empty");

        var request = new CreateRunRequest(experimentId);
        var response = await PostAsync<CreateRunResponse>(
            $"{ApiBasePath}/runs/create",
            request,
            cancellationToken);

        return response.Match(
            Succ: r => Fin<MlflowRun>.Succ(new MlflowRun(
                r.Run.Info.RunId,
                r.Run.Info.ExperimentId,
                r.Run.Info.Status,
                r.Run.Info.StartTime)),
            Fail: e => Fin<MlflowRun>.Fail(e));
    }

    public async Task<Fin<Unit>> LogMetricAsync(string runId, string key, double value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runId))
            return Error.New("Run ID cannot be empty");
        if (string.IsNullOrWhiteSpace(key))
            return Error.New("Metric key cannot be empty");

        var request = new LogMetricRequest(runId, key, value, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        return await PostEmptyResponseAsync(
            $"{ApiBasePath}/runs/log-metric",
            request,
            cancellationToken);
    }

    public async Task<Fin<Unit>> LogParamAsync(string runId, string key, string value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runId))
            return Error.New("Run ID cannot be empty");
        if (string.IsNullOrWhiteSpace(key))
            return Error.New("Param key cannot be empty");

        var request = new LogParamRequest(runId, key, value);
        return await PostEmptyResponseAsync(
            $"{ApiBasePath}/runs/log-parameter",
            request,
            cancellationToken);
    }

    public async Task<Fin<MlflowRunInfo>> UpdateRunAsync(string runId, string status, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runId))
            return Error.New("Run ID cannot be empty");
        if (string.IsNullOrWhiteSpace(status))
            return Error.New("Status cannot be empty");

        var request = new UpdateRunRequest(runId, status, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var response = await PostAsync<UpdateRunResponse>(
            $"{ApiBasePath}/runs/update",
            request,
            cancellationToken);

        return response.Match(
            Succ: r => Fin<MlflowRunInfo>.Succ(new MlflowRunInfo(
                r.RunInfo.RunId,
                r.RunInfo.Status,
                r.RunInfo.EndTime)),
            Fail: e => Fin<MlflowRunInfo>.Fail(e));
    }

    public async Task<Fin<Seq<MlflowMetric>>> GetMetricHistoryAsync(string runId, string metricKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runId))
            return Error.New("Run ID cannot be empty");
        if (string.IsNullOrWhiteSpace(metricKey))
            return Error.New("Metric key cannot be empty");

        var response = await GetAsync<GetMetricHistoryResponse>(
            $"{ApiBasePath}/metrics/get-history?run_id={runId}&metric_key={metricKey}",
            cancellationToken);

        return response.Match(
            Succ: r =>
            {
                var metrics = r.Metrics
                    .Select(m => new MlflowMetric(m.Key, m.Value, m.Timestamp, m.Step))
                    .ToSeq();
                return Fin<Seq<MlflowMetric>>.Succ(metrics);
            },
            Fail: e => Fin<Seq<MlflowMetric>>.Fail(e));
    }

    private async Task<Fin<T>> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(path, cancellationToken);
            return await HandleResponse<T>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Error.New(ex);
        }
    }

    private async Task<Fin<T>> PostAsync<T>(string path, object request, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(path, content, cancellationToken);
            return await HandleResponse<T>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Error.New(ex);
        }
    }

    private async Task<Fin<Unit>> PostEmptyResponseAsync(string path, object request, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(path, httpContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                try
                {
                    var error = JsonSerializer.Deserialize<MlflowErrorResponse>(content, _jsonOptions);
                    return Error.New($"{error?.ErrorCode}: {error?.Message}");
                }
                catch
                {
                    return Error.New($"HTTP {(int)response.StatusCode}: {content}");
                }
            }

            return Fin<Unit>.Succ(Unit.Default);
        }
        catch (Exception ex)
        {
            return Error.New(ex);
        }
    }

    private async Task<Fin<T>> HandleResponse<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                var error = JsonSerializer.Deserialize<MlflowErrorResponse>(content, _jsonOptions);
                return Error.New($"{error?.ErrorCode}: {error?.Message}");
            }
            catch
            {
                return Error.New($"HTTP {(int)response.StatusCode}: {content}");
            }
        }

        try
        {
            var trimmed = content.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed == "{}")
            {
                return Fin<T>.Succ(default!);
            }

            var result = JsonSerializer.Deserialize<T>(content, _jsonOptions);
            return result is null
                ? Error.New("Failed to deserialize response")
                : Fin<T>.Succ(result);
        }
        catch (JsonException ex)
        {
            return Error.New($"Failed to parse response: {ex.Message}");
        }
    }
}
