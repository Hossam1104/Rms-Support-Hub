import { ToastService } from './toast.service';

describe('ToastService', () => {
  let service: ToastService;

  beforeEach(() => {
    vi.useFakeTimers();
    service = new ToastService();
  });

  afterEach(() => {
    service.clearAll();
    vi.useRealTimers();
  });

  it('keeps at most three visible and queues overflow', () => {
    service.show('One', 'info', 0);
    service.show('Two', 'info', 0);
    service.show('Three', 'info', 0);
    service.show('Four', 'info', 0);
    service.show('Five', 'info', 0);

    expect(service.toasts().map(toast => toast.message)).toEqual(['One', 'Two', 'Three']);
    expect(service.queued().map(toast => toast.message)).toEqual(['Four', 'Five']);
  });

  it('collapses consecutive duplicates and increments the counter', () => {
    const id = service.show('Same failure', 'error', 0);
    service.show('Same failure', 'error', 0);
    service.show('Same failure', 'error', 0);
    service.show('Different message', 'error', 0);
    service.show('Third message', 'info', 0);
    service.show('Same failure', 'error', 0);

    expect(service.toasts()[0]).toMatchObject({ id, message: 'Same failure', type: 'error', count: 3 });
    expect(service.toasts()[1]).toMatchObject({ message: 'Different message', count: 1 });
    expect(service.queued()[0]).toMatchObject({ message: 'Same failure', count: 1 });
  });

  it('auto-dismisses after the requested duration', () => {
    const id = service.show('Temporary', 'success', 100);

    vi.advanceTimersByTime(99);
    expect(service.toasts().some(toast => toast.id === id)).toBe(true);

    vi.advanceTimersByTime(1);
    expect(service.toasts().some(toast => toast.id === id)).toBe(false);
  });

  it('pauses and resumes timed dismissal', () => {
    const id = service.show('Pause me', 'warning', 100);

    vi.advanceTimersByTime(40);
    service.pause(id);
    vi.advanceTimersByTime(100);
    expect(service.toasts().some(toast => toast.id === id)).toBe(true);

    service.resume(id);
    vi.advanceTimersByTime(59);
    expect(service.toasts().some(toast => toast.id === id)).toBe(true);
    vi.advanceTimersByTime(1);
    expect(service.toasts().some(toast => toast.id === id)).toBe(false);
  });

  it('promotes the queued toast when a visible toast closes', () => {
    service.show('One', 'info', 0);
    service.show('Two', 'info', 0);
    service.show('Three', 'info', 0);
    const queuedId = service.show('Queued', 'success', 0);
    const firstId = service.toasts()[0].id;

    service.remove(firstId);

    expect(service.toasts()).toHaveLength(3);
    expect(service.toasts()[2].id).toBe(queuedId);
    expect(service.queued()).toHaveLength(0);
  });

  it('removes a queued toast without disturbing visible items', () => {
    service.show('Visible', 'info', 0);
    const queuedId = service.show('Queued', 'info', 0);
    service.show('Third', 'info', 0);
    service.show('Fourth', 'info', 0);

    service.remove(queuedId);

    expect(service.toasts().map(toast => toast.message)).toEqual(['Visible', 'Third', 'Fourth']);
    expect(service.queued()).toEqual([]);
  });
});
