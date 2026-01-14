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
import { ApiService, ToolAgentResponse } from '../../services/api.service';

@Component({
  selector: 'app-tools-demo',
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
  templateUrl: './tools-demo.html',
  styleUrl: './tools-demo.scss',
})
export class ToolsDemo {
  private readonly api = inject(ApiService);

  protected readonly input = signal('Can you look up my account details? My email is john@example.com');
  protected readonly isLoading = signal(false);
  protected readonly result = signal<ToolAgentResponse | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly exampleInputs = [
    'Can you look up my account details? My email is john@example.com',
    'What is my current balance? Email: jane@example.com',
    'Show me my recent transactions. My email is john@example.com',
    'I need my account info and balance. Email: jane@example.com',
  ];

  setExample(example: string): void {
    this.input.set(example);
  }

  async executeToolsDemo(): Promise<void> {
    this.isLoading.set(true);
    this.error.set(null);
    this.result.set(null);

    this.api.executeToolsDemo({ input: this.input() }).subscribe({
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
}
