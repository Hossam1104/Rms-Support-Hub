export const environment = {
  production: false,
  // Relative so `ng serve`'s proxy.conf.json (routing /api -> :5200) actually
  // intercepts these requests -- an absolute http://localhost:5200/api URL
  // would bypass the dev-server proxy entirely and depend on CORS instead.
  apiUrl: '/api'
};
