$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot '..\PosTestingConfiguration.psm1') -Force

Describe 'INT-13D secure Testing origin configuration' {
    It 'returns one canonical exact origin and Agent origin' {
        $configuration = Get-PosTestingConfiguration

        $configuration.SupportHubOrigin | Should Be 'https://support-hub.integration.test:4443'
        $configuration.SupportHubHost | Should Be 'support-hub.integration.test'
        $configuration.SupportHubPort | Should Be 4443
        $configuration.AgentOrigin | Should Be 'https://rms-pos-agent.localhost:5001'
    }

    It 'normalizes only the exact configured origin' {
        (Get-PosTestingConfiguration ' HTTPS://SUPPORT-HUB.INTEGRATION.TEST:4443/ ').SupportHubOrigin |
            Should Be 'https://support-hub.integration.test:4443'
    }

    It 'rejects a different host, port, scheme, path, or wildcard' {
        foreach ($origin in @(
                'https://other.integration.test:4443',
                'https://support-hub.integration.test:443',
                'http://support-hub.integration.test:4443',
                'https://support-hub.integration.test:4443/tools',
                'https://*.integration.test:4443')) {
            { Get-PosTestingConfiguration $origin } | Should Throw
        }
    }
}
