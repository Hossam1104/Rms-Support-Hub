import { HttpBackend, HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, Observable, throwError } from 'rxjs';
import { components } from './generated/pos-agent-api.generated';
import { classifyPosAgentError } from './pos-agent-error';
import { POS_AGENT_ORIGIN, POS_AGENT_PATHS } from './pos-agent.constants';

type HealthStatus = components['schemas']['HealthStatusDto'];
type SessionInfo = components['schemas']['SessionInfoDto'];
type MutationTokenIssueRequest = components['schemas']['MutationTokenIssueRequestDto'];
type MutationTokenIssueResponse = components['schemas']['MutationTokenIssueResponseDto'];

@Injectable({ providedIn: 'root' })
export class PosAgentTransportService {
  private readonly http = new HttpClient(inject(HttpBackend));
  private readonly jsonHeaders = new HttpHeaders({ Accept: 'application/json' });

  getLive(): Observable<HealthStatus> {
    return this.http
      .get<HealthStatus>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.live}`, { headers: this.jsonHeaders })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getReady(): Observable<HealthStatus> {
    return this.http
      .get<HealthStatus>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.ready}`, { headers: this.jsonHeaders })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getSession(): Observable<SessionInfo> {
    return this.http
      .get<SessionInfo>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.session}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  issueMutationToken(operationId: string): Observable<MutationTokenIssueResponse> {
    const body: MutationTokenIssueRequest = { operationId };
    return this.http
      .post<MutationTokenIssueResponse>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.mutationToken}`, body, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }
}
