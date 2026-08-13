export const POS_AGENT_ORIGIN = 'https://rms-pos-agent.localhost:5001' as const;

export const POS_AGENT_PATHS = {
  live: '/health/live',
  ready: '/health/ready',
  session: '/api/v1/session',
  mutationToken: '/api/v1/security/mutation-token',
  deviceIdentity: '/api/v1/device/identity',
  deviceConnectivity: '/api/v1/device/connectivity',
  deviceCapabilities: '/api/v1/device/capabilities',
  configuration: '/api/v1/configuration',
  services: '/api/v1/services'
} as const;

export const POS_AGENT_MUTATION_TOKEN_HEADER = 'X-RMS-Mutation-Token' as const;
