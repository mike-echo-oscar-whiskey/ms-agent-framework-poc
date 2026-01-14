using AbstractMatters.AgentFramework.Poc.Application.Mlflow;
using AbstractMatters.AgentFramework.Poc.Infrastructure.Agents;
using AbstractMatters.AgentFramework.Poc.Infrastructure.Mlflow;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers();

// Add OpenAPI/Swagger
builder.Services.AddOpenApi();

// Configure OpenTelemetry tracing
// Jaeger is used for distributed tracing visualization (spans, latency, call hierarchy)
// MLflow is used for experiment tracking (metrics, parameters, model comparison via API)
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "AbstractMatters.AgentFramework.Poc.Api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://localhost:4317");
        }));

// Configure CORS for Angular frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register HttpClient for MLflow
builder.Services.AddHttpClient<IMlflowClient, MlflowClient>(client =>
{
    var mlflowBaseUrl = builder.Configuration.GetValue<string>("MLflow:BaseUrl") ?? "http://localhost:5000";
    client.BaseAddress = new Uri(mlflowBaseUrl);
});

// Register Azure OpenAI chat client for MAF
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["AzureOpenAI:Endpoint"]
        ?? throw new InvalidOperationException("AzureOpenAI:Endpoint configuration is missing");
    var apiKey = config["AzureOpenAI:ApiKey"]
        ?? throw new InvalidOperationException("AzureOpenAI:ApiKey configuration is missing");
    var deploymentName = config["AzureOpenAI:DeploymentName"]
        ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName configuration is missing");

    var azureClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new AzureKeyCredential(apiKey));

    return azureClient.GetChatClient(deploymentName).AsIChatClient();
});

// Register MAF workflow service as singleton to preserve conversation state across requests
builder.Services.AddSingleton<MafWorkflowService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngular");
app.MapControllers();

app.Run();
