/** Typed route metadata for the QA Support Hub shell (Session 01). Attached
 * to each tool route through Route.data so the shell, breadcrumbs, and hub
 * dashboard all read one consistent contract. */
export type ToolStatus = 'available' | 'migration-pending';

/** Accent keys map to gradient tokens in styles/_gradients.css. Components
 * consume the key, never a raw color (design-token guardrail). */
export type ToolAccent = 'brand' | 'info' | 'amber';

export interface ToolRouteData {
  /** Full tool name shown in page headers. */
  title: string;
  /** Short label used in breadcrumbs. */
  breadcrumb: string;
  status: ToolStatus;
  accent: ToolAccent;
}

/** Single source of truth for tool identity; spread into Route.data. */
export const TOOL_ROUTE_DATA = {
  promptStudio: {
    title: 'QA Prompt Studio',
    breadcrumb: 'Prompt Studio',
    status: 'migration-pending',
    accent: 'brand'
  },
  onlineOrders: {
    title: 'Online Orders',
    breadcrumb: 'Online Orders',
    status: 'available',
    accent: 'info'
  },
  posMaintenance: {
    title: 'POS Maintenance',
    breadcrumb: 'POS Maintenance',
    status: 'migration-pending',
    accent: 'amber'
  }
} as const satisfies Record<string, ToolRouteData>;
