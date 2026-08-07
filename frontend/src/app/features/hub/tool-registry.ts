import { QaToolDefinition, TOOL_ROUTE_DATA } from '../../core/models';

/** Single source of truth for the three tools presented by the hub. */
export const QA_TOOL_REGISTRY = [
    {
        id: 'prompt-studio',
        ...TOOL_ROUTE_DATA.promptStudio,
        description: 'Generate and refine structured QA prompts for bugs, stories, and test cases.',
        route: '/tools/prompt-studio',
        icon: 'bi-magic',
        actionLabel: 'Open Prompt Studio',
        capabilities: ['Bug Refinement', 'Story Refinement', 'Test Cases'],
        availabilityMessage: 'Available for deterministic prompt generation.'
    },
    {
        id: 'online-orders',
        ...TOOL_ROUTE_DATA.onlineOrders,
        description: 'Monitor, search, review, and manage supported online order workflows.',
        route: '/tools/online-orders',
        icon: 'bi-bag-check',
        actionLabel: 'Open Online Orders',
        capabilities: ['Search', 'Monitoring', 'Order Requests'],
        availabilityMessage: 'Available for supported online-order workflows.'
    },
    {
        id: 'pos-maintenance',
        ...TOOL_ROUTE_DATA.posMaintenance,
        description: 'A future POS support workspace for approved diagnostics, backups, and service visibility.',
        route: '/tools/pos-maintenance',
        icon: 'bi-pc-display',
        actionLabel: 'View Status',
        capabilities: ['Diagnostics', 'Backup', 'Services'],
        availabilityMessage: 'The POS Maintenance Tool is under development and will be integrated when ready.'
    }
] as const satisfies readonly QaToolDefinition[];