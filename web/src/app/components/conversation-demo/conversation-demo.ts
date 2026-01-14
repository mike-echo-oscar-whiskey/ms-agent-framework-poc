import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { ApiService, ConversationTurnResponse } from '../../services/api.service';

@Component({
  selector: 'app-conversation-demo',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatIconModule,
    MatListModule,
  ],
  templateUrl: './conversation-demo.html',
  styleUrl: './conversation-demo.scss',
})
export class ConversationDemo {
  private readonly api = inject(ApiService);

  protected readonly conversationId = signal(`conv-${Date.now()}`);
  protected readonly message = signal('Hi! My name is Alice and I love hiking.');
  protected readonly isLoading = signal(false);
  protected readonly turns = signal<ConversationTurnResponse[]>([]);
  protected readonly serializedThread = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly executionTimeMs = signal(0);

  protected readonly exampleMessages = [
    'Hi! My name is Alice and I love hiking.',
    'What was my name again?',
    'What hobby did I mention?',
    'Can you summarize what you know about me?',
  ];

  setExample(example: string): void {
    this.message.set(example);
  }

  sendMessage(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.api.sendConversationMessage({
      conversationId: this.conversationId(),
      message: this.message()
    }).subscribe({
      next: (response) => {
        this.turns.set(response.turns);
        this.executionTimeMs.set(response.executionTimeMs);
        this.message.set('');
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || err.message || 'An error occurred');
        this.isLoading.set(false);
      },
    });
  }

  serializeThread(): void {
    this.api.serializeThread(this.conversationId()).subscribe({
      next: (response) => {
        this.serializedThread.set(response.serializedThread);
      },
      error: (err) => {
        this.error.set(err.error?.message || err.message || 'Failed to serialize thread');
      },
    });
  }

  clearConversation(): void {
    this.api.clearConversation(this.conversationId()).subscribe({
      next: () => {
        this.conversationId.set(`conv-${Date.now()}`);
        this.turns.set([]);
        this.serializedThread.set(null);
        this.error.set(null);
      },
      error: (err) => {
        this.error.set(err.error?.message || err.message || 'Failed to clear conversation');
      },
    });
  }
}
