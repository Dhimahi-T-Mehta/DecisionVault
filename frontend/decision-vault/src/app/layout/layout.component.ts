import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
@Component({
  selector: 'dv-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterOutlet],
  template: `
    <div class="shell" [class.nav-open]="navOpen()">
      <aside class="sidebar">
        <div class="brand">
          <div class="brand-mark">DV</div>
          <span class="brand-name">Decision<b>Vault</b></span>
        </div>
        <nav>
          <a routerLink="/dashboard" routerLinkActive="active" (click)="closeNav()">Dashboard</a>
          <a routerLink="/decisions" routerLinkActive="active" (click)="closeNav()">Decisions</a>
          <a routerLink="/analytics" routerLinkActive="active" (click)="closeNav()">Analytics</a>
          @if (auth.isAdmin()) {
            <a routerLink="/admin" routerLinkActive="active" (click)="closeNav()">Admin</a>
          }
          <a routerLink="/profile" routerLinkActive="active" (click)="closeNav()">Profile</a>
        </nav>
        <div class="sidebar-foot">
          <div class="user-chip">
            <span class="avatar">{{ initial() }}</span>
            <span class="who">
              <span class="email">{{ auth.user()?.email }}</span>
              <span class="role">{{ auth.user()?.role }}</span>
            </span>
          </div>
          <button type="button" class="btn ghost small" (click)="auth.logout()">Sign out</button>
        </div>
      </aside>
      <div class="backdrop" (click)="closeNav()"></div>
      <main class="content">
        <header class="topbar">
          <button type="button" class="hamburger" (click)="toggleNav()" aria-label="Toggle navigation">☰</button>
          <span class="topbar-title">Decision<b>Vault</b></span>
        </header>
        <div class="page">
          <router-outlet />
        </div>
      </main>
    </div>
  `,
  styles: `
    :root { --navy-0: #0b1220; --navy-1: #0e1626; --navy-2: #16213a; --navy-3: #1d2a47; }
    .shell { display: grid; grid-template-columns: 240px 1fr; min-height: 100vh; background: var(--navy-0, #0b1220); color: var(--text, #e8ecf4); }
    .sidebar { display: flex; flex-direction: column; gap: 24px; padding: 22px 16px; background: var(--navy-1, #0e1626); border-right: 1px solid rgba(147,161,184,0.12); position: sticky; top: 0; height: 100vh; }
    .brand { display: flex; align-items: center; gap: 10px; padding: 0 8px; }
    .brand-mark { width: 34px; height: 34px; border-radius: 9px; background: linear-gradient(135deg, #f0b429, #d99a1b); color: #0b1220; font-weight: 800; display: grid; place-items: center; font-size: 13px; }
    .brand-name { font-size: 16px; letter-spacing: 0.02em; }
    .brand-name b { color: #f0b429; }
    nav { display: flex; flex-direction: column; gap: 4px; }
    nav a { padding: 10px 12px; border-radius: 8px; color: var(--text-dim, #93a1b8); text-decoration: none; font-size: 14px; transition: background .15s, color .15s; }
    nav a:hover { background: var(--navy-2, #16213a); color: var(--text, #e8ecf4); }
    nav a.active { background: var(--navy-3, #1d2a47); color: #f0b429; font-weight: 600; }
    .sidebar-foot { margin-top: auto; display: flex; flex-direction: column; gap: 10px; }
    .user-chip { display: flex; gap: 10px; align-items: center; }
    .avatar { width: 32px; height: 32px; border-radius: 50%; background: var(--navy-3, #1d2a47); color: #f0b429; display: grid; place-items: center; font-weight: 700; font-size: 13px; }
    .who { display: flex; flex-direction: column; min-width: 0; }
    .email { font-size: 12px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .role { font-size: 10px; color: var(--text-dim, #93a1b8); text-transform: uppercase; letter-spacing: 0.08em; }
    .content { min-width: 0; }
    .topbar { display: none; align-items: center; gap: 12px; padding: 12px 16px; background: var(--navy-1, #0e1626); border-bottom: 1px solid rgba(147,161,184,0.12); position: sticky; top: 0; z-index: 20; }
    .page { padding: 28px 32px; max-width: 1200px; margin: 0 auto; }
    .hamburger { display: none; background: none; border: 1px solid rgba(147,161,184,0.25); color: var(--text, #e8ecf4); border-radius: 8px; padding: 4px 10px; font-size: 16px; cursor: pointer; }
    .backdrop { display: none; }
    @media (max-width: 900px) {
      .shell { grid-template-columns: 1fr; }
      .topbar { display: flex; }
      .hamburger { display: block; }
      .sidebar { position: fixed; z-index: 30; left: 0; top: 0; bottom: 0; width: 250px; transform: translateX(-100%); transition: transform .2s ease; }
      .shell.nav-open .sidebar { transform: translateX(0); }
      .shell.nav-open .backdrop { display: block; position: fixed; inset: 0; background: rgba(0,0,0,0.5); z-index: 25; }
      .page { padding: 18px 16px; }
    }
  `
})
export class LayoutComponent {
  readonly auth = inject(AuthService);
  readonly navOpen = signal(false);

  initial(): string {
    const name = this.auth.user()?.fullName ?? this.auth.user()?.email ?? '?';
    return name.charAt(0).toUpperCase();
  }

  toggleNav(): void { this.navOpen.update(v => !v); }
  closeNav(): void { this.navOpen.set(false); }
}
