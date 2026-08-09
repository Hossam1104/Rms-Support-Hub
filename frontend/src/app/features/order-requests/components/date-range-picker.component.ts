import { CommonModule } from '@angular/common';
import {
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  computed,
  inject,
  signal
} from '@angular/core';

export interface DateRangeSelection {
  dateFrom: string | null;
  dateTo: string | null;
}

type PresetKey = 'today' | 'month' | '7d' | '30d';

interface DatePreset {
  key: PresetKey;
  label: string;
  description: string;
}

interface CalendarDay {
  iso: string;
  label: string;
  day: number;
  inCurrentMonth: boolean;
  isToday: boolean;
  isStart: boolean;
  isEnd: boolean;
  isInRange: boolean;
}

const WEEKDAYS = ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'];
const PRESETS: DatePreset[] = [
  { key: 'today', label: 'Today', description: 'Only today' },
  { key: 'month', label: 'This month', description: 'From the 1st to today' },
  { key: '7d', label: 'Last 7 days', description: 'Including today' },
  { key: '30d', label: 'Last 30 days', description: 'Including today' }
];

function toDateInputValue(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function fromDateInputValue(value: string | null): Date | null {
  if (!value) return null;
  const [year, month, day] = value.split('-').map(Number);
  if (![year, month, day].every(Number.isFinite)) return null;
  return new Date(year, month - 1, day);
}

function monthStart(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

function formatDate(date: Date): string {
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  }).format(date);
}

function formatMonth(date: Date): string {
  return new Intl.DateTimeFormat('en-US', {
    month: 'long',
    year: 'numeric'
  }).format(date);
}

function sameDate(left: string | null, right: string | null): boolean {
  return !!left && !!right && left === right;
}

function buildCalendarDays(viewMonth: Date, dateFrom: string | null, dateTo: string | null): CalendarDay[] {
  const month = monthStart(viewMonth);
  const firstGridDay = new Date(month.getFullYear(), month.getMonth(), 1 - month.getDay());
  const today = toDateInputValue(new Date());
  const days: CalendarDay[] = [];

  for (let index = 0; index < 42; index++) {
    const date = new Date(firstGridDay.getFullYear(), firstGridDay.getMonth(), firstGridDay.getDate() + index);
    const iso = toDateInputValue(date);
    days.push({
      iso,
      label: formatDate(date),
      day: date.getDate(),
      inCurrentMonth: date.getFullYear() === month.getFullYear() && date.getMonth() === month.getMonth(),
      isToday: iso === today,
      isStart: sameDate(iso, dateFrom),
      isEnd: sameDate(iso, dateTo),
      isInRange: !!dateFrom && !!dateTo && iso > dateFrom && iso < dateTo
    });
  }

  return days;
}

@Component({
  selector: 'app-date-range-picker',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="date-picker-shell">
      <button
        type="button"
        class="date-trigger"
        [class.is-open]="open()"
        aria-haspopup="dialog"
        [attr.aria-expanded]="open()"
        aria-label="Choose order request date range"
        (click)="togglePicker()">
        <span class="date-trigger-icon" aria-hidden="true"><i class="bi bi-calendar3"></i></span>
        <span class="date-trigger-copy">
          <span class="date-trigger-label">Date range</span>
          <strong>{{ selectionLabel() }}</strong>
        </span>
        <span class="date-trigger-meta">{{ presetLabel() }}</span>
        <i class="bi bi-chevron-down date-trigger-chevron" aria-hidden="true"></i>
      </button>

      @if (open()) {
        <div class="date-popover" role="dialog" aria-label="Order request date range picker">
          <div class="date-popover-header">
            <div>
              <span class="date-popover-eyebrow">Time window</span>
              <strong>{{ selectionLabel() }}</strong>
              <span class="date-popover-hint" aria-live="polite">{{ selectionHint() }}</span>
            </div>
            <button type="button" class="date-icon-button" aria-label="Close date picker" (click)="closePicker()">
              <i class="bi bi-x-lg" aria-hidden="true"></i>
            </button>
          </div>

          <div class="date-popover-body">
            <aside class="date-presets" aria-label="Date presets">
              <span class="date-section-label">Presets</span>
              @for (preset of presets; track preset.key) {
                <button
                  type="button"
                  class="date-preset"
                  [class.is-active]="isPresetActive(preset.key)"
                  (click)="selectPreset(preset.key)">
                  <span>{{ preset.label }}</span>
                  <small>{{ preset.description }}</small>
                </button>
              }
            </aside>

            <div class="date-calendar">
              <div class="calendar-toolbar">
                <button type="button" class="date-icon-button" aria-label="Previous month" (click)="previousMonth()">
                  <i class="bi bi-chevron-left" aria-hidden="true"></i>
                </button>
                <strong>{{ monthLabel() }}</strong>
                <button type="button" class="date-icon-button" aria-label="Next month" (click)="nextMonth()">
                  <i class="bi bi-chevron-right" aria-hidden="true"></i>
                </button>
              </div>

              <div class="calendar-weekdays" role="row">
                @for (weekday of weekdays; track weekday) {
                  <span role="columnheader">{{ weekday }}</span>
                }
              </div>

              <div class="calendar-grid" role="grid" aria-label="Calendar days">
                @for (day of calendarDays(); track day.iso) {
                  <button
                    type="button"
                    class="calendar-day"
                    [class.is-outside]="!day.inCurrentMonth"
                    [class.is-today]="day.isToday"
                    [class.is-in-range]="day.isInRange"
                    [class.is-start]="day.isStart"
                    [class.is-end]="day.isEnd"
                    [attr.aria-label]="day.label"
                    [attr.aria-selected]="day.isStart || day.isEnd"
                    role="gridcell"
                    (click)="selectDay(day.iso)">
                    {{ day.day }}
                  </button>
                }
              </div>
            </div>
          </div>

          <div class="date-popover-footer">
            <button type="button" class="date-clear-button" (click)="clearSelection()">Clear dates</button>
            <div class="date-footer-actions">
              <button type="button" class="date-cancel-button" (click)="closePicker()">Cancel</button>
              <button
                type="button"
                class="date-apply-button"
                [disabled]="!draftFrom() || !draftTo()"
                (click)="applySelection()">
                Apply range
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .date-picker-shell { position: relative; min-width: 0; }
    .date-trigger {
      display: grid;
      grid-template-columns: auto minmax(0, 1fr) auto auto;
      align-items: center;
      gap: 10px;
      width: 100%;
      min-height: var(--control-height);
      padding: 7px 11px;
      border: 1px solid var(--input-border);
      border-radius: var(--radius-md);
      background: var(--input-bg);
      color: var(--text-primary);
      text-align: left;
      cursor: pointer;
    }
    .date-trigger:hover,
    .date-trigger.is-open { border-color: var(--border-focus); background: var(--surface-hover); }
    .date-trigger.is-open { box-shadow: var(--focus-ring); }
    .date-trigger:focus-visible,
    .date-icon-button:focus-visible,
    .date-preset:focus-visible,
    .calendar-day:focus-visible,
    .date-clear-button:focus-visible,
    .date-cancel-button:focus-visible,
    .date-apply-button:focus-visible { outline: none; box-shadow: var(--focus-ring); }
    .date-trigger-icon {
      display: inline-grid;
      place-items: center;
      width: 32px;
      height: 32px;
      border-radius: var(--radius-sm);
      background: var(--accent-soft);
      color: var(--text-accent);
      font-size: .9rem;
    }
    .date-trigger-copy { display: grid; gap: 1px; min-width: 0; }
    .date-trigger-label,
    .date-popover-eyebrow,
    .date-section-label {
      color: var(--text-muted);
      font-size: .66rem;
      font-weight: 800;
      letter-spacing: .08em;
      text-transform: uppercase;
    }
    .date-trigger-copy strong {
      overflow: hidden;
      color: var(--text-primary);
      font-size: .78rem;
      font-weight: 800;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .date-trigger-meta {
      padding: 4px 8px;
      border: 1px solid var(--border-subtle);
      border-radius: var(--radius-pill);
      background: var(--surface-selected);
      color: var(--text-accent);
      font-size: .65rem;
      font-weight: 800;
      white-space: nowrap;
    }
    .date-trigger-chevron { color: var(--text-muted); font-size: .7rem; }
    .date-popover {
      position: absolute;
      top: calc(100% + 10px);
      right: 0;
      z-index: 100;
      width: min(620px, calc(100vw - 32px));
      overflow: hidden;
      border: 1px solid var(--border-strong);
      border-radius: var(--radius-lg);
      background: var(--surface-overlay);
      box-shadow: var(--shadow-lg);
    }
    .date-popover-header,
    .date-popover-footer {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--panel-gap);
      padding: 14px 16px;
    }
    .date-popover-header { border-bottom: 1px solid var(--divider); }
    .date-popover-header > div { display: grid; gap: 3px; min-width: 0; }
    .date-popover-header strong { color: var(--text-primary); font-size: .92rem; }
    .date-popover-hint { color: var(--text-muted); font-size: .72rem; }
    .date-icon-button {
      display: inline-grid;
      flex: 0 0 auto;
      place-items: center;
      width: 30px;
      height: 30px;
      border: 1px solid var(--border-subtle);
      border-radius: var(--radius-sm);
      background: var(--surface-interactive);
      color: var(--text-secondary);
      cursor: pointer;
    }
    .date-icon-button:hover { border-color: var(--border-focus); background: var(--surface-hover); color: var(--text-accent); }
    .date-popover-body { display: grid; grid-template-columns: 168px minmax(0, 1fr); }
    .date-presets {
      display: grid;
      align-content: start;
      gap: 6px;
      padding: 16px 12px;
      border-right: 1px solid var(--divider);
      background: var(--surface-raised);
    }
    .date-section-label { padding: 0 8px 4px; }
    .date-preset {
      display: grid;
      gap: 2px;
      width: 100%;
      padding: 9px 10px;
      border: 1px solid transparent;
      border-radius: var(--radius-md);
      background: transparent;
      color: var(--text-secondary);
      text-align: left;
      cursor: pointer;
    }
    .date-preset span { font-size: .76rem; font-weight: 800; }
    .date-preset small { color: var(--text-muted); font-size: .66rem; }
    .date-preset:hover,
    .date-preset.is-active { border-color: var(--border-focus); background: var(--surface-selected); color: var(--text-accent); }
    .date-preset.is-active small { color: var(--text-accent); opacity: .78; }
    .date-calendar { padding: 16px; }
    .calendar-toolbar { display: grid; grid-template-columns: 30px 1fr 30px; align-items: center; gap: 8px; margin-bottom: 12px; }
    .calendar-toolbar strong { color: var(--text-primary); font-size: .84rem; text-align: center; }
    .calendar-weekdays,
    .calendar-grid { display: grid; grid-template-columns: repeat(7, minmax(0, 1fr)); gap: 4px; }
    .calendar-weekdays { margin-bottom: 5px; }
    .calendar-weekdays span { color: var(--text-muted); font-size: .64rem; font-weight: 800; text-align: center; }
    .calendar-day {
      position: relative;
      display: grid;
      place-items: center;
      min-height: 32px;
      border: 1px solid transparent;
      border-radius: var(--radius-sm);
      background: transparent;
      color: var(--text-secondary);
      font: inherit;
      font-size: .72rem;
      font-weight: 750;
      cursor: pointer;
    }
    .calendar-day:hover { border-color: var(--border-focus); background: var(--surface-hover); color: var(--text-primary); }
    .calendar-day.is-outside { color: var(--text-disabled); }
    .calendar-day.is-today::after {
      position: absolute;
      right: 5px;
      bottom: 4px;
      width: 4px;
      height: 4px;
      border-radius: 50%;
      background: var(--accent);
      content: '';
    }
    .calendar-day.is-in-range { border-radius: 0; background: var(--surface-selected); color: var(--text-accent); }
    .calendar-day.is-start,
    .calendar-day.is-end {
      border-color: transparent;
      border-radius: var(--radius-sm);
      background: var(--grad-brand);
      box-shadow: var(--shadow-sm);
      color: var(--on-gradient);
    }
    .calendar-day.is-start::after,
    .calendar-day.is-end::after { background: currentColor; }
    .date-popover-footer { border-top: 1px solid var(--divider); background: var(--surface-raised); }
    .date-footer-actions { display: flex; align-items: center; gap: 8px; }
    .date-clear-button,
    .date-cancel-button,
    .date-apply-button {
      min-height: 32px;
      padding: 0 11px;
      border-radius: var(--radius-sm);
      font: inherit;
      font-size: .72rem;
      font-weight: 800;
      cursor: pointer;
    }
    .date-clear-button,
    .date-cancel-button { border: 1px solid var(--border-subtle); background: transparent; color: var(--text-secondary); }
    .date-clear-button:hover,
    .date-cancel-button:hover { border-color: var(--border-focus); color: var(--text-accent); }
    .date-apply-button { border: 1px solid transparent; background: var(--grad-brand); color: var(--on-gradient); }
    .date-apply-button:disabled { cursor: not-allowed; opacity: .45; }

    @media (max-width: 600px) {
      .date-trigger-meta { display: none; }
      .date-popover { position: fixed; top: auto; right: 16px; bottom: 16px; left: 16px; width: auto; max-height: calc(100vh - 32px); overflow-y: auto; }
      .date-popover-body { grid-template-columns: 1fr; }
      .date-presets { grid-template-columns: repeat(2, minmax(0, 1fr)); border-right: 0; border-bottom: 1px solid var(--divider); }
      .date-section-label { grid-column: 1 / -1; }
    }
  `]
})
export class DateRangePickerComponent implements OnChanges {
  @Input() dateFrom: string | null = null;
  @Input() dateTo: string | null = null;
  @Output() rangeChange = new EventEmitter<DateRangeSelection>();

  readonly weekdays = WEEKDAYS;
  readonly presets = PRESETS;
  readonly open = signal(false);
  readonly viewMonth = signal(monthStart(new Date()));
  readonly draftFrom = signal<string | null>(null);
  readonly draftTo = signal<string | null>(null);
  readonly calendarDays = computed(() => buildCalendarDays(this.viewMonth(), this.draftFrom(), this.draftTo()));
  readonly monthLabel = computed(() => formatMonth(this.viewMonth()));
  readonly selectionLabel = computed(() => this.formatSelection(this.draftFrom(), this.draftTo()));
  readonly selectionHint = computed(() => {
    if (this.draftFrom() && !this.draftTo()) return 'Choose an end date to complete the range.';
    if (!this.draftFrom()) return 'Choose a start date or use a preset.';
    return 'Ready to apply to the request list.';
  });

  private readonly host = inject(ElementRef<HTMLElement>);

  ngOnChanges(changes: SimpleChanges) {
    if (!this.open() && (changes['dateFrom'] || changes['dateTo'])) {
      this.draftFrom.set(this.dateFrom);
      this.draftTo.set(this.dateTo);
    }
  }

  togglePicker() {
    if (this.open()) {
      this.closePicker();
      return;
    }

    const today = new Date();
    this.draftFrom.set(this.dateFrom);
    this.draftTo.set(this.dateTo);
    this.viewMonth.set(monthStart(fromDateInputValue(this.dateFrom) || today));
    this.open.set(true);
  }

  closePicker() { this.open.set(false); }

  previousMonth() {
    const current = this.viewMonth();
    this.viewMonth.set(new Date(current.getFullYear(), current.getMonth() - 1, 1));
  }

  nextMonth() {
    const current = this.viewMonth();
    this.viewMonth.set(new Date(current.getFullYear(), current.getMonth() + 1, 1));
  }

  selectDay(iso: string) {
    const from = this.draftFrom();
    const to = this.draftTo();

    if (!from || to) {
      this.draftFrom.set(iso);
      this.draftTo.set(null);
      this.viewMonth.set(monthStart(fromDateInputValue(iso)!));
      return;
    }

    if (iso < from) {
      this.draftFrom.set(iso);
      this.draftTo.set(from);
    } else {
      this.draftTo.set(iso);
    }
  }

  selectPreset(key: PresetKey) {
    const today = new Date();
    const to = toDateInputValue(today);
    const from = new Date(today);

    if (key === 'today') {
      this.draftFrom.set(to);
    } else {
      if (key === 'month') from.setDate(1);
      if (key === '7d') from.setDate(from.getDate() - 6);
      if (key === '30d') from.setDate(from.getDate() - 29);
      this.draftFrom.set(toDateInputValue(from));
    }

    this.draftTo.set(to);
    this.viewMonth.set(monthStart(from));
  }

  isPresetActive(key: PresetKey): boolean {
    const range = this.presetRange(key);
    return this.draftFrom() === range.dateFrom && this.draftTo() === range.dateTo;
  }

  presetLabel(): string {
    const preset = this.presets.find(item => this.isPresetActive(item.key));
    return preset?.label || 'Custom range';
  }

  applySelection() {
    if (!this.draftFrom() || !this.draftTo()) return;
    this.rangeChange.emit({ dateFrom: this.draftFrom(), dateTo: this.draftTo() });
    this.closePicker();
  }

  clearSelection() {
    this.rangeChange.emit({ dateFrom: null, dateTo: null });
    this.closePicker();
  }

  @HostListener('document:pointerdown', ['$event'])
  onDocumentPointerDown(event: PointerEvent) {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) this.closePicker();
  }

  @HostListener('document:keydown.escape')
  onEscape() {
    if (this.open()) this.closePicker();
  }

  private presetRange(key: PresetKey): DateRangeSelection {
    const today = new Date();
    const to = toDateInputValue(today);
    const from = new Date(today);
    if (key === 'month') from.setDate(1);
    if (key === '7d') from.setDate(from.getDate() - 6);
    if (key === '30d') from.setDate(from.getDate() - 29);
    return { dateFrom: key === 'today' ? to : toDateInputValue(from), dateTo: to };
  }

  private formatSelection(dateFrom: string | null, dateTo: string | null): string {
    const from = fromDateInputValue(dateFrom);
    const to = fromDateInputValue(dateTo);
    if (from && to) return [formatDate(from), formatDate(to)].join(' - ');
    if (!from && !to) return 'Any date';
    if (from && !to) return `From ${formatDate(from)}`;
    if (!from || !to) return 'Choose dates';
    return `${formatDate(from)} – ${formatDate(to)}`;
  }
}
