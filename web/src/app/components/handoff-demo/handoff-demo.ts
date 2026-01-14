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
import { ApiService, HandoffResponse } from '../../services/api.service';

@Component({
  selector: 'app-handoff-demo',
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
  templateUrl: './handoff-demo.html',
  styleUrl: './handoff-demo.scss',
})
export class HandoffDemo {
  private readonly api = inject(ApiService);

  protected readonly input = signal('I need help with a billing issue - I was charged twice and also my password is not working');
  protected readonly isLoading = signal(false);
  protected readonly result = signal<HandoffResponse | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly exampleInputs = [
    'I need help with a billing issue - I was charged twice and also my password is not working',
    'Can you help me update my payment method?',
    'The website is showing an error when I try to download my invoice',
    'I want to upgrade my account but first need to verify my email',
  ];

  setExample(example: string): void {
    this.input.set(example);
  }

  executeHandoff(): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.result.set(null);

    this.api.executeHandoff({ input: this.input() }).subscribe({
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
