import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { DashboardDto } from '../../core/models';
import { DonutChart, Slice } from '../../shared/charts/donut.chart';
import { BarChart, Bar } from '../../shared/charts/bar.chart';
import { LineChart, Point } from '../../shared/charts/line.chart';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

@Component({
  selector: 'dv-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, DonutChart, BarChart, LineChart],
  template: `
    <div class="page-head">
      <div>
        <h1>Dashboard</h1>
        <p class="sub">Your decision-making at a glance.</p>
      </div>
      <a routerLink="/decisions/new" class="btn primary">+ New decision</a>
    </div>

    @if (error(); as e) {
      <div class="alert">{{ e }} <button type="button" class="btn ghost small" (click)="load()">Retry</button></div>
    }

    @if (loading()) {
      <div class="kpis">
        @for (i of [1,2,3,4]; track i) { <div class="kpi skeleton">&nbsp;</div> }
      </div>
      <div class="grid">
        <div class="card skeleton tall"></div>
        <div class="card skeleton tall"></div>
      </div>
    } @else if (data(); as d) {
      <div class="kpis">
        <div class="kpi"><span class="kpi-value">{{ d.totalDecisions }}</span><span class="kpi-label">Total decisions</span></div>
        <div class="kpi"><span class="kpi-value">{{ d.pendingReview }}</span><span class="kpi-label">Pending review</span></div>
        <div class="kpi good"><span class="kpi-value">{{ d.successful }}</span><span class="kpi-label">Successful</span></div>
        <div class="kpi bad"><span class="kpi-value">{{ d.unsuccessful }}</span><span class="kpi-label">Unsuccessful</span></div>
        <div class="kpi"><span class="kpi-value">{{ d.averageConfidence }}%</span><span class="kpi-label">Avg confidence</span></div>
        <div class="kpi amber"><span class="kpi-value">{{ d.decisionAccuracy }}%</span><span class="kpi-label">Decision accuracy</span></div>
      </div>

      <div class="grid">
        <div class="card">
          <h2>Outcome distribution</h2>
          <dv-donut [slices]="outcomeSlices(d)" title="Outcome distribution" />
        </div>
        <div class="card">
          <h2>Decisions by category</h2>
          <dv-bars [bars]="categoryBars(d)" title="Decisions by category" />
        </div>
        <div class="card">
          <h2>Decisions by status</h2>
          <dv-bars [bars]="statusBars(d)" title="Decisions by status" />
        </div>
        <div class="card">
          <h2>Monthly trend</h2>
          <dv-line [points]="trendPoints(d)" [xLabelFor]="monthLabel" title="Monthly trend" />
        </div>
      </div>

      <div class="card">
        <h2>Recent decisions</h2>
        @if (d.recentDecisions.length === 0) {
          <p class="empty">No decisions yet. <a routerLink="/decisions/new">Create your first one</a>.</p>
        } @else {
          <table>
            <thead><tr><th>Title</th><th>Status</th><th>Category</th><th>Confidence</th><th>Outcome</th><th>Created</th></tr></thead>
            <tbody>
              @for (r of d.recentDecisions; track r.id) {
                <tr>
                  <td><a [routerLink]="['/decisions', r.id]" class="row-link">{{ r.title }}</a></td>
                  <td><span class="pill" [attr.data-status]="r.status">{{ r.status }}</span></td>
                  <td>{{ r.categoryName }}</td>
                  <td>{{ r.confidenceScore ?? '—' }}</td>
                  <td>{{ outcomeLabel(r.isSuccessful, r.outcomeRating) }}</td>
                  <td>{{ r.createdAt | date: 'MMM d, y' }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    }
  `,
  styles: `
    .page-head { display: flex; justify-content: space-between; align-items: center; gap: 16px; margin-bottom: 20px; flex-wrap: wrap; }
    h1 { margin: 0; font-size: 24px; }
    .sub { margin: 4px 0 0; color: var(--text-dim, #93a1b8); font-size: 13px; }
    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 14px; margin-bottom: 20px; }
    .kpi { background: var(--surface-1, #0e1626); border: 1px solid rgba(147,161,184,0.12); border-radius: 12px; padding: 16px; display: flex; flex-direction: column; gap: 4px; }
    .kpi-value { font-size: 26px; font-weight: 700; }
    .kpi.good .kpi-value { color: #4fc3a1; }
    .kpi.bad .kpi-value { color: #e57373; }
    .kpi.amber .kpi-value { color: #f0b429; }
    .kpi-label { font-size: 11.5px; color: var(--text-dim, #93a1b8); text-transform: uppercase; letter-spacing: 0.06em; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-bottom: 20px; }
    .card { background: var(--surface-1, #0e1626); border: 1px solid rgba(147,161,184,0.12); border-radius: 12px; padding: 18px; }
    .card h2 { margin: 0 0 12px; font-size: 14px; color: var(--text-dim, #93a1b8); font-weight: 600; text-transform: uppercase; letter-spacing: 0.06em; }
    .skeleton { position: relative; overflow: hidden; color: transparent; }
    .skeleton::after { content: ''; position: absolute; inset: 0; background: linear-gradient(90deg, transparent, rgba(147,161,184,0.08), transparent); animation: shimmer 1.4s infinite; }
    .tall { min-height: 180px; }
    @keyframes shimmer { from { transform: translateX(-100%); } to { transform: translateX(100%); } }
    table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
    th { text-align: left; color: var(--text-dim, #93a1b8); font-weight: 600; font-size: 11.5px; text-transform: uppercase; letter-spacing: 0.05em; padding: 8px 10px; border-bottom: 1px solid rgba(147,161,184,0.14); }
    td { padding: 10px; border-bottom: 1px solid rgba(147,161,184,0.07); }
    .row-link { color: var(--text, #e8ecf4); text-decoration: none; font-weight: 600; }
    .row-link:hover { color: #f0b429; }
    .pill { display: inline-block; padding: 3px 10px; border-radius: 999px; font-size: 11.5px; font-weight: 600; background: var(--surface-2, #16213a); color: var(--text-dim, #93a1b8); }
    .pill[data-status='Reviewed'] { background: rgba(79,195,161,0.15); color: #4fc3a1; }
    .pill[data-status='Decided'], .pill[data-status='InProgress'] { background: rgba(91,141,239,0.15); color: #5b8def; }
    .pill[data-status='ReadyForReview'] { background: rgba(240,180,41,0.15); color: #f0b429; }
    .pill[data-status='Draft'], .pill[data-status='Evaluating'] { background: rgba(147,161,184,0.15); }
    .empty { color: var(--text-dim, #93a1b8); }
    .empty a { color: #f0b429; }
    .alert { background: rgba(229,115,115,0.12); border: 1px solid rgba(229,115,115,0.4); color: #f1a1a1; border-radius: 9px; padding: 10px 12px; font-size: 13px; margin-bottom: 16px; display: flex; gap: 12px; align-items: center; }
  `
})
export class DashboardComponent {
  private readonly api = inject(ApiService);

  readonly data = signal<DashboardDto | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly monthLabel = (p: Point) => `${MONTHS[(p.x - 1) % 12]}`;

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.data.set(await firstValueFrom(this.api.dashboard()));
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'Failed to load dashboard.');
    } finally {
      this.loading.set(false);
    }
  }

  outcomeSlices(d: DashboardDto): Slice[] {
    const colors: Record<string, string> = {
      Successful: '#4fc3a1', Unsuccessful: '#e57373', 'Pending Review': '#8d99ae'
    };
    return d.outcomeDistribution.map(o => ({ label: o.label, value: o.count, color: colors[o.label] }));
  }

  categoryBars(d: DashboardDto): Bar[] {
    return d.decisionsByCategory.map(c => ({ label: c.categoryName, value: c.count }));
  }

  statusBars(d: DashboardDto): Bar[] {
    const colors: Record<string, string> = {
      Draft: '#8d99ae', Evaluating: '#b58cd9', Decided: '#5b8def',
      InProgress: '#f4a259', ReadyForReview: '#f0b429', Reviewed: '#4fc3a1'
    };
    return d.decisionsByStatus.map(s => ({ label: s.status, value: s.count, color: colors[s.status] }));
  }

  trendPoints(d: DashboardDto): Point[] {
    return d.monthlyTrend
      .slice()
      .sort((a, b) => a.year * 12 + a.month - (b.year * 12 + b.month))
      .map(t => ({ x: t.month, y: t.total, label: `${MONTHS[t.month - 1]} ${t.year}` }));
  }

  outcomeLabel(successful: boolean | null, rating: number | null): string {
    if (successful === null) return '—';
    return successful ? `Success (${rating}/5)` : `Miss (${rating}/5)`;
  }
}
