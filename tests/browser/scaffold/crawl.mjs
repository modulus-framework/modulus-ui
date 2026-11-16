// Crawls a running scaffolded app. Each page is loaded at 390 px (light and dark) and 1280 px (light). A page fails on: a
// status other than 200, a failed resource, a page error, a horizontal scroll or an element past the viewport on the phone.
// Usage: node crawl.mjs <origin> <seeds file> [max pages]
// Environment: CHROME (path to a Chromium binary; default: Playwright's own), REPORT (JSON report file).
import { chromium } from 'playwright-core';
import { readFileSync, writeFileSync } from 'node:fs';

// Calls the identity-backed pages make to /account/* and /authorization/* when the app has no identity host. docs/CLI.md lists
// them as a known limitation, so they are not failures here. Every other failed resource is.
const knownResourceFailures = [/\/account\//, /\/authorization\//, /favicon\.ico$/];

const origin = process.argv[2];
const seedsFile = process.argv[3];
const max = Number(process.argv[4] ?? 400);
if (!origin || !seedsFile) {
  console.error('usage: node crawl.mjs <origin> <seeds file> [max pages]');
  process.exit(2);
}

const configs = [
  { width: 390, scheme: 'light' },
  { width: 390, scheme: 'dark' },
  { width: 1280, scheme: 'light' },
];

const seeds = readFileSync(seedsFile, 'utf8').split('\n').map(s => s.trim()).filter(Boolean);
const browser = await chromium.launch({ executablePath: process.env.CHROME || undefined, args: ['--no-sandbox'] });
const seen = new Set([origin + '/']);
const queue = [origin + '/'];
for (const route of seeds) {
  const url = origin + route;
  if (!seen.has(url)) { seen.add(url); queue.push(url); }
}

const report = [];
while (queue.length && report.length < max) {
  const url = queue.shift();
  const problems = [];
  for (const { width, scheme } of configs) {
    const tag = `${width}px ${scheme}`;
    const ctx = await browser.newContext({ viewport: { width, height: 844 }, colorScheme: scheme });
    const page = await ctx.newPage();
    page.on('pageerror', e => problems.push(`${tag} page error: ${String(e).slice(0, 160)}`));
    page.on('response', r => {
      if (r.status() < 400) return;
      const path = new URL(r.url()).pathname;
      if (knownResourceFailures.some(p => p.test(path))) return;
      if (r.url() === url) return; // the page itself is checked below
      problems.push(`${tag} resource ${r.status()} ${path}`);
    });
    let status = 0;
    try {
      const resp = await page.goto(url, { waitUntil: 'load', timeout: 60000 });
      status = resp ? resp.status() : 0;
      await page.waitForTimeout(1200);
    } catch (e) {
      problems.push(`${tag} load: ${String(e).split('\n')[0].slice(0, 160)}`);
    }
    if (status && status !== 200) problems.push(`${tag} HTTP ${status}`);
    if (status === 200) {
      const overflow = await page.evaluate(() => {
        const vw = document.documentElement.clientWidth;
        const scroll = document.documentElement.scrollWidth - vw;
        const past = [...document.querySelectorAll('main *, body > * *')].filter(el => {
          const r = el.getBoundingClientRect();
          return r.width > 0 && r.right > vw + 1 && !el.closest('.m-table-scroll, .m-scroll, [style*="overflow"], .ant-table-wrapper, .ant-table-content');
        }).slice(0, 3).map(el => `${el.tagName.toLowerCase()}.${String(el.className).slice(0, 30)}`);
        return { scroll, past };
      });
      if (width === 390 && overflow.scroll > 0) problems.push(`${tag} horizontal scroll ${overflow.scroll}px`);
      if (width === 390) overflow.past.forEach(o => problems.push(`${tag} past the viewport: ${o}`));
      if (width === 1280 && scheme === 'light') {
        const hrefs = await page.evaluate(() => [...document.querySelectorAll('a[href]')].map(a => a.getAttribute('href')));
        for (const h of hrefs) {
          if (!h || h.startsWith('#') || /^(javascript|mailto|tel):/.test(h)) continue;
          let abs;
          try { abs = new URL(h, url).href.split('#')[0]; } catch { continue; }
          if (abs.startsWith(origin) && !seen.has(abs)) { seen.add(abs); queue.push(abs); }
        }
      }
    }
    await ctx.close();
  }
  report.push({ url, problems });
  console.log((problems.length ? 'FAIL ' : 'ok   ') + url);
  problems.forEach(p => console.log('     ' + p));
}
await browser.close();

const failed = report.filter(r => r.problems.length);
console.log(`\nchecked ${report.length} pages, ${failed.length} with problems, ${queue.length} not reached`);
if (process.env.REPORT) writeFileSync(process.env.REPORT, JSON.stringify({ origin, pages: report.length, failed }, null, 2));
process.exit(failed.length ? 1 : 0);
