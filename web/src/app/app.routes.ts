import { Routes } from '@angular/router';
import { WorkflowDemo } from './components/workflow-demo/workflow-demo';
import { ToolsDemo } from './components/tools-demo/tools-demo';
import { ConversationDemo } from './components/conversation-demo/conversation-demo';
import { ConditionalRoutingDemo } from './components/conditional-routing-demo/conditional-routing-demo';
import { HandoffDemo } from './components/handoff-demo/handoff-demo';
import { GroupChatDemo } from './components/group-chat-demo/group-chat-demo';

export const routes: Routes = [
  { path: '', redirectTo: 'workflow-demo', pathMatch: 'full' },
  { path: 'workflow-demo', component: WorkflowDemo },
  { path: 'tools-demo', component: ToolsDemo },
  { path: 'conversation-demo', component: ConversationDemo },
  { path: 'conditional-routing-demo', component: ConditionalRoutingDemo },
  { path: 'handoff-demo', component: HandoffDemo },
  { path: 'group-chat-demo', component: GroupChatDemo },
];
