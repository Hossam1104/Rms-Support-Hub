import { Component, Input, Output, EventEmitter, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { JsonViewerComponent } from '../../../shared/components/json-viewer/json-viewer.component';
import { SendOrderResult } from '../../../core/models';

@Component({
  selector: 'app-api-config',
  standalone: true,
  imports: [CommonModule, FormsModule, JsonViewerComponent],
  template: `
    <div class="card-section glass-card">
      <div class="card-title">
        <i class="bi bi-send"></i>
        <span>API Request Execution</span>
      </div>

      <div class="form-group mb-3">
        <label class="form-label">Target API URL</label>
        <div class="input-group">
          <input type="text" class="glass-input" [(ngModel)]="targetUrl" placeholder="Enter target endpoint URL..." />
          <button type="button" class="glass-button" [disabled]="loading()" (click)="onSend()">
            <i class="bi" [class.bi-send]="!loading()" [class.bi-arrow-repeat]="loading()" [class.spin]="loading()"></i>
            <span>{{ loading() ? 'Sending...' : 'Send Order Request' }}</span>
          </button>
        </div>
      </div>

      <div class="json-preview mb-4">
        <app-json-viewer [data]="compiledJson" title="Compiled JSON Payload"></app-json-viewer>
      </div>

      <div class="response-section" *ngIf="apiResponse">
        <div class="response-header">
          <span>API Response</span>
          <span class="badge" [class.badge-success]="apiResponse.success" [class.badge-danger]="!apiResponse.success">
            Status: {{ apiResponse.statusCode }}
          </span>
        </div>
        <app-json-viewer [data]="apiResponse.responseText" title="Raw Response Body"></app-json-viewer>
      </div>
    </div>
  `,
  styles: [`
    .card-section { padding: 24px; margin-bottom: 24px; }
    .card-title { display: flex; align-items: center; gap: 10px; font-size: 1.1rem; font-weight: 600; margin-bottom: 20px; color: var(--text-primary); }
    .card-title i { color: var(--primary); }
    .input-group { display: flex; gap: 10px; }
    .input-group input { flex: 1; }
    .spin { animation: spin 1s infinite linear; }
    @keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
    .response-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; font-weight: 600; }
    .badge { padding: 4px 8px; border-radius: var(--radius-sm); font-size: 0.8rem; }
    .badge-success { background: var(--success-bg); color: var(--success); }
    .badge-danger { background: var(--danger-bg); color: var(--danger); }
  `]
})
export class ApiConfigComponent {
  @Input() targetUrl: string = '';
  @Input() compiledJson: Record<string, unknown> | null = null;
  @Input() apiResponse: SendOrderResult | null = null;
  @Output() sendRequest = new EventEmitter<{ url: string }>();

  loading = signal<boolean>(false);

  onSend() {
    this.loading.set(true);
    this.sendRequest.emit({ url: this.targetUrl });
    setTimeout(() => this.loading.set(false), 2500);
  }
}
