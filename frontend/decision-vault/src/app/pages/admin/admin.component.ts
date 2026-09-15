import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { debounceTime, firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api.service';
import {
  CATEGORY_KINDS, ActivityDto, AdminDashboardDto, AdminUserDto, CategoryDto, CategoryUpsert, PagedResult
} from '../../core/models';
import { AuthService } from '../../core/auth.service';
import { ApiClientError } from '../../core/api';

@Component({
  selector: 'dv-admin',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, ReactiveFormsModule],
  template: `
    <div class="page-head">
      <div>
        <h1>Admin</h1>
        <p class="sub">Platform-wide oversight. Changes here affect every user.</p>
      </div>
    </div>

    @if (error(); as e) { <div class="alert">{{ e }} <button type="button" class="btn ghost small" (click)="loadAll()">Retry</button></div> }

    @if (stats(); as s) {
      <div class="kpis">
        <div class="kpi"><span class="kpi-value">{{ s.totalUsers }}</span><span class="kpi-label">Users</span></div>
        <div class="kpi good"><span class="kpi-value">{{ s.activeUsers }}</span><span class="kpi-label">Active</span></div>
        <div class="kpi"><span class="kpi-value">{{ s.totalDecisions }}</span><span class="kpi-label">Decisions</span></div>
        <div class="kpi amber"><span class="kpi-value">{{ s.overallSuccessRate }}%</span><span class="kpi-label">Overall success rate</span></div>
        <div class="kpi"><span class="kpi-value">{{ categories().length }}</span><span class="kpi-label">Categories</span></div>
      </div>
    }

    <div class="tabs" role="tablist">
      <button type="button" role="tab" [attr.aria-selected]="tab() === 'users'" [class.on]="tab() === 'users'" (click)="tab.set('users')">Users</button>
      <button type="button" role="tab" [attr.aria-selected]="tab() === 'categories'" [class.on]="tab() === 'categories'" (click)="tab.set('categories')">Categories</button>
      <button type="button" role="tab" [attr.aria-selected]="tab() === 'activity'" [class.on]="tab() === 'activity'" (click)="tab.set('activity')">Activity</button>
    </div>

    <!-- USERS -->
    @if (tab() === 'users') {
      <div class="card">
        <div class="card-head">
          <input type="search" placeholder="Search users…" [formControl]="userSearch" />
        </div>
        <table>
          <thead><tr><th>User</th><th>Role</th><th>Status</th><th>Decisions</th><th>Joined</th><th></th></tr></thead>
          <tbody>
            @for (u of users(); track u.id) {
              <tr>
                <td><b>{{ u.fullName }}</b><span class="mail">{{ u.email }}</span></td>
                <td><span class="pill" [attr.data-role]="u.role">{{ u.role }}</span></td>
                <td><span class="dot" [class.on]="u.isActive"></span>{{ u.isActive ? 'Active' : 'Inactive' }}</td>
                <td>{{ u.decisionCount }}</td>
                <td>{{ u.createdAt | date: 'MMM d, y' }}</td>
                <td class="ops">
                  <button type="button" class="btn ghost small" (click)="toggleActive(u)">{{ u.isActive ? 'Deactivate' : 'Activate' }}</button>
                  <button type="button" class="btn ghost small" (click)="changeRole(u)">{{ u.role === 'Admin' ? 'Make User' : 'Make Admin' }}</button>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="empty">No users match “{{ userSearch.value }}”.</td></tr>
            }
          </tbody>
        </table>
        <div class="pager">
          <button type="button" class="btn ghost small" [disabled]="userPage() <= 1" (click)="goUsers(userPage() - 1)">← Prev</button>
          <span>{{ usersTotal() }} user(s)</span>
          <button type="button" class="btn ghost small" [disabled]="userPage() >= userPages()" (click)="goUsers(userPage() + 1)">Next →</button>
        </div>
      </div>
    }

    <!-- CATEGORIES -->
    @if (tab() === 'categories') {
      <div class="split">
        <div class="card">
          <h2>{{ editingCategory() ? 'Edit category' : 'New category' }}</h2>
          <form [formGroup]="categoryForm" (ngSubmit)="saveCategory()">
            <label>Name *<input formControlName="name" placeholder="e.g. Career" /></label>
            <label>Description<input formControlName="description" placeholder="What belongs here?" /></label>
            <label>Kind *
              <select formControlName="kind">
                @for (k of kinds; track k) {
                  <option [value]="k">{{ k }}</option>
                }
              </select>
            </label>
            <div class="row-actions">
              @if (editingCategory()) {
                <button type="button" class="btn ghost" (click)="cancelCategoryEdit()">Cancel edit</button>
              }
              <button type="submit" class="btn primary" [disabled]="categoryForm.invalid || busy()">
                {{ editingCategory() ? 'Save category' : 'Add category' }}
              </button>
            </div>
          </form>
        </div>
        <div class="card">
          <h2>Categories ({{ categories().length }})</h2>
          <ul class="cat-list">
            @for (c of categories(); track c.id) {
              <li>
                <div><b>{{ c.name }}</b><span class="kind">{{ c.kind }}</span>@if (c.description) {<span class="cdesc">{{ c.description }}</span>}</div>
                <div class="row-actions">
                  <button type="button" class="btn ghost small" (click)="editCategory(c)">Edit</button>
                  <button type="button" class="btn ghost small" (click)="deleteCategory(c)">Delete</button>
                </div>
              </li>
            } @empty {
              <li class="empty">No categories.</li>
            }
          </ul>
        </div>
      </div>
    }

    <!-- ACTIVITY -->
    @if (tab() === 'activity') {
      <div class="card">
        <table>
          <thead><tr><th>Event</th><th>Description</th><th>User</th><th>When</th></tr></thead>
          <tbody>
            @for (a of activity(); track a.id) {
              <tr>
                <td><span class="pill">{{ a.eventType }}</span></td>
                <td>{{ a.description }}</td>
                <td>{{ a.userEmail }}</td>
                <td>{{ a.createdAt | date: 'MMM d, HH:mm' }}</td>
              </tr>
            } @empty {
              <tr><td colspan="4" class="empty">No activity recorded.</td></tr>
            }
          </tbody>
        </table>
        <div class="pager">
          <button type="button" class="btn ghost small" [disabled]="actPage() <= 1" (click)="goActivity(actPage() - 1)">← Prev</button>
          <span>{{ actTotal() }} event(s)</span>
          <button type="button" class="btn ghost small" [disabled]="actPage() >= actPages()" (click)="goActivity(actPage() + 1)">Next →</button>
        </div>
      </div>
    }
  `,
  styles: `
    .page-head { display: flex; justify-content: space-between; align-items: center; gap: 16px; margin-bottom: 18px; }
    h1 { margin: 0; font-size: 24px; }
    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 14px; margin-bottom: 20px; }
    .kpi { background: var(--surface-1, #0e1626); border: 1px solid rgba(147,161,184,0.12); border-radius: 12px; padding: 14px 16px; }
    .kpi-value { display: block; font-size: 22px; font-weight: 700; }
    .kpi-label { font-size: 11px; color: var(--text-dim, #93a1b8); text-transform: uppercase; letter-spacing: 0.06em; }
    .kpi.good .kpi-value { color: #4fc3a1; } .kpi.amber .kpi-value { color: #f0b429; }
    .tabs { display: flex; gap: 6px; margin-bottom: 16px; }
    .tabs button { background: none; border: 1px solid transparent; color: var(--text-dim, #93a1b8); padding: 8px 16px; border-radius: 9px; font-size: 13.5px; cursor: pointer; }
    .tabs button.on { background: var(--surface-1, #0e1626); border-color: rgba(147,161,184,0.2); color: #f0b429; font-weight: 600; }
    .card { background: var(--surface-1, #0e1626); border: 1px solid rgba(147,161,184,0.12); border-radius: 12px; padding: 18px; }
    .card h2 { margin: 0 0 12px; font-size: 13px; color: var(--text-dim, #93a1b8); text-transform: uppercase; letter-spacing: 0.06em; }
    .card-head { margin-bottom: 10px; }
    input[type='search'] { width: 100%; max-width: 320px; background: var(--surface-2, #16213a); border: 1px solid rgba(147,161,184,0.2); color: var(--text, #e8ecf4); border-radius: 9px; padding: 9px 11px; font-size: 13.5px; outline: none; }
    table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
    th { text-align: left; color: var(--text-dim, #93a1b8); font-weight: 600; font-size: 11.5px; text-transform: uppercase; letter-spacing: 0.05em; padding: 8px 10px; border-bottom: 1px solid rgba(147,161,184,0.14); }
    td { padding: 11px 10px; border-bottom: 1px solid rgba(147,161,184,0.07); }
    .mail { display: block; color: var(--text-dim, #93a1b8); font-size: 12px; }
    .pill { display: inline-block; padding: 3px 10px; border-radius: 999px; font-size: 11.5px; font-weight: 600; background: var(--surface-2, #16213a); color: var(--text-dim, #93a1b8); }
    .pill[data-role='Admin'] { background: rgba(240,180,41,0.15); color: #f0b429; }
    .dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%; background: #e57373; margin-right: 7px; }
    .dot.on { background: #4fc3a1; }
    .ops { display: flex; gap: 6px; flex-wrap: wrap; }
    .split { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; align-items: start; }
    form { display: flex; flex-direction: column; gap: 12px; }
    label { display: flex; flex-direction: column; gap: 6px; font-size: 12.5px; color: var(--text-dim, #93a1b8); }
    label input, label select { background: var(--surface-2, #16213a); border: 1px solid rgba(147,161,184,0.2); color: var(--text, #e8ecf4); border-radius: 9px; padding: 9px 11px; font-size: 13.5px; outline: none; }
    .row-actions { display: flex; gap: 6px; flex-wrap: wrap; }
    .cat-list { list-style: none; padding: 0; margin: 0; }
    .cat-list li { display: flex; justify-content: space-between; align-items: center; gap: 10px; padding: 10px 0; border-bottom: 1px solid rgba(147,161,184,0.07); }
    .cat-list li:last-child { border-bottom: none; }
    .kind { display: inline-block; margin-left: 8px; font-size: 10.5px; background: var(--surface-2, #16213a); border-radius: 6px; padding: 2px 7px; color: #5b8def; font-weight: 600; }
    .cdesc { display: block; font-size: 12px; color: var(--text-dim, #93a1b8); }
    .pager { display: flex; align-items: center; justify-content: center; gap: 14px; margin-top: 12px; color: var(--text-dim, #93a1b8); font-size: 12.5px; }
    .empty { color: var(--text-dim, #93a1b8); text-align: center; padding: 16px; }
    .alert { background: rgba(229,115,115,0.12); border: 1px solid rgba(229,115,115,0.4); color: #f1a1a1; border-radius: 9px; padding: 10px 12px; font-size: 13px; margin-bottom: 16px; display: flex; gap: 12px; align-items: center; }
    @media (max-width: 900px) { .split { grid-template-columns: 1fr; } }
  `
})
export class AdminComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);

  readonly currentUserId = () => this.auth.user()?.id ?? -1;

  readonly tab = signal<'users' | 'categories' | 'activity'>('users');
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  readonly stats = signal<AdminDashboardDto | null>(null);
  readonly users = signal<AdminUserDto[]>([]);
  readonly userPage = signal(1);
  readonly usersTotal = signal(0);
  readonly userPages = signal(1);
  readonly userSearch = this.fb.nonNullable.control('');

  readonly categories = signal<CategoryDto[]>([]);
  readonly editingCategory = signal<number | null>(null);
  readonly categoryForm = this.fb.nonNullable.group({
    name: ['', Validators.required],
    description: [''],
    kind: ['Career']
  });

  readonly activity = signal<ActivityDto[]>([]);
  readonly actPage = signal(1);
  readonly actTotal = signal(0);
  readonly actPages = signal(1);

  async loadAll(): Promise<void> {
    this.error.set(null);
    await Promise.all([this.loadStats(), this.loadUsers(), this.loadCategories(), this.loadActivity()]);
  }

  readonly kinds = CATEGORY_KINDS;

  constructor() {
    void this.loadAll();
    this.userSearch.valueChanges.pipe(debounceTime(300)).subscribe(() => {
      this.userPage.set(1);
      void this.loadUsers();
    });
  }


  async loadStats(): Promise<void> {
    try { this.stats.set(await firstValueFrom(this.api.adminDashboard())); }
    catch (err) { this.fail(err); }
  }

  async loadUsers(): Promise<void> {
    try {
      const res = await firstValueFrom(this.api.adminUsers(this.userSearch.value || null, this.userPage(), 10));
      this.users.set(res.items);
      this.usersTotal.set(res.totalCount);
      this.userPages.set(Math.max(1, Math.ceil(res.totalCount / 10)));
    } catch (err) { this.fail(err); }
  }

  async loadCategories(): Promise<void> {
    try { this.categories.set(await firstValueFrom(this.api.categories())); }
    catch (err) { this.fail(err); }
  }

  async loadActivity(): Promise<void> {
    try {
      const res = await firstValueFrom(this.api.adminActivity(this.actPage(), 10));
      this.activity.set(res.items);
      this.actTotal.set(res.totalCount);
      this.actPages.set(Math.max(1, Math.ceil(res.totalCount / 10)));
    } catch (err) { this.fail(err); }
  }

  fail(err: unknown): void {
    this.error.set(err instanceof ApiClientError ? err.message : 'Failed to load admin data.');
  }

  goUsers(p: number): void { this.userPage.set(p); void this.loadUsers(); }
  goActivity(p: number): void { this.actPage.set(p); void this.loadActivity(); }

  async toggleActive(u: AdminUserDto): Promise<void> {
    const verb = u.isActive ? 'deactivate' : 'reactivate';
    if (u.id === this.currentUserId() && !confirm(`${verb} your own account? You will be signed out immediately.`)) return;
    if (u.id !== this.currentUserId() && !confirm(`${verb} ${u.fullName}?`)) return;
    await this.guard(() => this.api.setUserActive(u.id, !u.isActive).toPromise());
    await this.loadUsers();
    await this.loadStats();
  }

  async changeRole(u: AdminUserDto): Promise<void> {
    const next = u.role === 'Admin' ? 'User' : 'Admin';
    if (u.id === this.currentUserId() && !confirm(`Demote yourself to User? You will lose admin access immediately.`)) return;
    if (u.id !== this.currentUserId() && !confirm(`Make ${u.fullName} ${next === 'Admin' ? 'an admin' : 'a regular user'}?`)) return;
    await this.guard(() => this.api.setUserRole(u.id, next).toPromise());
    await this.loadUsers();
  }

  editCategory(c: CategoryDto): void {
    this.editingCategory.set(c.id);
    this.categoryForm.patchValue({ name: c.name, description: c.description ?? '', kind: c.kind });
  }

  cancelCategoryEdit(): void {
    this.editingCategory.set(null);
    this.categoryForm.reset({ name: '', description: '', kind: 'Career' });
  }

  async saveCategory(): Promise<void> {
    const v = this.categoryForm.getRawValue();
    const body: CategoryUpsert = { name: v.name, description: v.description || null, kind: v.kind };
    const id = this.editingCategory();
    await this.guard(async () => id ? this.api.updateCategory(id, body).toPromise() : this.api.createCategory(body).toPromise());
    if (id) this.cancelCategoryEdit();
    await this.loadCategories();
  }

  async deleteCategory(c: CategoryDto): Promise<void> {
    if (!confirm(`Delete category "${c.name}"? Decisions in it must be moved first.`)) return;
    await this.guard(() => this.api.deleteCategory(c.id).toPromise());
    if (this.editingCategory() === c.id) this.cancelCategoryEdit();
    await this.loadCategories();
  }

  async guard(action: () => Promise<unknown>): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    try { await action(); }
    catch (err) { this.error.set(err instanceof ApiClientError ? err.message : 'Action failed.'); }
    finally { this.busy.set(false); }
  }
}
