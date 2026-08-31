import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

// Tuki realistic API load test. High-frequency GPS stays local by design;
// this script only sends meaningful backend navigation events.
const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5129').replace(/\/+$/, '');
const PROFILE = (__ENV.PROFILE || 'smoke').toLowerCase();

const numberEnv = (name, fallback) => {
  const value = Number(__ENV[name]);
  return Number.isFinite(value) ? value : fallback;
};
const rateEnv = (name, fallback) => Math.min(1, Math.max(0, numberEnv(name, fallback)));
const boolEnv = (name, fallback = false) => {
  const value = __ENV[name];
  if (value === undefined || value === '') return fallback;
  return ['1', 'true', 'yes', 'y', 'on'].includes(String(value).toLowerCase());
};

const ALLOW_REMOTE_LOAD = boolEnv('ALLOW_REMOTE_LOAD');
const ENABLE_EXTERNALS = boolEnv('ENABLE_EXTERNALS');
// Stress/capacity tests disable backend-generated navigation speech AI by
// default. The backend recognizes X-Tuki-Disable-Ai and returns the existing
// deterministic navigation speech instead, so Gemini quota/latency is excluded
// without skipping the real navigation endpoints.
const DISABLE_SERVER_AI = boolEnv('DISABLE_SERVER_AI', true);
const REROUTE_RATE = rateEnv('REROUTE_RATE', 0.05);
const LOCATION_SYNC_RATE = rateEnv('LOCATION_SYNC_RATE', 0.05);
const ACTIVE_REFRESH_RATE = rateEnv('ACTIVE_REFRESH_RATE', 0.10);
const GOOGLE_MORE_RATE = rateEnv('GOOGLE_MORE_RATE', 0);
const AI_RATE = rateEnv('AI_RATE', 0);
const REROUTE_REASON = (__ENV.REROUTE_REASON || 'MANUAL').toUpperCase();
const NAV_HOLD_SECONDS = Math.max(0, numberEnv('NAV_HOLD_SECONDS', PROFILE === 'smoke' ? 4 : 30));
const BETWEEN_TRIPS_SECONDS = Math.max(0, numberEnv('BETWEEN_TRIPS_SECONDS', PROFILE === 'smoke' ? 1 : 15));
const THINK_TIME_SECONDS = Math.max(0, numberEnv('THINK_TIME_SECONDS', 0.8));
const BUDGET_PESOS = Math.max(0, numberEnv('BUDGET_PESOS', 150));

const isLocal = BASE_URL.includes('localhost') || BASE_URL.includes('127.0.0.1') || BASE_URL.includes('0.0.0.0');
if (PROFILE !== 'smoke' && !isLocal && !ALLOW_REMOTE_LOAD) {
  throw new Error(`Refusing PROFILE=${PROFILE} against remote ${BASE_URL}. Set ALLOW_REMOTE_LOAD=YES intentionally.`);
}
if ((GOOGLE_MORE_RATE > 0 || AI_RATE > 0) && !ENABLE_EXTERNALS) {
  throw new Error('Google/Gemini sampling is enabled. Add ENABLE_EXTERNALS=YES to acknowledge quota/billing impact.');
}
if (!DISABLE_SERVER_AI && !ENABLE_EXTERNALS) {
  throw new Error('Backend navigation speech AI is enabled for this load test. Add ENABLE_EXTERNALS=YES to acknowledge Gemini quota/billing impact.');
}

const DEFAULT_TRIPS = [
  {
    id: 'porac-angeles',
    origin: { latitude: 15.106744, longitude: 120.561241 },
    destination: { query: 'Angeles University Foundation', name: 'Angeles sample', latitude: 15.139582098206548, longitude: 120.60108373338038 }
  },
  {
    id: 'angeles-clark',
    origin: { latitude: 15.139582098206548, longitude: 120.60108373338038 },
    destination: { query: 'SM City Clark', name: 'Clark sample', latitude: 15.169377198609359, longitude: 120.58742919718586 }
  },
  {
    id: 'clark-dau',
    origin: { latitude: 15.169377198609359, longitude: 120.58742919718586 },
    destination: { query: 'Dau Bus Terminal', name: 'Dau sample', latitude: 15.1778, longitude: 120.5896 }
  },
  {
    id: 'mabalacat-porac',
    origin: { latitude: 15.2228, longitude: 120.5745 },
    destination: { query: 'Porac Public Market', name: 'Porac sample', latitude: 15.106744, longitude: 120.561241 }
  }
];

const TRIPS = __ENV.TRIPS_FILE ? JSON.parse(open(__ENV.TRIPS_FILE)) : DEFAULT_TRIPS;
if (!Array.isArray(TRIPS) || TRIPS.length === 0) throw new Error('TRIPS_FILE must be a non-empty JSON array.');

function scenario(profile) {
  switch (profile) {
	case 'hundred':
  return {
    realistic_users: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 10 },
        { duration: '30s', target: 25 },
        { duration: '1m', target: 50 },
        { duration: '1m', target: 75 },
        { duration: '2m', target: 100 },
        { duration: '1m', target: 100 },
        { duration: '1m', target: 0 },
      ],
      gracefulRampDown: '30s',
      gracefulStop: '30s',
      },
    };
    case 'smoke':
      return { realistic_users: { executor: 'constant-vus', vus: 1, duration: '30s', gracefulStop: '15s' } };
    case 'small':
      return { realistic_users: { executor: 'ramping-vus', startVUs: 0, stages: [
        { duration: '30s', target: 10 }, { duration: '1m', target: 25 }, { duration: '2m', target: 50 }, { duration: '30s', target: 0 }
      ], gracefulRampDown: '30s', gracefulStop: '30s' } };
    case 'load':
      return { realistic_users: { executor: 'ramping-vus', startVUs: 0, stages: [
        { duration: '1m', target: 50 }, { duration: '1m', target: 100 }, { duration: '2m', target: 250 },
        { duration: '3m', target: 500 }, { duration: '5m', target: 1000 }, { duration: '2m', target: 0 }
      ], gracefulRampDown: '1m', gracefulStop: '1m' } };
    case 'stress':
      return { realistic_users: { executor: 'ramping-vus', startVUs: 100, stages: [
        { duration: '2m', target: 250 }, { duration: '2m', target: 500 }, { duration: '2m', target: 750 },
        { duration: '2m', target: 1000 }, { duration: '2m', target: 1250 }, { duration: '2m', target: 1500 }, { duration: '2m', target: 0 }
      ], gracefulRampDown: '1m', gracefulStop: '1m' } };
    case 'spike':
      return { realistic_users: { executor: 'ramping-vus', startVUs: 0, stages: [
        { duration: '30s', target: 50 }, { duration: '1m', target: 50 }, { duration: '10s', target: 1000 },
        { duration: '2m', target: 1000 }, { duration: '20s', target: 50 }, { duration: '30s', target: 0 }
      ], gracefulRampDown: '30s', gracefulStop: '30s' } };
    case 'soak':
      return { realistic_users: { executor: 'constant-vus', vus: 500, duration: '2h', gracefulStop: '1m' } };
    default:
      throw new Error(`Unknown PROFILE=${profile}. Use smoke, small, load, stress, spike, or soak.`);
  }
}

const authDuration = new Trend('tuki_auth_duration', true);
const searchDuration = new Trend('tuki_place_search_duration', true);
const planDuration = new Trend('tuki_plan_duration', true);
const startDuration = new Trend('tuki_start_duration', true);
const rerouteDuration = new Trend('tuki_reroute_duration', true);
const cancelDuration = new Trend('tuki_cancel_duration', true);
const flowSuccess = new Rate('tuki_flow_success');
const searchSuccess = new Rate('tuki_place_search_success');
const planSuccess = new Rate('tuki_plan_success');
const startSuccess = new Rate('tuki_start_success');
const rerouteSuccess = new Rate('tuki_reroute_success');
const cancelSuccess = new Rate('tuki_cancel_success');
const noRouteCount = new Counter('tuki_no_route_count');
const unexpectedStatusCount = new Counter('tuki_unexpected_status_count');

export const options = {
  scenarios: scenario(PROFILE),
  thresholds: {
    http_req_failed: ['rate<0.02'],
    tuki_auth_duration: ['p(95)<2000'],
    tuki_place_search_duration: ['p(95)<2500'],
    tuki_plan_duration: ['p(95)<8000'],
    tuki_start_duration: ['p(95)<4000'],
    tuki_reroute_duration: ['p(95)<9000'],
    tuki_cancel_duration: ['p(95)<3000'],
    tuki_place_search_success: ['rate>0.95'],
    tuki_plan_success: ['rate>0.90'],
    tuki_start_success: ['rate>0.90'],
    tuki_cancel_success: ['rate>0.95'],
    tuki_flow_success: ['rate>0.85']
  }
};

let apiKey = null;
let sessionId = null;

const json = (response) => {
  try { return response.json(); } catch (_) { return null; }
};
const value = (obj, camel, pascal) => obj?.[camel] ?? obj?.[pascal];
const headers = (withJson = false) => ({
  headers: {
    Accept: 'application/json',
    ...(withJson ? { 'Content-Type': 'application/json' } : {}),
    ...(DISABLE_SERVER_AI ? { 'X-Tuki-Disable-Ai': 'true' } : {}),
    ...(apiKey ? { 'X-Api-Key': apiKey } : {})
  },
  timeout: __ENV.HTTP_TIMEOUT || '30s'
});
const tagged = (name, withJson = false, extra = {}) => ({ ...headers(withJson), tags: { name, endpoint: name, profile: PROFILE, ...extra } });
const think = (seconds) => { if (seconds > 0) sleep(seconds * (0.65 + Math.random() * 0.70)); };

function authenticate() {
  if (apiKey) return true;
  const response = http.post(`${BASE_URL}/api/auth/guest`, null, tagged('auth_guest'));
  authDuration.add(response.timings.duration);
  apiKey = value(json(response), 'apiKey', 'ApiKey') || null;
  return check(response, { 'guest auth 200': (r) => r.status === 200 }) && Boolean(apiKey);
}

function destinationFor(trip) {
  const q = encodeURIComponent(trip.destination.query);
  const response = http.get(
    `${BASE_URL}/api/places/search?q=${q}&focusLat=${trip.origin.latitude}&focusLon=${trip.origin.longitude}`,
    tagged('places_search', false, { trip: trip.id })
  );
  searchDuration.add(response.timings.duration, { trip: trip.id });
  const results = json(response);
  const first = Array.isArray(results) ? results[0] : null;
  const ok = response.status === 200 && Boolean(first);
  searchSuccess.add(ok, { trip: trip.id });

  if (Math.random() < GOOGLE_MORE_RATE) {
    http.get(
      `${BASE_URL}/api/places/search/more?q=${q}&focusLat=${trip.origin.latitude}&focusLon=${trip.origin.longitude}`,
      tagged('places_search_more', false, { trip: trip.id })
    );
  }

  return first ? {
    name: value(first, 'name', 'Name') || trip.destination.name,
    latitude: Number(value(first, 'latitude', 'Latitude')),
    longitude: Number(value(first, 'longitude', 'Longitude'))
  } : trip.destination;
}

function plan(trip, destination) {
  const preference = ['efficient', 'fastest', 'cheapest'][(__VU + __ITER) % 3];
  const body = {
    originLatitude: trip.origin.latitude,
    originLongitude: trip.origin.longitude,
    destinationName: destination.name,
    destinationLatitude: destination.latitude,
    destinationLongitude: destination.longitude,
    preference
  };
  if (BUDGET_PESOS > 0) body.budget = BUDGET_PESOS;

  const response = http.post(`${BASE_URL}/api/journeys/plan`, JSON.stringify(body), tagged('journeys_plan', true, { trip: trip.id, preference }));
  planDuration.add(response.timings.duration, { trip: trip.id });
  const recommendations = json(response);
  const recommendationId = Array.isArray(recommendations) && recommendations.length
    ? value(recommendations[0], 'recommendationId', 'RecommendationId')
    : null;
  const ok = response.status === 200 && Boolean(recommendationId);
  planSuccess.add(ok, { trip: trip.id });
  if (response.status === 200 && Array.isArray(recommendations) && recommendations.length === 0) noRouteCount.add(1, { trip: trip.id });
  return recommendationId;
}

function start(recommendationId, tripId) {
  const response = http.post(`${BASE_URL}/api/navigation/start`, JSON.stringify({ recommendationId }), tagged('navigation_start', true, { trip: tripId }));
  startDuration.add(response.timings.duration, { trip: tripId });
  const snapshot = json(response);
  sessionId = value(snapshot, 'sessionId', 'SessionId') || null;
  const ok = response.status === 201 && Boolean(sessionId);
  startSuccess.add(ok, { trip: tripId });
  return snapshot;
}

function maybeBoard(snapshot, tripId) {
  if (!value(snapshot, 'requiresBoardingConfirmation', 'RequiresBoardingConfirmation')) return true;
  const response = http.post(`${BASE_URL}/api/navigation/${sessionId}/boarding`, null, tagged('navigation_boarding', false, { trip: tripId }));
  return check(response, { 'boarding 200': (r) => r.status === 200 });
}

function maybeLocation(trip, destination) {
  if (Math.random() >= LOCATION_SYNC_RATE) return true;
  const fraction = 0.08;
  const body = {
    latitude: trip.origin.latitude + (destination.latitude - trip.origin.latitude) * fraction,
    longitude: trip.origin.longitude + (destination.longitude - trip.origin.longitude) * fraction,
    accuracyMeters: 8,
    timestamp: new Date().toISOString(),
    speedMetersPerSecond: 1.2,
    bearingDegrees: 45
  };
  const response = http.post(`${BASE_URL}/api/navigation/${sessionId}/location`, JSON.stringify(body), tagged('navigation_location', true, { trip: trip.id }));
  return check(response, { 'location sync 200': (r) => r.status === 200 });
}

function maybeActive(tripId) {
  if (Math.random() >= ACTIVE_REFRESH_RATE) return true;
  const response = http.get(`${BASE_URL}/api/navigation/active`, tagged('navigation_active', false, { trip: tripId }));
  return check(response, { 'active refresh 200': (r) => r.status === 200 });
}

function maybeReroute(trip, destination) {
  if (Math.random() >= REROUTE_RATE) return true;
  const fraction = 0.25;
  let latitude = trip.origin.latitude + (destination.latitude - trip.origin.latitude) * fraction;
  let longitude = trip.origin.longitude + (destination.longitude - trip.origin.longitude) * fraction;
  if (REROUTE_REASON === 'OFF_ROUTE') { latitude += 0.0015; longitude += 0.0015; }

  const response = http.post(
    `${BASE_URL}/api/navigation/${sessionId}/reroute`,
    JSON.stringify({
      reason: REROUTE_REASON,
      latitude,
      longitude,
      accuracyMeters: 8,
      timestamp: new Date().toISOString(),
      speedMetersPerSecond: 5.5,
      bearingDegrees: 30
    }),
    tagged('navigation_reroute', true, { trip: trip.id, reason: REROUTE_REASON })
  );
  rerouteDuration.add(response.timings.duration, { trip: trip.id, reason: REROUTE_REASON });
  const ok = response.status === 200 && Boolean(value(json(response), 'recommendationId', 'RecommendationId'));
  rerouteSuccess.add(ok, { trip: trip.id, reason: REROUTE_REASON });
  return ok;
}

function maybeAskAi(tripId) {
  if (Math.random() >= AI_RATE) return true;
  const response = http.post(
    `${BASE_URL}/api/AI/ask`,
    JSON.stringify({ message: 'Tuki, tama pa ba yung daan natin?', tripSessionId: sessionId }),
    tagged('ai_ask_active_trip', true, { trip: tripId })
  );
  return check(response, { 'AI question 200': (r) => r.status === 200 });
}

function cancel(tripId) {
  if (!sessionId) return true;
  const currentSession = sessionId;
  const response = http.post(`${BASE_URL}/api/navigation/${currentSession}/cancel`, null, tagged('navigation_cancel', false, { trip: tripId }));
  cancelDuration.add(response.timings.duration, { trip: tripId });
  const ok = response.status === 200;
  cancelSuccess.add(ok, { trip: tripId });
  if (ok || response.status === 404) sessionId = null;
  return ok;
}

export function setup() {
  console.log(`Navigation speech AI during load test: ${DISABLE_SERVER_AI ? 'disabled (deterministic fallback)' : 'enabled (external quota may be consumed)'}`);
  const response = http.get(`${BASE_URL}/health`, { tags: { name: 'health', endpoint: 'health', profile: PROFILE }, timeout: '30s' });
  if (response.status !== 200) throw new Error(`${BASE_URL}/health returned ${response.status}`);
}

export default function () {
  const trip = TRIPS[(__VU + __ITER - 1) % TRIPS.length];
  let ok = true;

  if (sessionId) ok = cancel(trip.id) && ok;
  group('01 auth', () => { ok = authenticate() && ok; });
  if (!apiKey) { flowSuccess.add(false, { trip: trip.id }); return; }

  think(THINK_TIME_SECONDS);
  let destination;
  group('02 search', () => { destination = destinationFor(trip); });

  think(THINK_TIME_SECONDS);
  let recommendationId;
  group('03 plan', () => { recommendationId = plan(trip, destination); ok = Boolean(recommendationId) && ok; });
  if (!recommendationId) { flowSuccess.add(false, { trip: trip.id }); think(BETWEEN_TRIPS_SECONDS); return; }

  think(THINK_TIME_SECONDS);
  let snapshot;
  group('04 start', () => { snapshot = start(recommendationId, trip.id); ok = Boolean(snapshot && sessionId) && ok; });
  if (!sessionId) { flowSuccess.add(false, { trip: trip.id }); think(BETWEEN_TRIPS_SECONDS); return; }

  group('05 board if requested', () => { ok = maybeBoard(snapshot, trip.id) && ok; });

  // Simulate time spent navigating locally. Do NOT turn active users into a
  // 1 Hz backend GPS flood; that is not Tuki's production architecture.
  think(NAV_HOLD_SECONDS / 2);
  group('06 meaningful nav events', () => {
    ok = maybeLocation(trip, destination) && ok;
    ok = maybeActive(trip.id) && ok;
    ok = maybeReroute(trip, destination) && ok;
    ok = maybeAskAi(trip.id) && ok;
  });
  think(NAV_HOLD_SECONDS / 2);

  group('07 cancel', () => { ok = cancel(trip.id) && ok; });
  if (!ok) unexpectedStatusCount.add(1, { trip: trip.id });
  flowSuccess.add(ok, { trip: trip.id });
  think(BETWEEN_TRIPS_SECONDS);
}
