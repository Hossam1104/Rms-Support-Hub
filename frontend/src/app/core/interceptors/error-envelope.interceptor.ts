import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ToastService } from '../services/toast.service';
import { ApiError } from '../models';

/**
 * Unwraps the R6 uniform error envelope (`{ error: { code, message, details } }`,
 * see docs/api-spec.md and remediation_plan.md B22) into a typed ApiError and
 * shows it via ToastService -- the single place HTTP failures are surfaced
 * to the user. Individual call sites no longer need their own
 * `error: () => toast.showError(...)` handlers for HTTP failures; a `next`
 * handler is still required for legitimate non-error outcomes (e.g. a
 * lookup's 200 `{ success: false }` "not found" result, which never reaches
 * this interceptor).
 */
export const errorEnvelopeInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);

  return next(req).pipe(
    catchError((err: unknown) => {
      const apiError = toApiError(err);
      toast.showError(apiError.message);
      return throwError(() => apiError);
    })
  );
};

function toApiError(err: unknown): ApiError {
  if (!(err instanceof HttpErrorResponse)) {
    return { status: 0, code: 'unknown_error', message: 'An unexpected error occurred.' };
  }

  const body = err.error as {
    error?: { code?: string; message?: string; details?: unknown };
    success?: boolean;
    errors?: unknown;
  } | null;

  if (body && typeof body === 'object') {
    if (body.error) {
      return {
        status: err.status,
        code: body.error.code ?? 'unknown_error',
        message: body.error.message ?? err.message,
        details: body.error.details
      };
    }

    // U4 (UI_Rework_Plan.md §U4): send-request's contract validation failure
    // is a 400 carrying `{ success: false, errors: string[] }` (see
    // docs/api-spec.md). One concise toast here; the component maps the
    // errors (carried in details) to inline fields -- no stacked per-error
    // toasts and no duplicate component toast for the same failure.
    if (body.success === false && Array.isArray(body.errors)) {
      const errors = body.errors as string[];
      return {
        status: err.status,
        code: 'validation_failed',
        message: `Order validation failed with ${errors.length} issue(s).`,
        details: errors
      };
    }
  }

  return {
    status: err.status,
    code: 'unknown_error',
    message: err.error?.message || err.message || 'An unexpected error occurred.',
    details: err.error
  };
}
