// Test obciążenia HTTP jednego wariantu API na danych z db/*/Publish/benchmark.sql (k6, w kontenerze).
// Z sieci compose (bez przekierowania portów Docker Desktop, które zniekształca pomiar):
//   docker run --rm -i --network rekrutacja_default -e BASE_URL=http://api-efcore-postgres:8080 grafana/k6:2.3.0 run - < benchmarks/k6/products.js
// Zmienne: SCENARIO = read (GET, domyślnie) | write (PUT ceny) | create (POST), VUS (domyślnie 32), DURATION (30s)
import http from 'k6/http';
import { check } from 'k6';

const baseUrl = __ENV.BASE_URL;
const scenario = __ENV.SCENARIO || 'read';
const productCount = 100000;
const json = { headers: { 'Content-Type': 'application/json' } };

export const options = {
  vus: Number(__ENV.VUS || 32),
  duration: __ENV.DURATION || '30s',
  summaryTrendStats: ['avg', 'p(50)', 'p(95)', 'p(99)', 'max'],
  thresholds: { checks: ['rate==1'] }, // każda błędna odpowiedź oznacza nieważny pomiar
};

// Id jak w generatorze danych: b0000000-0000-0000-0000-{n w hex}, n losowe z 1..productCount
const productId = (n = Math.floor(Math.random() * productCount) + 1) =>
  `b0000000-0000-0000-0000-${n.toString(16).padStart(12, '0')}`;

const requests = {
  read: () => http.get(`${baseUrl}/api/products/${productId()}`),
  // cena spoza zakresu danych (1-999.99) i losowa - każde żądanie naprawdę zmienia cenę
  write: () => http.put(`${baseUrl}/api/products/${productId()}/price`,
    JSON.stringify({ price: Math.floor(Math.random() * 9900000 + 100000) / 100, currency: 'PLN' }), json),
  // nazwa jak w benchmarkach repozytoriów - usuwa je polecenie cleanup
  create: () => http.post(`${baseUrl}/api/products`,
    JSON.stringify({ name: 'Benchmark create', price: 10, currency: 'PLN' }), json),
};

export function setup() {
  if (!requests[scenario])
    throw new Error(`Unknown SCENARIO '${scenario}' - use read, write or create.`);
  if (http.get(`${baseUrl}/api/products/${productId(productCount)}`).status !== 200)
    throw new Error(`No benchmark data behind ${baseUrl} - load db/*/Publish/benchmark.sql.`);
}

export default function () {
  check(requests[scenario](), { 'status 2xx': (r) => r.status >= 200 && r.status < 300 });
}
