import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { CategoryDto, DECISION_STATUSES, DecisionListItemDto, PagedResult } from '../../core/models';

@Component({
  selector: 'dv-decisions-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, RouterLink, ReactiveFormsModule],
  template: `
    <div class="page-head">
      <div>
        <h1>Decisions</h1>
        <p class="sub">{{ total() }} total</p>
      </div>
      <a routerLink="/decisions/new" class="btn primary">+ New decision</a>
    </div>

    <form [formGroup]="filters" class="filters">
      <input type="search" formControlName="search" placeholder="Search title or description…" />
      <select formControlName="categoryId">
        <option [ngValue]="null">All categories</option>
        @for (c of categories(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
      </select>
      <select formControlName="status">
        <option [ngValue]="null">Any status</option>
        @for (s of statuses; track s) { <option [ngValue]="s">{{ s }}</option> }
      </select>
      <select formControlName="outcome">
        <option [ngValue]="null">Any outcome</option>
        <option value="Successful">Successful</option>
        <option value="Unsuccessful">Unsuccessful</option>
        <option value="PendingReview">Pending review</option>
      </select>
      <select formControlName="sortBy">
        <option value="CreatedAt">Newest</option>
        <option value="Title">Title</option>
        <option value="ConfidenceScore">Confidence</option>
        <option value="ReviewDate">Review date</option>
      </select>
    </form>

    @if (error(); as e) {
      <div class="alert">{{ e }} <button type="button" class="btn ghost small" (click)="load()">Retry</button></div>
    }

    @if (loading()) {
      <div class="card">
        @for (i of [1,2,3,4]; track i) { <div class="row skeleton"></div> }
      </div>
    } @else if (items().length === 0) {
      <div class="card empty-state">
        <p>No decisions match these filters.</p>
        <a routerLink="/decisions/new" class="btn ghost small">Create one</a>
      </div>
    } @else {
      <div class="card table-card">
        <table>
          <thead>
            <tr><th>Decision</th><th>Status</th><th>Category</th><th>Confidence</th><th>Outcome</th><th>Created</th><th></th></tr>
          </thead>
          <tbody>
            @for (d of items(); track d.id) {
              <tr>
                <td>
                  <a [routerLink]="['/decisions', d.id]" class="row-link">{{ d.title }}</a>
                  <span class="opts">{{ d.optionsCount }} options</span>
                </td>
                <td><span class="pill" [attr.data-status]="d.status">{{ d.status }}</span></td>
                <td>{{ d.categoryName }}</td>
                <td>{{ d.confidenceScore ?? '—' }}</td>
                <td>{{ outcomeLabel(d) }}</td>
                <td>{{ d.createdAt | date: 'MMM d, y' }}</td>
                <td><a [routerLink]="['/decisions', d.id]" class="btn ghost small">Open</a></td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <div class="pager">
        <button type="button" class="btn ghost small" [disabled]="page() <= 1" (click)="go(page() - 1)">← Prev</button>
        <span>Page {{ page() }} of {{ totalPages() }}</span>
        <button type="button" class="btn ghost small" [disabled]="page() >= totalPages()" (click)="go(page() + 1)">Next →</button>
      </div>
    }
  `,
  styles: `
    .page-head { display: flex; justify-content: space-between; align-items: center; gap: 16px; margin-bottom: 18px; flex-wrap: wrap; }
    h1 { margin: 0; font-size: 24px; }
    .sub { margin: 4px 0 0; color: var(--text-dim, #93a1b8); font-size: 13px; }
    .filters { display: grid; grid-template-columns: 2fr 1fr 1fr 1fr 1fr; gap: 10px; margin-bottom: 16px; }
    .filters input, .filters select { background: var(--surface-1, #0e1626); border: 1px solid rgba(147,161,184,0.2); color: var(--text, #e8ecf4); border-radius: 9px; padding: 9px 11px; font-size: 13.5px; }
    .card { background: var(--surface-1, #0e1626); border: 1px solid rgba(147,161,184,0.12); border-radius: 12px; padding: 6px 0; overflow: hidden; }
    .table-card { padding: 0; overflow-x: auto; }
    .row { height: 34px; margin: 8px 14px; border-radius: 8px; }
    table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
    th { text-align: left; color: var(--text-dim, #93a1b8); font-weight: 600; font-size: 11.5px; text-transform: uppercase; letter-spacing: 0.05em; padding: 12px 14px; border-bottom: 1px solid rgba(147,161,184,0.14); }
    td { padding: 12px 14px; border-bottom: 1px solid rgba(147,161,184,0.07); }
    tr:last-child td { border-bottom: none; }
    .row-link { color: var(--text, #e8ecf4); text-decoration: none; font-weight: 600; }
    .row-link:hover { color: #f0b429; }
    .opts { display: block; color: var(--text-dim, #93a1b8); font-size: 11.5px; margin-top: 2px; }
    .pill { display: inline-block; padding: 3px 10px; border-radius: 999px; font-size: 11.5px; font-weight: 600; background: var(--surface-2, #16213a); color: var(--text-dim, #93a1b8); }
    .pill[data-status='Reviewed'] { background: rgba(79,195,161,0.15); color: #4fc3a1; }
    .pill[data-status='Decided'], .pill[data-status='InProgress'] { background: rgba(91,141,239,0.15); color: #5b8def; }
    .pill[data-status='ReadyForReview'] { background: rgba(240,180,41,0.15); color: #f0b429; }
    .pager { display: flex; align-items: center; justify-content: center; gap: 16px; margin-top: 16px; color: var(--text-dim, #93a1b8); font-size: 13px; }
    .empty-state { padding: 40px; text-align: center; color: var(--text-dim, #93a1b8); }
    .alert { background: rgba(229,115,115,0.12); border: 1px solid rgba(229,115,115,0.4); color: #f1a1a1; border-radius: 9px; padding: 10px 12px; font-size: 13px; margin-bottom: 16px; display: flex; gap: 12px; align-items: center; }
    .skeleton { position: relative; overflow: hidden; background: var(--surface-2, #16213a); }
    .skeleton::after { content: ''; position: absolute; inset: 0; background: linear-gradient(90deg, transparent, rgba(147,161,184,0.08), transparent); animation: shimmer 1.4s infinite; }
    @keyframes shimmer { from { transform: translateX(-100%); } to { transform: translateX(100%); } }
    @media (max-width: 900px) { .filters { grid-template-columns: 1fr 1fr; } }
  `
})
export class DecisionsListComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);

  readonly statuses = DECISION_STATUSES;
  readonly pageSize = 10;

  readonly filters = this.fb.nonNullable.group({
    search: [''],
    categoryId: [null as number | null],
    status: [null as string | null],
    outcome: [null as string | null],
    sortBy: ['CreatedAt']
  });

  readonly categories = signal<CategoryDto[]>([]);
  readonly items = signal<DecisionListItemDto[]>([]);
  readonly page = signal(1);
  readonly total = signal(0);
  readonly totalPages = signal(1);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  constructor() {
    void this.loadCategories();
    this.filters.valueChanges.pipe(debounceTime(250)).subscribe(() => {
      this.page.set(1);
      void this.load();
    });
    void this.load();
  }

  async loadCategories(): Promise<void> {
    try {
      this.categories.set(await firstValueFrom(this.api.categories()));
    } catch {
      // Non-fatal: filters degrade to text search only.
    }
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    const f = this.filters.getRawValue();
    try {
      const res = await firstValueFrom(this.api.decisions({
        page: this.page(),
        pageSize: this.pageSize,
        search: f.search || null,
        categoryId: f.categoryId,
        status: f.status,
        outcome: f.outcome,
        sortBy: f.sortBy
      }));
      this.items.set(res.items);
      this.total.set(res.totalCount);
      this.totalPages.set(Math.max(1, Math.ceil(res.totalCount / this.pageSize)));
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'Failed to load decisions.');
    } finally {
      this.loading.set(false);
    }
  }

  go(p: number): void {
    this.page.set(p);
    void this.load();
  }

  outcomeLabel(d: DecisionListItemDto): string {
    if (d.isSuccessful === null) return '—';
    return d.isSuccessful ? `Success (${d.outcomeRating}/5)` : `Miss (${d.outcomeRating}/5)`;
  }
}
