export const environment = {
  production: true,
  // Same-origin in production: the API and the built Angular bundle are
  // served by the same host (see cmd.md / README.md), so a relative path
  // avoids baking any hostname into the bundle (remediation_plan.md B26).
  apiUrl: '/api'
};
