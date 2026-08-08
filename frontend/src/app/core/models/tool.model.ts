/** Typed route metadata for the RMS+ Support Hub shell (Session 01). Attached
 * to each tool route through Route.data so the shell and breadcrumbs share one
 * consistent contract. */
export type ToolId = 'prompt-studio' | 'online-orders' | 'pos-maintenance';
export type ToolStatus = 'available' | 'migration-pending';

/** Accent keys map to the `--tool-<accent>-from/to` identity tokens in
 * styles/_tokens.css. Components consume the key, never a raw color
 * (design-token guardrail). */
export type ToolAccent = 'brand' | 'info' | 'amber';

export interface ToolRouteData {
  /** Full tool name shown in page headers. */
  title: string;
  /** Short label used in breadcrumbs. */
  breadcrumb: string;
  status: ToolStatus;
  accent: ToolAccent;
}

/** Complete metadata contract used by the root dashboard registry. */
export interface QaToolDefinition extends ToolRouteData {
  id: ToolId;
  route: string;
  description: string;
  icon: string;
  actionLabel: string;
  capabilities: readonly string[];
  availabilityMessage: string;
}

/** Single source of truth for tool identity; spread into Route.data. */
export const TOOL_ROUTE_DATA = {
  promptStudio: {
    title: 'QA Prompt Studio',
    breadcrumb: 'Prompt Studio',
    status: 'available',
    accent: 'brand'
  },
  onlineOrders: {
    title: 'Online Order Tool',
    breadcrumb: 'Online Orders',
    status: 'available',
    accent: 'info'
  },
  posMaintenance: {
    title: 'POS Maintenance Tool',
    breadcrumb: 'POS Maintenance',
    status: 'migration-pending',
    accent: 'amber'
  }
} as const satisfies Record<string, ToolRouteData>;
