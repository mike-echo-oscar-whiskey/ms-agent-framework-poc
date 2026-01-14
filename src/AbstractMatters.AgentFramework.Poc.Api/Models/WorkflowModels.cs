namespace AbstractMatters.AgentFramework.Poc.Api.Models;

public record DemoWorkflowRequest(string Input);

public record WorkflowResultResponse(
    string FinalResponse,
    List<AgentStepResultResponse> AgentResults,
    int TotalTokens,
    long ExecutionTimeMs);

public record AgentStepResultResponse(
    string AgentName,
    string Input,
    string Output,
    int TokensUsed,
    long ExecutionTimeMs);

public record ErrorResponse(string Message);

public record ToolCallResponse(
    string ToolName,
    string Arguments,
    string Result);

public record ToolAgentResponse(
    string Response,
    List<ToolCallResponse> ToolCalls,
    int TotalTokens,
    long ExecutionTimeMs);

// Conversation memory models
public record ConversationRequest(string ConversationId, string Message);
public record ResumeConversationRequest(string ConversationId, string SerializedThread, string Message);
public record ConversationTurnResponse(string Role, string Message);
public record ConversationResponse(
    List<ConversationTurnResponse> Turns,
    string? SerializedThread,
    int TotalTokens,
    long ExecutionTimeMs);
public record SerializeThreadResponse(string? SerializedThread);

// Handoff models
public record HandoffEventResponse(string FromAgent, string ToAgent, string Reason);
public record HandoffResponse(
    List<HandoffEventResponse> Handoffs,
    string FinalResponse,
    List<AgentStepResultResponse> AgentResults,
    int TotalTokens,
    long ExecutionTimeMs);

// Group chat models
public record GroupChatRequest(string Topic, int MaxTurns = 4);
public record GroupChatMessageResponse(string AgentName, string Message, int TurnNumber);
public record GroupChatResponse(
    List<GroupChatMessageResponse> Messages,
    string FinalConsensus,
    int TotalTokens,
    long ExecutionTimeMs);
