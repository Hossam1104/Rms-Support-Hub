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
        description: 'Safe first-release views for understanding the local POS device and Agent environment.',
        icon: 'bi-activity',
        status: 'read-only',
        examples: [
            'Device identity and Agent capabilities',
            'Connectivity evidence',
            'Read-only support diagnostics'
        ]
    },
    {
        id: 'backup-restore',
        title: 'Backup & Restore',
        description: 'Approved workflows for protecting and recovering POS data and configuration.',
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
        description: 'Read-only view of safe POS identity and redacted environment settings.',
        icon: 'bi-sliders2-vertical',
        status: 'read-only',
        examples: [
            'Redacted configuration visibility',
            'Branch and POS identity',
            'Secret-presence flags only'
        ]
    },
    {
        id: 'windows-services',
        title: 'Windows Services',
        description: 'Allow-listed Windows service visibility without lifecycle controls.',
        icon: 'bi-gear-wide-connected',
        status: 'read-only',
        examples: [
            'Windows service status evidence',
            'No start, stop, or restart actions'
        ]
    },
    {
        id: 'environment-connectivity',
        title: 'Environment / Connectivity',
        description: 'Bounded support checks for machine reachability and dependency evidence.',
        icon: 'bi-diagram-3',
        status: 'read-only',
        examples: [
            'Server reachability evidence',
            'Environment detection',
            'Dependency status evidence'
        ]
    }
] as const;
