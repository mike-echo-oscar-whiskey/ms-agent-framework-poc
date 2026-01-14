using System.Net;
using System.Text.Json;
using AbstractMatters.AgentFramework.Poc.Application.Mlflow;
using AbstractMatters.AgentFramework.Poc.Infrastructure.Mlflow;
using AwesomeAssertions;
using LanguageExt;

namespace AbstractMatters.AgentFramework.Poc.Infrastructure.Tests.Mlflow;

public class MlflowClientTests
{
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly MlflowClient _sut;

    public MlflowClientTests()
    {
        _mockHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        _sut = new MlflowClient(_httpClient);
    }

    [Fact]
    public async Task CreateExperimentAsync_WithValidName_ReturnsSuccessWithExperimentId()
    {
        // Arrange
        const string experimentName = "test-experiment";
        const string expectedExperimentId = "123";

        _mockHandler.SetupResponse(
            HttpMethod.Post,
            "/api/2.0/mlflow/experiments/create",
            new { experiment_id = expectedExperimentId });

        // Act
        var result = await _sut.CreateExperimentAsync(experimentName, CancellationToken.None);

        // Assert
        result.IsSucc.Should().BeTrue();
        result.IfSucc(exp => exp.ExperimentId.Should().Be(expectedExperimentId));
    }

    [Fact]
    public async Task CreateExperimentAsync_WhenApiReturnsError_ReturnsFailure()
    {
        // Arrange
        const string experimentName = "existing-experiment";

        _mockHandler.SetupErrorResponse(
            HttpMethod.Post,
            "/api/2.0/mlflow/experiments/create",
            HttpStatusCode.BadRequest,
            "RESOURCE_ALREADY_EXISTS",
            "Experiment already exists");

        // Act
        var result = await _sut.CreateExperimentAsync(experimentName, CancellationToken.None);

        // Assert
        result.IsFail.Should().BeTrue();
        result.IfFail(err => err.Message.Should().Contain("RESOURCE_ALREADY_EXISTS"));
    }

    [Fact]
    public async Task GetExperimentAsync_WithValidId_ReturnsExperiment()
    {
        // Arrange
        const string experimentId = "123";

        _mockHandler.SetupResponse(
            HttpMethod.Get,
            "/api/2.0/mlflow/experiments/get",
            new
            {
                experiment = new
                {
                    experiment_id = experimentId,
                    name = "test-experiment",
                    artifact_location = "/mlflow/artifacts/123",
                    lifecycle_stage = "active"
                }
            });

        // Act
        var result = await _sut.GetExperimentAsync(experimentId, CancellationToken.None);

        // Assert
        result.IsSucc.Should().BeTrue();
        result.IfSucc(exp =>
        {
            exp.ExperimentId.Should().Be(experimentId);
            exp.Name.Should().Be("test-experiment");
            exp.LifecycleStage.Should().Be("active");
        });
    }

    [Fact]
    public async Task CreateRunAsync_WithValidExperimentId_ReturnsRun()
    {
        // Arrange
        const string experimentId = "123";
        const string runId = "run-456";

        _mockHandler.SetupResponse(
            HttpMethod.Post,
            "/api/2.0/mlflow/runs/create",
            new
            {
                run = new
                {
                    info = new
                    {
                        run_id = runId,
                        experiment_id = experimentId,
                        status = "RUNNING",
                        start_time = 1704067200000L
                    }
                }
            });

        // Act
        var result = await _sut.CreateRunAsync(experimentId, CancellationToken.None);

        // Assert
        result.IsSucc.Should().BeTrue();
        result.IfSucc(run =>
        {
            run.RunId.Should().Be(runId);
            run.ExperimentId.Should().Be(experimentId);
            run.Status.Should().Be("RUNNING");
        });
    }

    [Fact]
    public async Task LogMetricAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        const string runId = "run-456";
        const string key = "accuracy";
        const double value = 0.95;

        _mockHandler.SetupResponse(
            HttpMethod.Post,
            "/api/2.0/mlflow/runs/log-metric",
            new { });

        // Act
        var result = await _sut.LogMetricAsync(runId, key, value, CancellationToken.None);

        // Assert
        var paths = string.Join(", ", _mockHandler.ReceivedPaths);
        result.IfFail(err => throw new Exception($"Error: {err.Message}. Paths: [{paths}]"));
        result.IsSucc.Should().BeTrue();
    }

    [Fact]
    public async Task LogParamAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        const string runId = "run-456";
        const string key = "model";
        const string value = "gpt-4o";

        _mockHandler.SetupResponse(
            HttpMethod.Post,
            "/api/2.0/mlflow/runs/log-parameter",
            new { });

        // Act
        var result = await _sut.LogParamAsync(runId, key, value, CancellationToken.None);

        // Assert
        result.IsSucc.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateRunAsync_WithValidData_ReturnsUpdatedRun()
    {
        // Arrange
        const string runId = "run-456";
        const string newStatus = "FINISHED";

        _mockHandler.SetupResponse(
            HttpMethod.Post,
            "/api/2.0/mlflow/runs/update",
            new
            {
                run_info = new
                {
                    run_id = runId,
                    status = newStatus,
                    end_time = 1704070800000L
                }
            });

        // Act
        var result = await _sut.UpdateRunAsync(runId, newStatus, CancellationToken.None);

        // Assert
        result.IsSucc.Should().BeTrue();
        result.IfSucc(info => info.Status.Should().Be(newStatus));
    }

    [Fact]
    public async Task GetMetricHistoryAsync_WithValidData_ReturnsMetrics()
    {
        // Arrange
        const string runId = "run-456";
        const string metricKey = "loss";

        _mockHandler.SetupResponse(
            HttpMethod.Get,
            "/api/2.0/mlflow/metrics/get-history",
            new
            {
                metrics = new[]
                {
                    new { key = metricKey, value = 0.5, timestamp = 1704067200000L, step = 0 },
                    new { key = metricKey, value = 0.3, timestamp = 1704067260000L, step = 1 },
                    new { key = metricKey, value = 0.1, timestamp = 1704067320000L, step = 2 }
                }
            });

        // Act
        var result = await _sut.GetMetricHistoryAsync(runId, metricKey, CancellationToken.None);

        // Assert
        result.IsSucc.Should().BeTrue();
        result.IfSucc(metrics =>
        {
            metrics.Should().HaveCount(3);
            metrics[0].Value.Should().Be(0.5);
            metrics[2].Value.Should().Be(0.1);
        });
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<(HttpMethod Method, string Path), (HttpStatusCode StatusCode, string Content)> _responses = new();
    public List<string> ReceivedPaths { get; } = new();

    public void SetupResponse<T>(HttpMethod method, string path, T responseBody)
    {
        var json = JsonSerializer.Serialize(responseBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        _responses[(method, path)] = (HttpStatusCode.OK, json);
    }

    public void SetupErrorResponse(HttpMethod method, string path, HttpStatusCode statusCode, string errorCode, string message)
    {
        var json = JsonSerializer.Serialize(new { error_code = errorCode, message });
        _responses[(method, path)] = (statusCode, json);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        ReceivedPaths.Add($"{request.Method} {path}");
        var key = (request.Method, path);

        if (_responses.TryGetValue(key, out var response))
        {
            return Task.FromResult(new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(response.Content, System.Text.Encoding.UTF8, "application/json")
            });
        }

        var registeredKeys = string.Join(", ", _responses.Keys.Select(k => $"{k.Method} {k.Path}"));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"No mock for {request.Method} {path}. Registered: [{registeredKeys}]")
        });
    }
}
