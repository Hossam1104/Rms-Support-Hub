import { Routes } from '@angular/router';

export const promptStudioRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./prompt-studio.component').then(m => m.PromptStudioComponent)
  },
  {
    path: 'bugs',
    loadComponent: () => import('./bug-refiner/bug-refiner.component').then(m => m.BugRefinerComponent)
  },
  {
    path: 'stories',
    loadComponent: () => import('./story-refiner/story-refiner.component').then(m => m.StoryRefinerComponent)
  },
  {
    path: 'test-cases',
    loadComponent: () => import('./test-case-generator/test-case-generator.component').then(m => m.TestCaseGeneratorComponent)
  }
];
