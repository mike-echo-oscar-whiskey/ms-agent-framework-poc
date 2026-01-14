import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface DemoWorkflowRequest {
  input: string;
}

export interface AgentStepResultResponse {
  agentName: string;
  input: string;
  output: string;
  tokensUsed: number;
  executionTimeMs: number;
}

export interface WorkflowResultResponse {
  finalResponse: string;
  agentResults: AgentStepResultResponse[];
  totalTokens: number;
  executionTimeMs: number;
}

export interface ToolCallResponse {
  toolName: string;
  arguments: string;
  result: string;
}

export interface ToolAgentResponse {
  response: string;
  toolCalls: ToolCallResponse[];
  totalTokens: number;
  executionTimeMs: number;
}

// Conversation memory interfaces
export interface ConversationRequest {
  conversationId: string;
  message: string;
}

export interface ResumeConversationRequest {
  conversationId: string;
  serializedThread: string;
  message: string;
}

export interface ConversationTurnResponse {
  role: string;
  message: string;
}

export interface ConversationResponse {
  turns: ConversationTurnResponse[];
  serializedThread: string | null;
  totalTokens: number;
  executionTimeMs: number;
}

export interface SerializeThreadResponse {
  serializedThread: string | null;
}

// Handoff interfaces
export interface HandoffEventResponse {
  fromAgent: string;
  toAgent: string;
  reason: string;
}

export interface HandoffResponse {
  handoffs: HandoffEventResponse[];
  finalResponse: string;
  agentResults: AgentStepResultResponse[];
  totalTokens: number;
  executionTimeMs: number;
}

// Group chat interfaces
export interface GroupChatRequest {
  topic: string;
  maxTurns: number;
}

export interface GroupChatMessageResponse {
  agentName: string;
  message: string;
  turnNumber: number;
}

export interface GroupChatResponse {
  messages: GroupChatMessageResponse[];
  finalConsensus: string;
  totalTokens: number;
  executionTimeMs: number;
}

@Injectable({
  providedIn: 'root',
})
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = 'http://localhost:5139/api';

  executeDemoWorkflow(request: DemoWorkflowRequest): Observable<WorkflowResultResponse> {
    return this.http.post<WorkflowResultResponse>(
      `${this.baseUrl}/workflows/execute/demo`,
      request
    );
  }

  executeToolsDemo(request: DemoWorkflowRequest): Observable<ToolAgentResponse> {
    return this.http.post<ToolAgentResponse>(
      `${this.baseUrl}/workflows/execute/tools-demo`,
      request
    );
  }

  // Conversation memory endpoints
  sendConversationMessage(request: ConversationRequest): Observable<ConversationResponse> {
    return this.http.post<ConversationResponse>(
      `${this.baseUrl}/workflows/conversation`,
      request
    );
  }

  serializeThread(conversationId: string): Observable<SerializeThreadResponse> {
    return this.http.post<SerializeThreadResponse>(
      `${this.baseUrl}/workflows/conversation/${conversationId}/serialize`,
      {}
    );
  }

  resumeConversation(request: ResumeConversationRequest): Observable<ConversationResponse> {
    return this.http.post<ConversationResponse>(
      `${this.baseUrl}/workflows/conversation/resume`,
      request
    );
  }

  clearConversation(conversationId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.baseUrl}/workflows/conversation/${conversationId}`
    );
  }

  // Handoff endpoint
  executeHandoff(request: DemoWorkflowRequest): Observable<HandoffResponse> {
    return this.http.post<HandoffResponse>(
      `${this.baseUrl}/workflows/execute/handoff`,
      request
    );
  }

  // Group chat endpoint
  executeGroupChat(request: GroupChatRequest): Observable<GroupChatResponse> {
    return this.http.post<GroupChatResponse>(
      `${this.baseUrl}/workflows/execute/group-chat`,
      request
    );
  }
}
