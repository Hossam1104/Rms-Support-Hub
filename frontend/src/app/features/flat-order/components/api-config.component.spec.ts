import { TestBed } from '@angular/core/testing';
import { ApiConfigComponent } from './api-config.component';
import { EnvironmentDto, ModuleEndpoint } from '../../../core/models';

const testingEnv = {
  key: 'UPC Testing',
  environment: 'Testing'
} as EnvironmentDto;

const endpoint: ModuleEndpoint = {
  environmentKey: 'UPC Testing',
  environment: 'Testing',
  apiUrl: 'http://10.10.10.181:8080/RmsMainServerApi/api/Order/CreateAndAssignOrder'
};

describe('ApiConfigComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ApiConfigComponent]
    }).compileComponents();
  });

  function createFixture() {
    const fixture = TestBed.createComponent(ApiConfigComponent);
    fixture.componentRef.setInput('endpoint', endpoint);
    fixture.componentRef.setInput('environment', testingEnv);
    fixture.detectChanges();
    return fixture;
  }

  it('shows the active environment endpoint read-only', () => {
    const fixture = createFixture();
    const input = fixture.nativeElement.querySelector('#endpoint-url') as HTMLInputElement;

    expect(input.value).toBe(endpoint.apiUrl);
    expect(input.readOnly).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('UPC Testing');
  });

  it('marks Testing clearly and Production distinctly', () => {
    const fixture = createFixture();
    expect(fixture.nativeElement.textContent).toContain('TEST');

    fixture.componentRef.setInput('environment', { key: 'UPC Production', environment: 'Production' } as EnvironmentDto);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('PROD');
  });

  it('does not expose a custom endpoint control for Production', () => {
    const fixture = createFixture();
    const sent: void[] = [];
    fixture.componentInstance.sendRequest.subscribe(e => sent.push(e));

    fixture.componentRef.setInput('environment', { key: 'UPC Production', environment: 'Production' } as EnvironmentDto);
    fixture.detectChanges();
    fixture.componentInstance.onSend();

    expect(fixture.nativeElement.textContent).not.toContain('Use custom endpoint');
    expect(sent.length).toBe(1);
  });

  it('emits only the server-resolved send action', () => {
    const fixture = createFixture();
    const sent: void[] = [];
    fixture.componentInstance.sendRequest.subscribe(e => sent.push(e));

    fixture.componentInstance.onSend();

    expect(sent.length).toBe(1);
    expect(fixture.nativeElement.querySelector('input[placeholder*="custom"]')).toBeNull();
  });

  it('drives the button state from the lifecycle loading input, not a timer', () => {
    const fixture = createFixture();

    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('.send-button') as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    expect(button.textContent).toContain('Sending...');

    fixture.componentRef.setInput('loading', false);
    fixture.detectChanges();
    expect(button.disabled).toBe(false);
    expect(button.textContent).not.toContain('Sending...');
  });

  it('renders the validation issue count and global errors', () => {
    const fixture = createFixture();

    fixture.componentRef.setInput('validationSummary', {
      totalCount: 3,
      globalErrors: ['An unmapped server error.']
    });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('3 validation issue(s)');
    expect(text).toContain('An unmapped server error.');
  });
});
