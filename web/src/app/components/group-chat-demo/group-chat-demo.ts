import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatSliderModule } from '@angular/material/slider';
import { ApiService, GroupChatResponse } from '../../services/api.service';

@Component({
  selector: 'app-group-chat-demo',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatIconModule,
    MatSliderModule,
  ],
  templateUrl: './group-chat-demo.html',
  styleUrl: './group-chat-demo.scss',
})
export class GroupChatDemo {
  private readonly api = inject(ApiService);

  protected readonly topic = signal('Should we build a mobile app for our product?');
  protected readonly maxTurns = signal(4);
  protected readonly isLoading = signal(false);
  protected readonly result = signal<GroupChatResponse | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly exampleTopics = [
    'Should we build a mobile app for our product?',
    'How can we improve user onboarding?',
    'What features should we prioritize for Q2?',
    'Should we migrate to microservices?',
  ];

  protected readonly agentIcons: Record<string, string> = {
    'ProductManager': 'person',
    'TechLead': 'code',
    'Designer': 'palette',
    'QAEngineer': 'bug_report',
  };

  protected readonly agentColors: Record<string, string> = {
    'ProductManager': 'primary',
    'TechLead': 'tertiary',
    'Designer': 'secondary',
    'QAEngineer': 'error',
  };

  setExample(example: string): void {
    this.topic.set(example);
  }

  executeGroupChat(): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.result.set(null);

    this.api.executeGroupChat({
      topic: this.topic(),
      maxTurns: this.maxTurns()
    }).subscribe({
      next: (response) => {
        this.result.set(response);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || err.message || 'An error occurred');
        this.isLoading.set(false);
      },
    });
  }

  getAgentIcon(agentName: string): string {
    return this.agentIcons[agentName] || 'smart_toy';
  }

  getAgentColorClass(agentName: string): string {
    return `agent-${this.agentColors[agentName] || 'primary'}`;
  }
}
