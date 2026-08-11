export const POS_AGENT_ORIGIN = 'https://rms-pos-agent.localhost:5001' as const;

export const POS_AGENT_PATHS = {
  live: '/health/live',
  ready: '/health/ready',
  session: '/api/v1/session',
  mutationToken: '/api/v1/security/mutation-token'
} as const;

export const POS_AGENT_MUTATION_TOKEN_HEADER = 'X-RMS-Mutation-Token' as const;
