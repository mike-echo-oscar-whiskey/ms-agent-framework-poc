using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AbstractMatters.AgentFramework.Poc.Infrastructure.Agents;

public record AgentConfig(string Name, string Instructions);

public record WorkflowResult(
    string FinalResponse,
    List<AgentStepResult> AgentResults,
    int TotalTokens,
    long ExecutionTimeMs);

public record AgentStepResult(
    string AgentName,
    string Input,
    string Output,
    int TokensUsed,
    long ExecutionTimeMs);

public record ToolCallInfo(
    string ToolName,
    string Arguments,
    string Result);

public record ToolAgentResult(
    string Response,
    List<ToolCallInfo> ToolCalls,
    int TotalTokens,
    long ExecutionTimeMs);

// Thread/Conversation memory demo records
public record ConversationTurn(string Role, string Message);
public record ConversationResult(
    List<ConversationTurn> Turns,
    string? SerializedThread,
    int TotalTokens,
    long ExecutionTimeMs);

// Conditional routing demo records
public record RoutingDecision(string DetectedCategory, string SelectedAgent);
public record ConditionalRoutingResult(
    RoutingDecision Routing,
    string FinalResponse,
    List<AgentStepResult> AgentResults,
    int TotalTokens,
    long ExecutionTimeMs);

// Handoff demo records
public record HandoffEvent(string FromAgent, string ToAgent, string Reason);
public record HandoffResult(
    List<HandoffEvent> Handoffs,
    string FinalResponse,
    List<AgentStepResult> AgentResults,
    int TotalTokens,
    long ExecutionTimeMs);

// Group chat demo records
public record GroupChatMessage(string AgentName, string Message, int TurnNumber);
public record GroupChatResult(
    List<GroupChatMessage> Messages,
    string FinalConsensus,
    int TotalTokens,
    long ExecutionTimeMs);

public class MafWorkflowService
{
    private readonly IChatClient _chatClient;

    // Thread storage for conversation memory demo (in production, use Redis/database)
    private readonly ConcurrentDictionary<string, AgentThread> _threads = new();
    private readonly ConcurrentDictionary<string, List<ConversationTurn>> _conversationHistory = new();
    private readonly ConcurrentDictionary<string, string> _serializedThreads = new();

    public MafWorkflowService(IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        _chatClient = chatClient;
    }

    public async Task<WorkflowResult> ExecuteSequentialAsync(
        IEnumerable<AgentConfig> agentConfigs,
        string input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var agentResults = new List<AgentStepResult>();
        var configs = agentConfigs.ToList();

        // Create MAF agents using ChatClientAgent
        var agents = configs
            .Select(config => new ChatClientAgent(_chatClient, config.Instructions))
            .Cast<AIAgent>()
            .ToArray();

        // Build workflow with edges using MAF WorkflowBuilder
        var builder = new WorkflowBuilder(agents[0]);
        for (int i = 0; i < agents.Length - 1; i++)
        {
            builder.AddEdge(agents[i], agents[i + 1]);
        }
        var workflow = builder.Build();

        // Execute workflow with streaming to capture per-agent outputs
        var message = new ChatMessage(ChatRole.User, input);
        await using var run = await InProcessExecution.StreamAsync(workflow, message);

        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        int currentAgentIndex = 0;
        var currentOutput = new System.Text.StringBuilder();
        var agentStartTime = Stopwatch.StartNew();
        string lastExecutorId = "";

        await foreach (var evt in run.WatchStreamAsync().WithCancellation(cancellationToken))
        {
            if (evt is AgentRunUpdateEvent update)
            {
                var executorId = update.ExecutorId ?? "Unknown";

                // New agent started
                if (!string.IsNullOrEmpty(lastExecutorId) && lastExecutorId != executorId)
                {
                    var agentName = currentAgentIndex < configs.Count
                        ? configs[currentAgentIndex].Name
                        : lastExecutorId;

                    agentResults.Add(new AgentStepResult(
                        AgentName: agentName,
                        Input: agentResults.Count == 0 ? input : agentResults[^1].Output,
                        Output: currentOutput.ToString(),
                        TokensUsed: EstimateTokens(currentOutput.ToString()),
                        ExecutionTimeMs: agentStartTime.ElapsedMilliseconds));

                    currentOutput.Clear();
                    agentStartTime.Restart();
                    currentAgentIndex++;
                }

                lastExecutorId = executorId;

                // Extract text from update data
                var text = update.Data?.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    currentOutput.Append(text);
                }
            }
        }

        // Add the last agent's result
        if (!string.IsNullOrEmpty(lastExecutorId) && currentOutput.Length > 0)
        {
            var agentName = currentAgentIndex < configs.Count
                ? configs[currentAgentIndex].Name
                : lastExecutorId;

            agentResults.Add(new AgentStepResult(
                AgentName: agentName,
                Input: agentResults.Count == 0 ? input : agentResults[^1].Output,
                Output: currentOutput.ToString(),
                TokensUsed: EstimateTokens(currentOutput.ToString()),
                ExecutionTimeMs: agentStartTime.ElapsedMilliseconds));
        }

        stopwatch.Stop();

        var finalResponse = agentResults.Count > 0 ? agentResults[^1].Output : string.Empty;
        var totalTokens = agentResults.Sum(r => r.TokensUsed);

        return new WorkflowResult(
            FinalResponse: finalResponse,
            AgentResults: agentResults,
            TotalTokens: totalTokens,
            ExecutionTimeMs: stopwatch.ElapsedMilliseconds);
    }

    public async Task<ToolAgentResult> ExecuteWithToolsAsync(
        string instructions,
        string input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var toolCalls = new List<ToolCallInfo>();

        // Define tools using AIFunctionFactory
        var tools = new AIFunction[]
        {
            AIFunctionFactory.Create(GetAccountInfo),
            AIFunctionFactory.Create(GetAccountBalance),
            AIFunctionFactory.Create(GetRecentTransactions)
        };

        // Create agent with tools using MAF's CreateAIAgent extension
        var agent = _chatClient.CreateAIAgent(
            instructions: instructions,
            tools: tools);

        // Execute agent with streaming
        var response = new System.Text.StringBuilder();
        await foreach (var update in agent.RunStreamingAsync(
            input,
            thread: null,
            options: null,
            cancellationToken: cancellationToken))
        {
            if (update.Text != null)
            {
                response.Append(update.Text);
            }
        }

        stopwatch.Stop();

        return new ToolAgentResult(
            Response: response.ToString(),
            ToolCalls: toolCalls,
            TotalTokens: EstimateTokens(response.ToString()),
            ExecutionTimeMs: stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Demonstrates AgentThread for conversation memory (multi-turn conversations).
    /// The agent remembers context from previous messages in the same conversation.
    /// </summary>
    public async Task<ConversationResult> ExecuteConversationAsync(
        string conversationId,
        string message,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Create agent with memory-aware instructions
        var agent = _chatClient.CreateAIAgent(
            instructions: """
                You are a helpful assistant with excellent memory.
                Remember details from our conversation and reference them naturally.
                If the user mentions their name, remember it for later.
                If they ask about previous topics, recall and reference them.
                Be conversational and show that you remember our chat history.
                """);

        // Get or create thread for this conversation
        if (!_threads.TryGetValue(conversationId, out var thread))
        {
            thread = agent.GetNewThread();
            _threads[conversationId] = thread;
            _conversationHistory[conversationId] = [];
        }

        // Add user message to history
        _conversationHistory[conversationId].Add(new ConversationTurn("user", message));

        // Execute with thread for memory
        var response = new System.Text.StringBuilder();
        await foreach (var update in agent.RunStreamingAsync(
            message,
            thread: thread,
            options: null,
            cancellationToken: cancellationToken))
        {
            if (update.Text != null)
            {
                response.Append(update.Text);
            }
        }

        // Add assistant response to history
        var assistantMessage = response.ToString();
        _conversationHistory[conversationId].Add(new ConversationTurn("assistant", assistantMessage));

        stopwatch.Stop();

        return new ConversationResult(
            Turns: _conversationHistory[conversationId].ToList(),
            SerializedThread: null,
            TotalTokens: EstimateTokens(assistantMessage),
            ExecutionTimeMs: stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Serializes a conversation thread to JSON for persistence.
    /// </summary>
    public Task<string?> SerializeThreadAsync(string conversationId)
    {
        if (!_threads.TryGetValue(conversationId, out var thread))
        {
            return Task.FromResult<string?>(null);
        }

        var serialized = thread.Serialize();
        var json = JsonSerializer.Serialize(serialized);
        _serializedThreads[conversationId] = json;

        return Task.FromResult<string?>(json);
    }

    /// <summary>
    /// Restores a conversation from serialized thread state.
    /// </summary>
    public async Task<ConversationResult?> ResumeConversationAsync(
        string conversationId,
        string serializedThread,
        string message,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Create agent
        var agent = _chatClient.CreateAIAgent(
            instructions: """
                You are a helpful assistant with excellent memory.
                Remember details from our conversation and reference them naturally.
                If the user mentions their name, remember it for later.
                If they ask about previous topics, recall and reference them.
                Be conversational and show that you remember our chat history.
                """);

        // Deserialize thread
        var threadData = JsonSerializer.Deserialize<JsonElement>(serializedThread);
        var thread = agent.DeserializeThread(threadData);
        _threads[conversationId] = thread;

        // Initialize or restore history
        if (!_conversationHistory.ContainsKey(conversationId))
        {
            _conversationHistory[conversationId] = [new ConversationTurn("system", "[Conversation resumed from saved state]")];
        }

        // Add user message
        _conversationHistory[conversationId].Add(new ConversationTurn("user", message));

        // Execute with restored thread
        var response = new System.Text.StringBuilder();
        await foreach (var update in agent.RunStreamingAsync(
            message,
            thread: thread,
            options: null,
            cancellationToken: cancellationToken))
        {
            if (update.Text != null)
            {
                response.Append(update.Text);
            }
        }

        var assistantMessage = response.ToString();
        _conversationHistory[conversationId].Add(new ConversationTurn("assistant", assistantMessage));

        stopwatch.Stop();

        return new ConversationResult(
            Turns: _conversationHistory[conversationId].ToList(),
            SerializedThread: serializedThread,
            TotalTokens: EstimateTokens(assistantMessage),
            ExecutionTimeMs: stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Clears a conversation and its thread.
    /// </summary>
    public void ClearConversation(string conversationId)
    {
        _threads.TryRemove(conversationId, out _);
        _conversationHistory.TryRemove(conversationId, out _);
        _serializedThreads.TryRemove(conversationId, out _);
    }

    /// <summary>
    /// Demonstrates conditional routing using MAF's HandoffsWorkflowBuilder.
    /// The classifier agent determines the category and hands off to the appropriate specialist.
    /// This is implemented as a handoff pattern where routing is determined by the agent.
    /// </summary>
    public async Task<ConditionalRoutingResult> ExecuteConditionalRoutingAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var agentResults = new List<AgentStepResult>();
        string detectedCategory = "GENERAL";
        string selectedAgent = "GeneralSupport";

        // Create classifier agent that will hand off to specialists
        var classifierAgent = new ChatClientAgent(
            _chatClient,
            instructions: """
            You are a classifier and router. Analyze the user's message and hand off to the appropriate specialist:
            - Use handoff to BillingSpecialist for payment, invoice, subscription, refund issues
            - Use handoff to TechnicalSupport for bugs, errors, technical problems, how-to questions
            - Use handoff to SalesAdvisor for pricing, upgrades, new features, purchases
            - Use handoff to GeneralSupport for anything else

            Always hand off to a specialist - do not respond directly.
            """,
            name: "Classifier",
            description: "Classifies and routes customer requests to appropriate specialists");

        var billingAgent = new ChatClientAgent(
            _chatClient,
            instructions: "You are a billing specialist. Help with payments, invoices, subscriptions, and refunds. Be empathetic about billing concerns.",
            name: "BillingSpecialist",
            description: "Handles payment, invoice, subscription, and refund issues");

        var technicalAgent = new ChatClientAgent(
            _chatClient,
            instructions: "You are a technical support expert. Help diagnose and solve technical issues. Be methodical and clear.",
            name: "TechnicalSupport",
            description: "Handles bugs, errors, technical problems, and how-to questions");

        var salesAgent = new ChatClientAgent(
            _chatClient,
            instructions: "You are a sales advisor. Help with pricing questions, upgrades, and purchasing decisions. Be helpful but not pushy.",
            name: "SalesAdvisor",
            description: "Handles pricing, upgrades, new features, and purchases");

        var generalAgent = new ChatClientAgent(
            _chatClient,
            instructions: "You are a general support agent. Help with any questions and be friendly and helpful.",
            name: "GeneralSupport",
            description: "Handles general inquiries and questions");

        // Build handoff workflow - classifier can hand off to any specialist
        var workflow = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(classifierAgent)
            .WithHandoffs(classifierAgent, [billingAgent, technicalAgent, salesAgent, generalAgent])
            .Build();

        // Execute the workflow with streaming
        var message = new ChatMessage(ChatRole.User, input);
        await using var run = await InProcessExecution.StreamAsync(workflow, message);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        string lastExecutorId = "";
        var currentOutput = new System.Text.StringBuilder();
        var agentStartTime = Stopwatch.StartNew();

        await foreach (var evt in run.WatchStreamAsync().WithCancellation(cancellationToken))
        {
            if (evt is AgentRunUpdateEvent update)
            {
                var executorId = update.ExecutorId ?? "Unknown";

                // Detect agent change (handoff/routing occurred)
                if (!string.IsNullOrEmpty(lastExecutorId) && lastExecutorId != executorId)
                {
                    // Record the classifier's result
                    agentResults.Add(new AgentStepResult(
                        AgentName: lastExecutorId,
                        Input: input,
                        Output: currentOutput.ToString(),
                        TokensUsed: EstimateTokens(currentOutput.ToString()),
                        ExecutionTimeMs: agentStartTime.ElapsedMilliseconds));

                    // Determine category based on which specialist was selected
                    selectedAgent = executorId;
                    detectedCategory = executorId switch
                    {
                        "BillingSpecialist" => "BILLING",
                        "TechnicalSupport" => "TECHNICAL",
                        "SalesAdvisor" => "SALES",
                        _ => "GENERAL"
                    };

                    currentOutput.Clear();
                    agentStartTime.Restart();
                }

                lastExecutorId = executorId;

                var text = update.Data?.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    currentOutput.Append(text);
                }
            }
        }

        // Add the final agent's result
        if (!string.IsNullOrEmpty(lastExecutorId) && currentOutput.Length > 0)
        {
            agentResults.Add(new AgentStepResult(
                AgentName: lastExecutorId,
                Input: input,
                Output: currentOutput.ToString(),
                TokensUsed: EstimateTokens(currentOutput.ToString()),
                ExecutionTimeMs: agentStartTime.ElapsedMilliseconds));

            // If only one agent responded, it was the specialist
            if (agentResults.Count == 1)
            {
                selectedAgent = lastExecutorId;
                detectedCategory = lastExecutorId switch
                {
                    "BillingSpecialist" => "BILLING",
                    "TechnicalSupport" => "TECHNICAL",
                    "SalesAdvisor" => "SALES",
                    _ => "GENERAL"
                };
            }
        }

        stopwatch.Stop();

        var finalResponse = agentResults.Count > 0 ? agentResults[^1].Output : string.Empty;

        return new ConditionalRoutingResult(
            Routing: new RoutingDecision(detectedCategory, selectedAgent),
            FinalResponse: finalResponse,
            AgentResults: agentResults,
            TotalTokens: agentResults.Sum(r => r.TokensUsed),
            ExecutionTimeMs: stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Demonstrates agent handoff pattern using MAF's HandoffsWorkflowBuilder.
    /// The triage agent can hand off to specialist agents based on customer needs.
    /// MAF automatically provides handoff tools to agents based on the workflow configuration.
    /// </summary>
    public async Task<HandoffResult> ExecuteHandoffAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var agentResults = new List<AgentStepResult>();
        var handoffs = new List<HandoffEvent>();

        // Create specialist agents with descriptions (MAF uses descriptions for handoff tool generation)
        var triageAgent = new ChatClientAgent(
            _chatClient,
            instructions: """
            You are a triage agent. Analyze the customer's request and determine the best course of action.
            If you can handle simple questions yourself, do so. Otherwise, hand off to the appropriate specialist.
            """,
            name: "TriageAgent",
            description: "Initial triage and routing of customer requests");

        var billingAgent = new ChatClientAgent(
            _chatClient,
            instructions: "You are a billing specialist. Help with payments, invoices, subscriptions, and refunds. Be empathetic about billing concerns.",
            name: "BillingSpecialist",
            description: "Handles payment, invoice, subscription, and refund issues");

        var technicalAgent = new ChatClientAgent(
            _chatClient,
            instructions: "You are a technical specialist. Help diagnose and solve technical issues. Be methodical and provide clear troubleshooting steps.",
            name: "TechnicalSpecialist",
            description: "Handles bugs, errors, and technical problems");

        var accountAgent = new ChatClientAgent(
            _chatClient,
            instructions: "You are an account specialist. Help with account settings, profile changes, and access issues. Be security-conscious.",
            name: "AccountSpecialist",
            description: "Handles account settings, profile, and access issues");

        // Build handoff workflow using MAF's HandoffsWorkflowBuilder
        // MAF automatically creates handoff tools based on agent descriptions
        var workflow = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(triageAgent)
            .WithHandoffs(triageAgent, [billingAgent, technicalAgent, accountAgent])
            .Build();

        // Execute the workflow with streaming
        var message = new ChatMessage(ChatRole.User, input);
        await using var run = await InProcessExecution.StreamAsync(workflow, message);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        string currentAgentName = "TriageAgent";
        string lastAgentName = "";
        var currentOutput = new System.Text.StringBuilder();
        var agentStartTime = Stopwatch.StartNew();

        await foreach (var evt in run.WatchStreamAsync().WithCancellation(cancellationToken))
        {
            if (evt is AgentRunUpdateEvent update)
            {
                var executorId = update.ExecutorId ?? "Unknown";

                // Detect agent change (handoff occurred)
                if (!string.IsNullOrEmpty(lastAgentName) && lastAgentName != executorId)
                {
                    // Record the previous agent's result
                    agentResults.Add(new AgentStepResult(
                        AgentName: lastAgentName,
                        Input: agentResults.Count == 0 ? input : "Handoff from previous agent",
                        Output: currentOutput.ToString(),
                        TokensUsed: EstimateTokens(currentOutput.ToString()),
                        ExecutionTimeMs: agentStartTime.ElapsedMilliseconds));

                    // Record the handoff event
                    handoffs.Add(new HandoffEvent(lastAgentName, executorId, "Routed by MAF handoff workflow"));

                    currentOutput.Clear();
                    agentStartTime.Restart();
                    currentAgentName = executorId;
                }

                lastAgentName = executorId;

                var text = update.Data?.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    currentOutput.Append(text);
                }
            }
        }

        // Add the final agent's result
        if (!string.IsNullOrEmpty(lastAgentName) && currentOutput.Length > 0)
        {
            agentResults.Add(new AgentStepResult(
                AgentName: lastAgentName,
                Input: agentResults.Count == 0 ? input : "Handoff from previous agent",
                Output: currentOutput.ToString(),
                TokensUsed: EstimateTokens(currentOutput.ToString()),
                ExecutionTimeMs: agentStartTime.ElapsedMilliseconds));
        }

        stopwatch.Stop();

        var finalResponse = agentResults.Count > 0 ? agentResults[^1].Output : string.Empty;

        return new HandoffResult(
            Handoffs: handoffs,
            FinalResponse: finalResponse,
            AgentResults: agentResults,
            TotalTokens: agentResults.Sum(r => r.TokensUsed),
            ExecutionTimeMs: stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Demonstrates multi-agent group chat using MAF's GroupChatWorkflowBuilder.
    /// Uses RoundRobinGroupChatManager for turn-based discussion with termination condition.
    /// </summary>
    public async Task<GroupChatResult> ExecuteGroupChatAsync(
        string topic,
        int maxTurns = 4,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var messages = new List<GroupChatMessage>();
        var totalTokens = 0;

        // Create participating agents using MAF's ChatClientAgent with names and descriptions
        var productManager = new ChatClientAgent(
            _chatClient,
            instructions: $"You are a Product Manager in a group discussion about: {topic}. Focus on user needs, market fit, and business value. Be concise (2-3 sentences). Build on others' ideas.",
            name: "ProductManager",
            description: "Focuses on user needs, market fit, and business value");

        var techLead = new ChatClientAgent(
            _chatClient,
            instructions: $"You are a Tech Lead in a group discussion about: {topic}. Focus on technical feasibility, architecture, and implementation. Be practical (2-3 sentences).",
            name: "TechLead",
            description: "Focuses on technical feasibility, architecture, and implementation");

        var designer = new ChatClientAgent(
            _chatClient,
            instructions: $"You are a UX Designer in a group discussion about: {topic}. Focus on user experience, usability, and design principles. Advocate for users (2-3 sentences).",
            name: "Designer",
            description: "Focuses on user experience, usability, and design principles");

        var qaEngineer = new ChatClientAgent(
            _chatClient,
            instructions: $"You are a QA Engineer in a group discussion about: {topic}. Focus on quality, edge cases, and potential issues. Think about what could go wrong (2-3 sentences).",
            name: "QAEngineer",
            description: "Focuses on quality, edge cases, and potential issues");

        var participants = new AIAgent[] { productManager, techLead, designer, qaEngineer };

        // Build group chat workflow using MAF's GroupChatWorkflowBuilder with RoundRobinGroupChatManager
        var workflow = AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(agents => new RoundRobinGroupChatManager(agents, TerminateAfterTurns(maxTurns)))
            .AddParticipants(participants)
            .Build();

        // Execute the workflow with streaming
        var message = new ChatMessage(ChatRole.User, $"Topic for discussion: {topic}");
        await using var run = await InProcessExecution.StreamAsync(workflow, message);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        string lastExecutorId = "";
        var currentOutput = new System.Text.StringBuilder();
        int turnNumber = 0;

        await foreach (var evt in run.WatchStreamAsync().WithCancellation(cancellationToken))
        {
            if (evt is AgentRunUpdateEvent update)
            {
                var executorId = update.ExecutorId ?? "Unknown";

                // New agent started speaking
                if (!string.IsNullOrEmpty(lastExecutorId) && lastExecutorId != executorId)
                {
                    turnNumber++;
                    var agentMessage = currentOutput.ToString();
                    messages.Add(new GroupChatMessage(lastExecutorId, agentMessage, turnNumber));
                    totalTokens += EstimateTokens(agentMessage);
                    currentOutput.Clear();
                }

                lastExecutorId = executorId;

                var text = update.Data?.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    currentOutput.Append(text);
                }
            }
        }

        // Add the last agent's message
        if (!string.IsNullOrEmpty(lastExecutorId) && currentOutput.Length > 0)
        {
            turnNumber++;
            var agentMessage = currentOutput.ToString();
            messages.Add(new GroupChatMessage(lastExecutorId, agentMessage, turnNumber));
            totalTokens += EstimateTokens(agentMessage);
        }

        // Final: Moderator synthesizes consensus
        var moderator = new ChatClientAgent(
            _chatClient,
            instructions: "You are a moderator. Synthesize the discussion into key decisions, consensus points, and any remaining open questions. Be structured and actionable.",
            name: "Moderator");

        var discussionSummary = string.Join("\n", messages.Select(m => $"{m.AgentName}: {m.Message}"));
        var consensusOutput = new System.Text.StringBuilder();

        await foreach (var update in moderator.RunStreamingAsync(
            $"Discussion complete. Please synthesize:\n\n{discussionSummary}",
            thread: null,
            options: null,
            cancellationToken: cancellationToken))
        {
            if (update.Text != null)
            {
                consensusOutput.Append(update.Text);
            }
        }

        totalTokens += EstimateTokens(consensusOutput.ToString());
        stopwatch.Stop();

        return new GroupChatResult(
            Messages: messages,
            FinalConsensus: consensusOutput.ToString(),
            TotalTokens: totalTokens,
            ExecutionTimeMs: stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Creates a termination condition that stops after a specified number of turns.
    /// </summary>
    private static Func<RoundRobinGroupChatManager, IEnumerable<ChatMessage>, CancellationToken, ValueTask<bool>> TerminateAfterTurns(int maxTurns)
    {
        return (manager, _, _) => ValueTask.FromResult(manager.IterationCount >= maxTurns);
    }

    [Description("Get customer account information by email address")]
    private static string GetAccountInfo(
        [Description("Customer email address")] string email)
    {
        // Simulated account lookup
        return email.ToLowerInvariant() switch
        {
            "john@example.com" => "Account: John Doe, Plan: Premium, Status: Active, Member since: 2022-03-15",
            "jane@example.com" => "Account: Jane Smith, Plan: Basic, Status: Active, Member since: 2023-01-20",
            _ => $"Account not found for email: {email}"
        };
    }

    [Description("Get the current balance for a customer account")]
    private static string GetAccountBalance(
        [Description("Customer email address")] string email)
    {
        // Simulated balance lookup
        return email.ToLowerInvariant() switch
        {
            "john@example.com" => "Current balance: €125.50, Next billing date: 2026-02-01",
            "jane@example.com" => "Current balance: €0.00, Next billing date: 2026-02-15",
            _ => $"Balance not found for email: {email}"
        };
    }

    [Description("Get recent transactions for a customer account")]
    private static string GetRecentTransactions(
        [Description("Customer email address")] string email,
        [Description("Number of transactions to retrieve")] int count = 3)
    {
        // Simulated transaction history
        return email.ToLowerInvariant() switch
        {
            "john@example.com" => $"Last {count} transactions for John Doe:\n" +
                "- 2026-01-10: Premium subscription renewal - €49.99\n" +
                "- 2026-01-05: Add-on purchase - €15.00\n" +
                "- 2025-12-10: Premium subscription renewal - €49.99",
            "jane@example.com" => $"Last {count} transactions for Jane Smith:\n" +
                "- 2026-01-15: Basic subscription renewal - €9.99\n" +
                "- 2025-12-15: Basic subscription renewal - €9.99\n" +
                "- 2025-11-15: Basic subscription renewal - €9.99",
            _ => $"No transactions found for email: {email}"
        };
    }

    private static int EstimateTokens(string text)
    {
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}
