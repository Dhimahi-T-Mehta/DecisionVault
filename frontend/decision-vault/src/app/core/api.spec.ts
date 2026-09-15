import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { TestBed } from '@angular/core/testing';
import { authInterceptor, apiErrorToMessage, ApiClientError } from './api';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';
import type { AuthResponse } from './models';

const session: AuthResponse = {
  token: 'jwt-tok',
  expiresAt: new Date(Date.now() + 3.6e6).toISOString(),
  user: { id: 1, fullName: 'U', email: 'u@example.com', role: 'User', createdAt: '2026-01-01T00:00:00Z' }
};

describe('authInterceptor', () => {
  let http: HttpTestingController;
  let client: HttpClient;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('attaches the Bearer token to API calls', async () => {
    TestBed.inject(AuthService).applySession(session);

    const done = firstValueFrom(client.get(`${environment.apiBaseUrl}/api/decisions`));
    const req = http.expectOne(`${environment.apiBaseUrl}/api/decisions`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-tok');
    req.flush([]);
    await done;
  });

  it('leaves foreign URLs unauthenticated', async () => {
    TestBed.inject(AuthService).applySession(session);

    const done = firstValueFrom(client.get('http://other-host.example/api/thing'));
    const req = http.expectOne('http://other-host.example/api/thing');
    expect(req.request.headers.get('Authorization')).toBeNull();
    req.flush({});
    await done;
  });

  it('clears the session and redirects on 401 while authenticated', async () => {
    const auth = TestBed.inject(AuthService);
    auth.applySession(session);
    expect(auth.isAuthenticated()).toBeTrue();

    const captured: unknown[] = [];
    client.get(`${environment.apiBaseUrl}/api/decisions`).subscribe({ error: (e: unknown) => captured.push(e) });
    const req = http.expectOne(`${environment.apiBaseUrl}/api/decisions`);
    req.flush({ success: false, message: 'Token expired', errors: [], timestamp: 't' }, { status: 401, statusText: 'Unauthorized' });

    await Promise.resolve();
    const err = captured[0] as ApiClientError;
    expect(err).toBeInstanceOf(ApiClientError);
    expect(auth.isAuthenticated()).toBeFalse();
  });
});

describe('apiErrorToMessage', () => {
  const makeErr = (status: number, body?: unknown) =>
    new HttpErrorResponse({ status, error: body, url: '/x' });

  it('maps the API error envelope onto message + field errors', () => {
    const err = apiErrorToMessage(makeErr(400, {
      success: false, message: 'Decision failed validation.', errors: ['Title must be 3-200 characters.'], timestamp: 't'
    }));
    expect(err).toBeInstanceOf(ApiClientError);
    expect(err.message).toBe('Decision failed validation.');
    expect(err.fieldErrors).toEqual(['Title must be 3-200 characters.']);
    expect(err.status).toBe(400);
  });

  it('gives a friendly offline message for status 0', () => {
    expect(apiErrorToMessage(makeErr(0)).message).toContain('Cannot reach the server');
  });

  it('falls back per status code when the body is not an envelope', () => {
    expect(apiErrorToMessage(makeErr(403)).message).toContain('permission');
    expect(apiErrorToMessage(makeErr(404)).message).toContain('not found');
    expect(apiErrorToMessage(makeErr(409)).message).toContain('conflicts');
    expect(apiErrorToMessage(makeErr(500)).message).toContain('Server error');
    expect(apiErrorToMessage(makeErr(422)).message).toContain('Request failed');
  });
});
