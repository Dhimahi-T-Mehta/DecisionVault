import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResponse, UserDto } from './models';

const TOKEN_KEY = 'dv_token';
const USER_KEY = 'dv_user';
const EXPIRY_KEY = 'dv_expires';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly tokenSignal = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  private readonly userSignal = signal<UserDto | null>(this.readUser());
  private readonly expirySignal = signal<string | null>(localStorage.getItem(EXPIRY_KEY));

  /** Reactive session state. */
  readonly token = this.tokenSignal.asReadonly();
  readonly user = this.userSignal.asReadonly();
  readonly isAuthenticated = computed(() => {
    const token = this.tokenSignal();
    const expiry = this.expirySignal();
    return !!token && !!expiry && new Date(expiry).getTime() > Date.now();
  });
  readonly isAdmin = computed(() => this.userSignal()?.role === 'Admin');

  login(email: string, password: string) {
    return this.http
      .post<AuthResponse>(`${environment.apiBaseUrl}/api/auth/login`, { email, password })
      .pipe(tap(res => this.applySession(res)));
  }

  register(fullName: string, email: string, password: string) {
    return this.http
      .post<AuthResponse>(`${environment.apiBaseUrl}/api/auth/register`, { fullName, email, password })
      .pipe(tap(res => this.applySession(res)));
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    localStorage.removeItem(EXPIRY_KEY);
    this.tokenSignal.set(null);
    this.userSignal.set(null);
    this.expirySignal.set(null);
    void this.router.navigate(['/login']);
  }

  /** Clears a stale session without navigating (used by the 401 interceptor). */
  clearSession(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    localStorage.removeItem(EXPIRY_KEY);
    this.tokenSignal.set(null);
    this.userSignal.set(null);
    this.expirySignal.set(null);
  }

  /** Refreshes the cached profile after /auth/me or profile updates. */
  setUser(user: UserDto): void {
    this.userSignal.set(user);
    localStorage.setItem(USER_KEY, JSON.stringify(user));
  }

  updateProfile(fullName: string, email: string) {
    return this.http
      .put<UserDto>(`${environment.apiBaseUrl}/api/auth/profile`, { fullName, email })
      .pipe(tap(user => this.setUser(user)));
  }

  changePassword(currentPassword: string, newPassword: string) {
    return this.http.put(`${environment.apiBaseUrl}/api/auth/change-password`, {
      currentPassword,
      newPassword
    });
  }

  /** Applies a successful auth response; public so tests can seed a session. */
  applySession(res: AuthResponse): void {
    localStorage.setItem(TOKEN_KEY, res.token);
    localStorage.setItem(USER_KEY, JSON.stringify(res.user));
    localStorage.setItem(EXPIRY_KEY, res.expiresAt);
    this.tokenSignal.set(res.token);
    this.userSignal.set(res.user);
    this.expirySignal.set(res.expiresAt);
  }

  private readUser(): UserDto | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as UserDto;
    } catch {
      return null;
    }
  }
}
