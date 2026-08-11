import { HttpErrorResponse } from '@angular/common/http';

export type PosAgentErrorKind =
  | 'transportUnavailableOrBlocked'
  | 'authenticationRequired'
  | 'notAuthorized'
  | 'originRejected'
  | 'unsupportedOperation'
  | 'contractMismatch'
  | 'agentServerError'
  | 'unknown';

export interface PosAgentProblemDetails {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly code?: string;
  readonly correlationId?: string;
}

export class PosAgentTransportError extends Error {
  override readonly name = 'PosAgentTransportError';

  constructor(
    readonly kind: PosAgentErrorKind,
    readonly status: number,
    message: string,
    readonly problem?: PosAgentProblemDetails
  ) {
    super(message);
  }

  get code(): string | undefined {
    return this.problem?.code;
  }

  get correlationId(): string | undefined {
    return this.problem?.correlationId;
  }
}

export function classifyPosAgentError(error: unknown): PosAgentTransportError {
  if (error instanceof PosAgentTransportError) {
    return error;
  }

  if (!(error instanceof HttpErrorResponse)) {
    return new PosAgentTransportError('unknown', 0, 'The POS Agent request failed.');
  }

  const problem = readProblemDetails(error.error);
  const status = error.status;

  if (status === 0) {
    return new PosAgentTransportError(
      'transportUnavailableOrBlocked',
      status,
      'The POS Agent could not be reached or the browser blocked the request.',
      problem
    );
  }

  if (status === 401) {
    return new PosAgentTransportError(
      'authenticationRequired',
      status,
      'Windows authentication is required for the POS Agent.',
      problem
    );
  }

  if (status === 403) {
    const kind = problem?.code === 'origin_rejected' ? 'originRejected' : 'notAuthorized';
    return new PosAgentTransportError(kind, status, messageFor(kind), problem);
  }

  if (status === 400 && problem?.code === 'operation_not_supported') {
    return new PosAgentTransportError('unsupportedOperation', status, messageFor('unsupportedOperation'), problem);
  }

  if (status === 409 || status === 422 || status === 400) {
    return new PosAgentTransportError('contractMismatch', status, 'The POS Agent contract rejected the request.', problem);
  }

  if (status >= 500 && status <= 599) {
    return new PosAgentTransportError('agentServerError', status, 'The POS Agent reported a server error.', problem);
  }

  return new PosAgentTransportError('unknown', status, 'The POS Agent request failed.', problem);
}

function readProblemDetails(value: unknown): PosAgentProblemDetails | undefined {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    return undefined;
  }

  const candidate = value as Record<string, unknown>;
  return {
    type: typeof candidate['type'] === 'string' ? candidate['type'] : undefined,
    title: typeof candidate['title'] === 'string' ? candidate['title'] : undefined,
    status: typeof candidate['status'] === 'number' ? candidate['status'] : undefined,
    code: typeof candidate['code'] === 'string' ? candidate['code'] : undefined,
    correlationId: typeof candidate['correlationId'] === 'string' ? candidate['correlationId'] : undefined
  };
}

function messageFor(kind: PosAgentErrorKind): string {
  switch (kind) {
    case 'originRejected':
      return 'The Support Hub origin is not accepted by the POS Agent.';
    case 'unsupportedOperation':
      return 'The requested POS Agent operation is not supported.';
    default:
      return 'The POS Agent request was not authorized.';
  }
}
