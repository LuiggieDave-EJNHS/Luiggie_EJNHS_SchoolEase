// ============================================================================
// SchoolEase load test (k6)
// ----------------------------------------------------------------------------
// Answers: "can the site handle 1,000 users at the same time?"
//
// k6 is free and open source:  https://k6.io  (winget install k6 --source winget)
//
// RUN IT AGAINST YOUR LOCAL MACHINE, NOT RENDER.
// Render's free tier forbids load testing (it is treated as abuse and the
// service can be suspended). Start the app locally first, then run k6:
//
//   dotnet run --project SchoolEase.Web --urls http://localhost:5199
//
//   k6 run `
//     -e BASE_URL=http://localhost:5199 `
//     -e SE_EMAIL=admin@schoolease.ph `
//     -e SE_PASSWORD="YourRealPassword" `
//     -e SE_PAGES="/,/Dashboard,/Students,/Announcements,/LearningActivitySheets" `
//     loadtest/schoolease.k6.js
//
//   # quick smoke run instead of the full ramp (about 30 seconds):
//   k6 run -e BASE_URL=http://localhost:5199 -e SE_EMAIL=... -e SE_PASSWORD=... `
//     -e SE_STAGES=smoke loadtest/schoolease.k6.js
//
// Use a dedicated, email-confirmed, active account. Each virtual user signs in
// once, so the run writes one audit-log row per virtual user.
// ============================================================================

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';

// ---------------------------------------------------------------- config ----
const BASE = (__ENV.BASE_URL || 'http://localhost:5199').replace(/\/+$/, '');
const EMAIL = __ENV.SE_EMAIL || '';
const PASSWORD = __ENV.SE_PASSWORD || '';

// Pages a signed-in account may open. Override with SE_PAGES (comma separated).
// Pick pages the account's role is allowed to see, otherwise every request 403s.
const PAGES = (__ENV.SE_PAGES || '/,/Dashboard,/Announcements,/LearningActivitySheets')
  .split(',')
  .map((p) => p.trim())
  .filter((p) => p.length > 0);

// 'full' = 10 -> 100 -> 500 -> 1000 virtual users. 'smoke' = quick sanity check.
const MODE = (__ENV.SE_STAGES || 'full').toLowerCase();

const STAGES = MODE === 'smoke'
  ? [
      { duration: '10s', target: 5 },
      { duration: '20s', target: 5 },
      { duration: '5s', target: 0 },
    ]
  : [
      { duration: '1m', target: 10 },   // warm up
      { duration: '2m', target: 100 },  // normal school peak
      { duration: '2m', target: 500 },  // heavy
      { duration: '3m', target: 1000 }, // worst case the teachers asked about
      { duration: '1m', target: 0 },    // ramp down
    ];

export const options = {
  scenarios: {
    browsing: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: STAGES,
      gracefulRampDown: '30s',
    },
  },
  thresholds: {
    // Fail the run when the experience degrades past these limits.
    http_req_failed: ['rate<0.01'],                       // < 1% errors
    http_req_duration: ['p(95)<800', 'p(99)<2500'],       // p95 < 0.8s, p99 < 2.5s
    login_ok: ['rate>0.99'],                              // sign-in must work
  },
  // The login page HTML is needed to read the anti-forgery token.
  discardResponseBodies: false,
};

// --------------------------------------------------------------- metrics ----
const loginDuration = new Trend('login_duration', true);
const loginOk = new Rate('login_ok');
const pageDuration = new Trend('page_duration', true);

// --------------------------------------------------------------- helpers ----
function readToken(html) {
  const match = html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/);
  return match ? match[1] : null;
}

/** Signs in once for this virtual user; the cookie jar is kept per VU. */
function signIn() {
  const loginPage = http.get(`${BASE}/Account/Login`);
  const token = readToken(loginPage.body || '');

  if (!token) {
    loginOk.add(false);
    return false;
  }

  const res = http.post(
    `${BASE}/Account/Login`,
    {
      Email: EMAIL,
      Password: PASSWORD,
      RememberMe: 'false',
      __RequestVerificationToken: token,
    },
    { redirects: 5 }
  );

  loginDuration.add(res.timings.duration);

  // A successful sign-in lands on the dashboard (not back on the login page).
  const landed = res.url.indexOf('/Account/Login') === -1;
  const ok = res.status === 200 && landed;
  loginOk.add(ok);
  check(res, { 'signed in': () => ok });

  return ok;
}

// ------------------------------------------------------------- scenarios ----
export default function () {
  // First iteration of every virtual user performs a real sign-in. After that
  // the session cookie is reused, which is how real users behave.
  if (__ITER === 0) {
    if (!signIn()) {
      sleep(1);
      return;
    }
  }

  const path = PAGES[(__VU + __ITER) % PAGES.length];
  const res = http.get(`${BASE}${path}`);

  pageDuration.add(res.timings.duration);
  check(res, {
    'page loads': (r) => r.status === 200,
  });

  // Think time so virtual users behave like people, not a flood.
  sleep(1 + Math.random() * 2);
}
