import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { ModuleService } from './core/services/module.service';
import { errorEnvelopeInterceptor } from './core/interceptors/error-envelope.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([errorEnvelopeInterceptor])),
    // Loads /api/modules once, before the app renders, so ModuleService.modules()
    // is never a stale hardcoded list (see remediation_plan.md B25).
    provideAppInitializer(() => inject(ModuleService).initialize())
  ]
};
