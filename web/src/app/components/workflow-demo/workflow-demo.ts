import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { ApiService, WorkflowResultResponse } from '../../services/api.service';

@Component({
  selector: 'app-workflow-demo',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatExpansionModule,
    MatChipsModule,
    MatIconModule,
  ],
  templateUrl: './workflow-demo.html',
  styleUrl: './workflow-demo.scss',
})
export class WorkflowDemo {
  private readonly api = inject(ApiService);

  protected readonly input = signal('I was charged twice for my subscription last month.');
  protected readonly isLoading = signal(false);
  protected readonly result = signal<WorkflowResultResponse | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly exampleInputs = [
    'I was charged twice for my subscription last month.',
    'My application keeps crashing when I try to upload files.',
    'How do I change my account password?',
    'I need a refund for my last purchase.',
    'The API is returning 500 errors intermittently.',
  ];

  setExample(example: string): void {
    this.input.set(example);
  }

  async executeDemo(): Promise<void> {
    this.isLoading.set(true);
    this.error.set(null);
    this.result.set(null);

    this.api.executeDemoWorkflow({ input: this.input() }).subscribe({
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
