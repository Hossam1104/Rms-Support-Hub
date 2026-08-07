import { QaToolDefinition, TOOL_ROUTE_DATA } from '../../core/models';

/** Single source of truth for the three tools presented by the hub. */
export const QA_TOOL_REGISTRY = [
    {
        id: 'prompt-studio',
        ...TOOL_ROUTE_DATA.promptStudio,
        description: 'Generate and refine structured QA prompts for bugs, stories, and test cases.',
        route: '/tools/prompt-studio',
        icon: 'bi-magic',
        actionLabel: 'View Prompt Studio',
        capabilities: ['Bug Refinement', 'Story Refinement', 'Test Cases'],
        availabilityMessage: 'The Angular workspace migration is in progress.'
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
        description: 'Centralized POS diagnostics, configuration, backup, service, and maintenance workflows.',
        route: '/tools/pos-maintenance',
        icon: 'bi-pc-display',
        actionLabel: 'View Migration Status',
        capabilities: ['Diagnostics', 'Backup', 'Services'],
        availabilityMessage: 'The standalone POS source is required before migration can begin.'
    }
] as const satisfies readonly QaToolDefinition[];