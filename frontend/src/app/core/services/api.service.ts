import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/** Query-string values HttpParams accepts natively; undefined/null entries
 * are dropped rather than serialized as the literal string "undefined".
 * Array values are sent as repeated `key=a&key=b` params -- how ASP.NET
 * Core model-binds an `int[]`/`string[]` query parameter (e.g.
 * OrderRequestListQuery.Statuses for R9's multi-select status chips). */
export type ApiParamValue = string | number | boolean | undefined | null;
export type ApiParams = Record<string, ApiParamValue | ApiParamValue[]>;

function toHttpParams(params?: ApiParams): HttpParams {
  let httpParams = new HttpParams();
  if (!params) return httpParams;
  for (const [key, value] of Object.entries(params)) {
    if (Array.isArray(value)) {
      for (const entry of value) {
        if (entry !== undefined && entry !== null) {
          httpParams = httpParams.append(key, entry);
        }
      }
    } else if (value !== undefined && value !== null) {
      httpParams = httpParams.set(key, value);
    }
  }
  return httpParams;
}

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl || '/api';

  get<T>(path: string, params?: ApiParams): Observable<T> {
    return this.http.get<T>(`${this.baseUrl}/${path}`, { params: toHttpParams(params) });
  }

  post<T>(path: string, body: unknown, params?: ApiParams): Observable<T> {
    return this.http.post<T>(`${this.baseUrl}/${path}`, body, { params: toHttpParams(params) });
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<T>(`${this.baseUrl}/${path}`, body);
  }

  delete<T>(path: string): Observable<T> {
    return this.http.delete<T>(`${this.baseUrl}/${path}`);
  }
}
