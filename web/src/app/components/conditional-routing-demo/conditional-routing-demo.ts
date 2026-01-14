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
import { ApiService, ConditionalRoutingResponse } from '../../services/api.service';

@Component({
  selector: 'app-conditional-routing-demo',
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
  templateUrl: './conditional-routing-demo.html',
  styleUrl: './conditional-routing-demo.scss',
})
export class ConditionalRoutingDemo {
  private readonly api = inject(ApiService);

  protected readonly input = signal('I was charged twice for my subscription last month');
  protected readonly isLoading = signal(false);
  protected readonly result = signal<ConditionalRoutingResponse | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly exampleInputs = [
    'I was charged twice for my subscription last month',
    'The app keeps crashing when I try to upload a photo',
    'How much does the premium plan cost?',
    'What are your business hours?',
  ];

  protected readonly categoryIcons: Record<string, string> = {
    'BILLING': 'payments',
    'TECHNICAL': 'build',
    'SALES': 'shopping_cart',
    'GENERAL': 'help',
  };

  setExample(example: string): void {
    this.input.set(example);
  }

  executeRouting(): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.result.set(null);

    this.api.executeConditionalRouting({ input: this.input() }).subscribe({
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

  getCategoryIcon(category: string): string {
    return this.categoryIcons[category] || 'help';
  }
}
