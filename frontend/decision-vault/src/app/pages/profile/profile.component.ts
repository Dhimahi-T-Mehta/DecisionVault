import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ProfileSummaryDto } from '../../core/models';
import { ApiClientError } from '../../core/api';

@Component({
  selector: 'dv-profile',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, ReactiveFormsModule],
  template: `
    <div class="narrow">
      <h1>Profile</h1>

      @if (error(); as e) { <div class="alert">{{ e }}</div> }
      @if (notice(); as n) { <div class="notice">{{ n }}</div> }

      @if (loading()) {
        <div class="card skeleton" style="min-height:120px"></div>
      } @else if (summary(); as s) {
        <section class="card">
          <h2>Membership</h2>
          <div class="member">
            <span class="avatar">{{ initial() }}</span>
            <div>
              <b class="name">{{ auth.user()?.fullName }}</b>
              <span class="mail">{{ auth.user()?.email }}</span>
              <span class="since">Member since {{ s.memberSince | date: 'MMMM y' }} · {{ auth.user()?.role }}</span>
            </div>
          </div>
          <div class="stats">
            <div class="stat"><b>{{ s.totalDecisions }}</b><span>Total decisions</span></div>
            <div class="stat"><b>{{ s.averageOutcomeRating ?? '—' }}</b><span>Avg outcome rating</span></div>
          </div>
        </section>
      }

      <section class="card">
        <h2>Update profile</h2>
        <form [formGroup]="profileForm" (ngSubmit)="saveProfile()">
          <label>Full name<input formControlName="fullName" /></label>
          <label>Email<input formControlName="email" type="email" /></label>
          <button type="submit" class="btn primary" [disabled]="profileForm.invalid || busy()">Save changes</button>
        </form>
      </section>

      <section class="card">
        <h2>Change password</h2>
        <form [formGroup]="passwordForm" (ngSubmit)="savePassword()">
          <label>Current password<input type="password" formControlName="currentPassword" autocomplete="current-password" /></label>
          <label>New password<input type="password" formControlName="newPassword" autocomplete="new-password" placeholder="8+ chars, upper, lower, digit" /></label>
          <button type="submit" class="btn primary" [disabled]="passwordForm.invalid || busy()">Change password</button>
        </form>
      </section>
    </div>
  `,
  styles: `
    .narrow { max-width: 620px; margin: 0 auto; }
    h1 { margin: 0 0 16px; font-size: 24px; }
    .card { background: var(--surface-1, #0e1626); border: 1px solid rgba(147,161,184,0.12); border-radius: 12px; padding: 20px; margin-bottom: 16px; }
    .card h2 { margin: 0 0 14px; font-size: 13px; color: var(--text-dim, #93a1b8); text-transform: uppercase; letter-spacing: 0.06em; }
    .member { display: flex; gap: 14px; align-items: center; margin-bottom: 16px; }
    .avatar { width: 52px; height: 52px; border-radius: 50%; background: var(--surface-2, #16213a); color: #f0b429; display: grid; place-items: center; font-weight: 800; font-size: 22px; }
    .name { display: block; font-size: 16px; }
    .mail { display: block; color: var(--text-dim, #93a1b8); font-size: 13px; }
    .since { display: block; color: var(--text-dim, #93a1b8); font-size: 12px; margin-top: 2px; }
    .stats { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
    .stat { background: var(--surface-2, #16213a); border-radius: 10px; padding: 12px 14px; }
    .stat b { font-size: 20px; display: block; }
    .stat span { font-size: 11px; color: var(--text-dim, #93a1b8); text-transform: uppercase; letter-spacing: 0.05em; }
    form { display: flex; flex-direction: column; gap: 12px; }
    label { display: flex; flex-direction: column; gap: 6px; font-size: 12.5px; color: var(--text-dim, #93a1b8); }
    input { background: var(--surface-2, #16213a); border: 1px solid rgba(147,161,184,0.2); color: var(--text, #e8ecf4); border-radius: 9px; padding: 10px 12px; font-size: 14px; outline: none; }
    input:focus { border-color: #f0b429; }
    form button { align-self: flex-start; }
    .alert { background: rgba(229,115,115,0.12); border: 1px solid rgba(229,115,115,0.4); color: #f1a1a1; border-radius: 9px; padding: 10px 12px; font-size: 13px; margin-bottom: 14px; }
    .notice { background: rgba(79,195,161,0.12); border: 1px solid rgba(79,195,161,0.4); color: #7fd8bf; border-radius: 9px; padding: 10px 12px; font-size: 13px; margin-bottom: 14px; }
  `
})
export class ProfileComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  readonly auth = inject(AuthService);

  readonly summary = signal<ProfileSummaryDto | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly busy = signal(false);

  readonly profileForm = this.fb.nonNullable.group({
    fullName: [this.auth.user()?.fullName ?? ''],
    email: [this.auth.user()?.email ?? '',]
  });

  readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,72}$/)]]
  });

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.summary.set(await firstValueFrom(this.api.profileSummary()));
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'Failed to load profile.');
    } finally {
      this.loading.set(false);
    }
  }

  async saveProfile(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    this.notice.set(null);
    const v = this.profileForm.getRawValue();
    try {
      await firstValueFrom(this.auth.updateProfile(v.fullName, v.email));
      this.notice.set('Profile updated.');
    } catch (err) {
      this.error.set(err instanceof ApiClientError ? err.message : 'Failed to update profile.');
    } finally {
      this.busy.set(false);
    }
  }

  async savePassword(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    this.notice.set(null);
    const v = this.passwordForm.getRawValue();
    try {
      await firstValueFrom(this.auth.changePassword(v.currentPassword, v.newPassword));
      this.notice.set('Password changed.');
      this.passwordForm.reset();
    } catch (err) {
      this.error.set(err instanceof ApiClientError ? err.message : 'Failed to change password.');
    } finally {
      this.busy.set(false);
    }
  }

  initial(): string {
    const name = this.auth.user()?.fullName ?? this.auth.user()?.email ?? '?';
    return name.charAt(0).toUpperCase();
  }
}
