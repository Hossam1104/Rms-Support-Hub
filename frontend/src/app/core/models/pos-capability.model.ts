export type PosCapabilityStatus =
    | 'pending'
    | 'read-only'
    | 'state-changing'
    | 'destructive'
    | 'unavailable';

export interface PosCapability {
    id: string;
    title: string;
    description: string;
    icon: string;
    status: PosCapabilityStatus;
    examples: readonly string[];
}

export const POS_CAPABILITIES: readonly PosCapability[] = [
    {
        id: 'diagnostics',
        title: 'Diagnostics',
        description: 'Future support views for understanding the POS application and its environment.',
        icon: 'bi-activity',
        status: 'pending',
        examples: [
            'Application and environment information',
            'Connectivity status',
            'Configured endpoint visibility',
            'Local service state',
            'Support diagnostics'
        ]
    },
    {
        id: 'backup-restore',
        title: 'Backup & Restore',
        description: 'Future approved workflows for protecting and recovering POS data and configuration.',
        icon: 'bi-database-check',
        status: 'pending',
        examples: [
            'Approved database backup workflows',
            'Configuration backup',
            'Restore workflow'
        ]
    },
    {
        id: 'configuration',
        title: 'Configuration',
        description: 'Future controlled views and edits for approved POS identity and environment settings.',
        icon: 'bi-sliders2-vertical',
        status: 'pending',
        examples: [
            'Approved configuration viewing and editing',
            'Branch and POS identity',
            'Environment information'
        ]
    },
    {
        id: 'windows-services',
        title: 'Windows Services',
        description: 'Future allow-listed service visibility and controlled lifecycle workflows.',
        icon: 'bi-gear-wide-connected',
        status: 'pending',
        examples: [
            'Approved Windows service status',
            'Controlled start, stop, and restart'
        ]
    },
    {
        id: 'environment-connectivity',
        title: 'Environment / Connectivity',
        description: 'Future support checks for machine reachability and dependency health.',
        icon: 'bi-diagram-3',
        status: 'pending',
        examples: [
            'Server reachability',
            'Environment detection',
            'Dependency status'
        ]
    }
] as const;
