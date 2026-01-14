using AbstractMatters.AgentFramework.Poc.Api.Models;
using AbstractMatters.AgentFramework.Poc.Application.Mlflow;
using AbstractMatters.AgentFramework.Poc.Infrastructure.Agents;
using Microsoft.AspNetCore.Mvc;

namespace AbstractMatters.AgentFramework.Poc.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowsController : ControllerBase
{
    private const string DemoExperimentName = "customer-support-triage";
    private readonly MafWorkflowService _workflowService;
    private readonly IMlflowClient _mlflowClient;
    private readonly ILogger<WorkflowsController> _logger;

    public WorkflowsController(
        MafWorkflowService workflowService,
        IMlflowClient mlflowClient,
        ILogger<WorkflowsController> logger)
    {
        ArgumentNullException.ThrowIfNull(workflowService);
        ArgumentNullException.ThrowIfNull(mlflowClient);
        ArgumentNullException.ThrowIfNull(logger);
        _workflowService = workflowService;
        _mlflowClient = mlflowClient;
        _logger = logger;
    }

    [HttpPost("execute/demo")]
    public async Task<IActionResult> ExecuteDemoWorkflow(
        [FromBody] DemoWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        // Define the multi-agent workflow using MAF
        // NOTE: In MAF sequential workflows, each agent's output becomes the next agent's input.
        // So agents must include context for downstream agents in their output.
        // The specialist name will be updated after classification based on the actual detected category.
        var agents = new List<AgentConfig>
        {
            new("Classifier", """
                You classify customer support tickets. Analyze the customer's message and determine the category.
                Categories: billing, technical, or general.

                IMPORTANT: Your response format must be:
                Category: [category]
                Customer Issue: [repeat the customer's original message]

                This format passes context to the next agent in the workflow.
                """),
            new("Router", """
                You route classified tickets to the appropriate specialist.
                You receive input in the format "Category: X, Customer Issue: Y".

                Based on the category, add the specialist assignment and pass along the customer issue.

                IMPORTANT: Your response format must be:
                Specialist: [billing-specialist/technical-support/general-support]
                Customer Issue: [the customer's original issue from input]

                This format passes context to the specialist agent.
                """),
            new("Specialist", GetSpecialistInstructions())
        };

        try
        {
            // Execute using MAF WorkflowBuilder with streaming
            var result = await _workflowService.ExecuteSequentialAsync(agents, request.Input, cancellationToken);

            // Log to MLflow (fire-and-forget)
            _ = LogToMlflowAsync(request.Input, result, CancellationToken.None);

            // Update the specialist name based on the actual classification result
            var responseResults = result.AgentResults
                .Select((ar, index) =>
                {
                    var agentName = ar.AgentName;
                    // For the specialist (3rd agent), derive name from classifier output
                    if (index == 2 && result.AgentResults.Count > 0)
                    {
                        agentName = GetSpecialistNameFromClassification(result.AgentResults[0].Output);
                    }
                    return new AgentStepResultResponse(
                        agentName,
                        ar.Input,
                        ar.Output,
                        ar.TokensUsed,
                        ar.ExecutionTimeMs);
                })
                .ToList();

            return Ok(new WorkflowResultResponse(
                result.FinalResponse,
                responseResults,
                result.TotalTokens,
                result.ExecutionTimeMs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow execution failed");
            return BadRequest(new ErrorResponse($"Workflow execution failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Demonstrates MAF agent with function tools (tool use pattern).
    /// The agent can look up customer account information using tools.
    /// </summary>
    [HttpPost("execute/tools-demo")]
    public async Task<IActionResult> ExecuteToolsDemo(
        [FromBody] DemoWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        const string instructions = """
            You are a helpful customer support agent with access to account lookup tools.
            When a customer asks about their account, use the available tools to look up their information.
            Always be helpful and provide accurate information based on the tool results.
            If you need an email address to look up information, ask the customer for it.

            Available test accounts:
            - john@example.com (Premium plan)
            - jane@example.com (Basic plan)
            """;

        try
        {
            var result = await _workflowService.ExecuteWithToolsAsync(
                instructions,
                request.Input,
                cancellationToken);

            return Ok(new ToolAgentResponse(
                result.Response,
                result.ToolCalls.Select(tc => new ToolCallResponse(tc.ToolName, tc.Arguments, tc.Result)).ToList(),
                result.TotalTokens,
                result.ExecutionTimeMs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool agent execution failed");
            return BadRequest(new ErrorResponse($"Tool agent execution failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Demonstrates AgentThread for conversation memory.
    /// Multiple messages to the same conversation ID maintain context.
    /// </summary>
    [HttpPost("conversation")]
    public async Task<IActionResult> SendConversationMessage(
        [FromBody] ConversationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _workflowService.ExecuteConversationAsync(
                request.ConversationId,
                request.Message,
                cancellationToken);

            return Ok(new ConversationResponse(
                result.Turns.Select(t => new ConversationTurnResponse(t.Role, t.Message)).ToList(),
                result.SerializedThread,
                result.TotalTokens,
                result.ExecutionTimeMs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Conversation execution failed");
            return BadRequest(new ErrorResponse($"Conversation failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Serializes a conversation thread for persistence.
    /// </summary>
    [HttpPost("conversation/{conversationId}/serialize")]
    public async Task<IActionResult> SerializeThread(string conversationId)
    {
        var serialized = await _workflowService.SerializeThreadAsync(conversationId);
        return Ok(new SerializeThreadResponse(serialized));
    }

    /// <summary>
    /// Resumes a conversation from serialized thread state.
    /// </summary>
    [HttpPost("conversation/resume")]
    public async Task<IActionResult> ResumeConversation(
        [FromBody] ResumeConversationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _workflowService.ResumeConversationAsync(
                request.ConversationId,
                request.SerializedThread,
                request.Message,
                cancellationToken);

            if (result == null)
            {
                return BadRequest(new ErrorResponse("Failed to resume conversation"));
            }

            return Ok(new ConversationResponse(
                result.Turns.Select(t => new ConversationTurnResponse(t.Role, t.Message)).ToList(),
                result.SerializedThread,
                result.TotalTokens,
                result.ExecutionTimeMs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resume conversation failed");
            return BadRequest(new ErrorResponse($"Resume failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Clears a conversation and frees resources.
    /// </summary>
    [HttpDelete("conversation/{conversationId}")]
    public IActionResult ClearConversation(string conversationId)
    {
        _workflowService.ClearConversation(conversationId);
        return NoContent();
    }

    /// <summary>
    /// Demonstrates agent handoff pattern using MAF's HandoffsWorkflowBuilder.
    /// </summary>
    [HttpPost("execute/handoff")]
    public async Task<IActionResult> ExecuteHandoff(
        [FromBody] DemoWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _workflowService.ExecuteHandoffAsync(
                request.Input,
                cancellationToken);

            return Ok(new HandoffResponse(
                result.Handoffs.Select(h => new HandoffEventResponse(h.FromAgent, h.ToAgent, h.Reason)).ToList(),
                result.FinalResponse,
                result.AgentResults.Select(r => new AgentStepResultResponse(
                    r.AgentName, r.Input, r.Output, r.TokensUsed, r.ExecutionTimeMs)).ToList(),
                result.TotalTokens,
                result.ExecutionTimeMs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handoff execution failed");
            return BadRequest(new ErrorResponse($"Handoff failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Demonstrates multi-agent group chat collaboration.
    /// </summary>
    [HttpPost("execute/group-chat")]
    public async Task<IActionResult> ExecuteGroupChat(
        [FromBody] GroupChatRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _workflowService.ExecuteGroupChatAsync(
                request.Topic,
                request.MaxTurns,
                cancellationToken);

            return Ok(new GroupChatResponse(
                result.Messages.Select(m => new GroupChatMessageResponse(
                    m.AgentName, m.Message, m.TurnNumber)).ToList(),
                result.FinalConsensus,
                result.TotalTokens,
                result.ExecutionTimeMs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Group chat execution failed");
            return BadRequest(new ErrorResponse($"Group chat failed: {ex.Message}"));
        }
    }

    private static string GetSpecialistInstructions()
    {
        return """
            You receive routed input that may include "Specialist:" and "Customer Issue:" labels.
            Extract the customer's actual issue and respond helpfully to solve their problem.
            Ignore routing metadata and respond directly to the customer's concern.

            You are a support specialist. Adapt your expertise based on the type of issue:
            - For billing issues: Help with payments, invoices, refunds. Be empathetic.
            - For technical issues: Help with bugs, errors, troubleshooting. Be methodical.
            - For general issues: Help with any questions. Be friendly and helpful.
            """;
    }

    private static string GetSpecialistNameFromClassification(string classifierOutput)
    {
        var lowerOutput = classifierOutput.ToLowerInvariant();

        if (lowerOutput.Contains("category: billing") || lowerOutput.Contains("category:billing"))
            return "BillingSpecialist";

        if (lowerOutput.Contains("category: technical") || lowerOutput.Contains("category:technical"))
            return "TechnicalSpecialist";

        return "GeneralSpecialist";
    }

    private async Task LogToMlflowAsync(
        string input,
        WorkflowResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get or create experiment
            var experimentResult = await _mlflowClient.GetExperimentByNameAsync(DemoExperimentName, cancellationToken);
            string experimentId;

            if (experimentResult.IsFail)
            {
                var createResult = await _mlflowClient.CreateExperimentAsync(DemoExperimentName, cancellationToken);
                if (createResult.IsFail)
                {
                    _logger.LogWarning("Failed to create MLflow experiment");
                    return;
                }
                experimentId = createResult.Match(
                    Succ: exp => exp.ExperimentId,
                    Fail: _ => throw new InvalidOperationException("Should not reach here"));
            }
            else
            {
                experimentId = experimentResult.Match(
                    Succ: exp => exp.ExperimentId,
                    Fail: _ => throw new InvalidOperationException("Should not reach here"));
            }

            _logger.LogInformation("Using MLflow experiment {ExperimentId}", experimentId);

            // Create run
            var runResult = await _mlflowClient.CreateRunAsync(experimentId, cancellationToken);
            if (runResult.IsFail)
            {
                _logger.LogWarning("Failed to start MLflow run");
                return;
            }

            var runId = runResult.Match(
                Succ: run => run.RunId,
                Fail: _ => throw new InvalidOperationException("Should not reach here"));

            // Log parameters
            await _mlflowClient.LogParamAsync(runId, "input_preview", input.Length > 100 ? input[..100] + "..." : input, cancellationToken);
            await _mlflowClient.LogParamAsync(runId, "agent_count", result.AgentResults.Count.ToString(), cancellationToken);

            // Log metrics
            await _mlflowClient.LogMetricAsync(runId, "total_tokens", result.TotalTokens, cancellationToken);
            await _mlflowClient.LogMetricAsync(runId, "total_execution_time_ms", result.ExecutionTimeMs, cancellationToken);

            // Log per-agent metrics
            foreach (var agentResult in result.AgentResults)
            {
                var agentKey = agentResult.AgentName.ToLowerInvariant().Replace(" ", "_");
                await _mlflowClient.LogMetricAsync(runId, $"{agentKey}_tokens", agentResult.TokensUsed, cancellationToken);
                await _mlflowClient.LogMetricAsync(runId, $"{agentKey}_execution_time_ms", agentResult.ExecutionTimeMs, cancellationToken);
            }

            await _mlflowClient.UpdateRunAsync(runId, "FINISHED", cancellationToken);
            _logger.LogInformation("Logged workflow execution to MLflow run {RunId}", runId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log to MLflow");
        }
    }
}
