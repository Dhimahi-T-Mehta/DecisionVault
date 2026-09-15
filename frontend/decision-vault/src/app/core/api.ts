import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../environments/environment';
import { ApiError } from './models';
import { AuthService } from './auth.service';

/** Attaches the JWT and maps API error envelopes onto a uniform client error. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const token = auth.token();
  const isApiCall = req.url.startsWith(environment.apiBaseUrl);
  const authed = token && isApiCall
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authed).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse) {
        if (err.status === 401 && isApiCall && auth.isAuthenticated()) {
          // Token expired or revoked mid-session.
          auth.clearSession();
          void router.navigate(['/login'], { queryParams: { expired: '1' } });
        }
        throw apiErrorToMessage(err);
      }
      throw err;
    })
  );
};

export class ApiClientError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly fieldErrors: string[]
  ) {
    super(message);
  }
}

export function apiErrorToMessage(err: HttpErrorResponse): ApiClientError {
  if (err.status === 0) {
    return new ApiClientError('Cannot reach the server. Is the API running?', 0, []);
  }
  const envelope = err.error as Partial<ApiError> | null;
  if (envelope && typeof envelope === 'object' && envelope.success === false && typeof envelope.message === 'string') {
    return new ApiClientError(envelope.message, err.status, envelope.errors ?? []);
  }
  const fallback =
    err.status === 401 ? 'Authentication failed. Check your credentials.'
    : err.status === 403 ? 'You do not have permission to do that.'
    : err.status === 404 ? 'The requested item was not found.'
    : err.status === 409 ? 'That action conflicts with the current state.'
    : err.status >= 500 ? 'Server error. Please try again.'
    : 'Request failed. Please try again.';
  return new ApiClientError(fallback, err.status, []);
}
