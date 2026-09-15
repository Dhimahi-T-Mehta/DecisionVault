import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { CategoryDto } from '../../core/models';
import { ApiClientError } from '../../core/api';

@Component({
  selector: 'dv-decision-create',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="narrow">
      <a routerLink="/decisions" class="back">← Back to decisions</a>
      <div class="card">
        <h1>New decision</h1>
        <p class="sub">Frame it as a question. You can add options and reasoning next.</p>

        @if (error(); as e) { <div class="alert">{{ e }}</div> }

        <form [formGroup]="form" (ngSubmit)="submit()">
          <label>Title *
            <input type="text" formControlName="title" placeholder="e.g. Which CI provider should we adopt?" />
          </label>
          <label>Category *
            <select formControlName="categoryId">
              <option [ngValue]="null" disabled>Select a category…</option>
              @for (c of categories(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
            </select>
          </label>
          <label>Description
            <textarea rows="4" formControlName="description" placeholder="Context, constraints, what success looks like…"></textarea>
          </label>
          <div class="actions">
            <a routerLink="/decisions" class="btn ghost">Cancel</a>
            <button type="submit" class="btn primary" [disabled]="form.invalid || busy()">
              @if (busy()) { Creating… } @else { Create decision }
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
  styles: `
    .narrow { max-width: 640px; margin: 0 auto; }
    .back { color: var(--text-dim, #93a1b8); text-decoration: none; font-size: 13px; display: inline-block; margin-bottom: 12px; }
    .back:hover { color: #f0b429; }
    .card { background: var(--surface-1, #0e1626); border: 1px solid rgba(147,161,184,0.12); border-radius: 12px; padding: 26px; }
    h1 { margin: 0 0 6px; font-size: 22px; }
    .sub { color: var(--text-dim, #93a1b8); font-size: 13px; margin: 0 0 18px; }
    form { display: flex; flex-direction: column; gap: 14px; }
    label { display: flex; flex-direction: column; gap: 6px; font-size: 12.5px; color: var(--text-dim, #93a1b8); }
    input, select, textarea { background: var(--surface-2, #16213a); border: 1px solid rgba(147,161,184,0.2); color: var(--text, #e8ecf4); border-radius: 9px; padding: 10px 12px; font-size: 14px; outline: none; font-family: inherit; }
    input:focus, select:focus, textarea:focus { border-color: #f0b429; }
    textarea { resize: vertical; }
    .actions { display: flex; justify-content: flex-end; gap: 10px; margin-top: 6px; }
    .alert { background: rgba(229,115,115,0.12); border: 1px solid rgba(229,115,115,0.4); color: #f1a1a1; border-radius: 9px; padding: 10px 12px; font-size: 13px; }
  `
})
export class DecisionCreateComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(200)]],
    categoryId: [null as number | null, Validators.required],
    description: ['']
  });

  readonly categories = signal<CategoryDto[]>([]);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    firstValueFrom(this.api.categories())
      .then(c => this.categories.set(c))
      .catch(() => this.error.set('Failed to load categories. Is the API running?'));
  }

  async submit(): Promise<void> {
    if (this.form.invalid || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    const v = this.form.getRawValue();
    try {
      const created = await firstValueFrom(this.api.createDecision({
        title: v.title,
        categoryId: v.categoryId!,
        description: v.description || null
      }));
      await this.router.navigate(['/decisions', created.id]);
    } catch (err) {
      this.error.set(err instanceof ApiClientError ? err.message : 'Failed to create decision.');
      this.busy.set(false);
    }
  }
}
