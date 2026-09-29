// Builds `documentation/luna-demo.gif` by driving the running Luna stack.
//
// Why drive the real app instead of hand-drawing a mock-up: a demo that does not
// match the product is worse than no demo. The same rule applies to the data —
// a storefront with an empty cart and "No orders yet" is a truthful rendering of
// an unused system and a useless demo, so this script creates a customer, fills
// a cart and places a real order before it captures anything.
//
// The stack is expected to be already up (see infrastructure/README.md). This
// script only reads and writes through the public HTTP surface.
//
// Regenerate with: npm run demo:build   (from src/Frontend)
// It is committed, not built in CI: the GIF is documentation, and rebuilding it
// on every push would put a binary diff in every commit.
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from '@playwright/test';
import sharp from 'sharp';
// gifenc is CommonJS, so it arrives as a namespace rather than named exports.
import gifenc from 'gifenc';

const { GIFEncoder, applyPalette, quantize } = gifenc;

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const BASE = process.env.LUNA_URL ?? 'http://localhost:3000';
const outFile = path.join(repoRoot, 'documentation', 'luna-demo.gif');

const VIEW = { width: 1280, height: 860 };

// A GIF is LZW over indexed colour, so the file size tracks pixels x frames
// x colour count, and a portfolio page should not ship a multi-megabyte asset.
// 5fps over ~12s is 60 frames at 680x458 over 96 colours, which lands around
// 1.3MB. Two numbers here are load-bearing rather than cosmetic: the GIF canvas
// caps at 65,535px tall and `sharp` stacks the frames into one buffer to
// quantise the first one, so FRAMES x GIF_HEIGHT has to stay under that
// (60 x 458 = 27,480).
const FPS = 5;
const GIF_WIDTH = 680;
const GIF_HEIGHT = 458;
const COLORS = 96;

const frameDir = mkdtempSync(path.join(tmpdir(), 'luna-demo-frames-'));
const frames = [];
let index = 0;

/** A throwaway customer per run, so the demo never collides with real data. */
const email = `demo${Date.now()}@luna.test`;
const PASSWORD = 'Demo!Passw0rd';
const CUSTOMER = {
  firstName: 'Ada',
  lastName: 'Lovelace',
  email,
  fullName: 'Ada Lovelace',
  addressLine1: '12 Analytical Way',
  city: 'Sao Paulo',
  state: 'SP',
  postalCode: '01310-100',
  country: 'BR',
};

/** Hold the shot for `ms` so the eye can read it. */
async function hold(page, ms = 1200) {
  const shots = Math.max(1, Math.round((ms / 1000) * FPS));
  for (let i = 0; i < shots; i++) {
    const file = path.join(frameDir, `frame-${String(index++).padStart(3, '0')}.png`);
    await page.screenshot({ path: file });
    frames.push(file);
  }
}

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: VIEW, deviceScaleFactor: 1 });

try {
  // --- sign in -------------------------------------------------------------
  // Registration shows a "check your email" screen, but the backend only creates
  // the user and never sends mail, so the account is usable immediately. The
  // demo registers rather than reusing a fixed account so it can be re-run.
  console.log('[demo] registering and signing in');
  await page.goto(`${BASE}/register`, { waitUntil: 'networkidle' });
  await page.fill('#firstName', CUSTOMER.firstName);
  await page.fill('#lastName', CUSTOMER.lastName);
  await page.fill('#email', CUSTOMER.email);
  await page.fill('#password', PASSWORD);
  await page.fill('#confirmPassword', PASSWORD);
  await page.check('input[name="terms"]');
  await page.getByRole('button', { name: 'Create account' }).click();
  await page.waitForTimeout(2500);

  await page.goto(`${BASE}/login`, { waitUntil: 'networkidle' });
  await page.fill('#email', email);
  await page.fill('#password', PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await page.waitForTimeout(3500);
  if (page.url().includes('/login')) throw new Error('sign-in did not complete');

  // --- storefront ----------------------------------------------------------
  // Scoped to `main` so the sticky header does not push the product grid
  // partly out of frame.
  console.log('[demo] storefront');
  await page.goto(`${BASE}/shop`, { waitUntil: 'networkidle' });
  await page.waitForSelector('img[src*="unsplash"]', { timeout: 30_000 });
  await page.waitForTimeout(3500);
  await hold(page, 2400);

  // The grid links to each product, so the demo follows real ids rather than
  // hard-coding GUIDs that the seed is free to change.
  const products = await page.evaluate(() =>
    [...document.querySelectorAll('a[href^="/shop/products/"]')].map((a) => ({
      href: a.getAttribute('href'),
      name: (a.textContent || '').trim(),
    })),
  );
  const seen = new Set();
  const picks = products.filter((p) => p.href && !seen.has(p.href) && seen.add(p.href)).slice(0, 3);
  if (picks.length === 0) throw new Error('no product links found on the storefront');

  // --- product detail, and a real cart line from each ----------------------
  for (const [i, product] of picks.entries()) {
    console.log(`[demo] product ${i + 1}: ${product.name}`);
    await page.goto(`${BASE}${product.href}`, { waitUntil: 'networkidle' });
    await page.waitForSelector('img[src*="unsplash"]', { timeout: 30_000 });
    await page.waitForTimeout(2500);
    await hold(page, i === 0 ? 1800 : 1200);
    await page.getByRole('button', { name: /add to cart/i }).first().click();
    await page.waitForTimeout(1200);
  }

  // --- cart ----------------------------------------------------------------
  console.log('[demo] cart');
  await page.goto(`${BASE}/cart`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(3000);
  await hold(page, 2000);

  // --- checkout ------------------------------------------------------------
  console.log('[demo] checkout');
  await page.goto(`${BASE}/checkout`, { waitUntil: 'networkidle' });
  await page.waitForSelector('#fullName', { timeout: 30_000 });
  await page.waitForTimeout(1500);
  await page.fill('#email', CUSTOMER.email);
  await page.fill('#fullName', CUSTOMER.fullName);
  await page.fill('#paymentMethod', '4242 4242 4242 4242');
  await page.fill('#addressLine1', CUSTOMER.addressLine1);
  await page.fill('#city', CUSTOMER.city);
  await page.fill('#state', CUSTOMER.state);
  await page.fill('#postalCode', CUSTOMER.postalCode);
  await page.fill('#country', CUSTOMER.country);
  await page.waitForTimeout(800);
  await hold(page, 1800);

  console.log('[demo] placing order');
  await page.getByRole('button', { name: /place order/i }).first().click();
  await page.waitForTimeout(6000);

  // --- order history -------------------------------------------------------
  // Reached by URL rather than by following the redirect, so a failed order
  // still yields a frame instead of an exception.
  console.log('[demo] orders');
  await page.goto(`${BASE}/orders`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(3000);
  // Guard the thing this script exists to prevent: a demo whose final frame
  // says "No orders yet" is a truthful picture of an unused system, and a
  // truthful picture of an unused system is not a demo.
  const orderHistory = await page.locator('main').innerText();
  if (/no orders yet/i.test(orderHistory)) {
    throw new Error('order history is empty; the checkout step did not produce an order');
  }
  await hold(page, 2400);

  await browser.close();

  // --- stitch --------------------------------------------------------------
  console.log(`[demo] stitching ${frames.length} frames at ${FPS}fps`);
  // One palette for the whole animation, taken from the first frame. The app's
  // palette is stable across the frames and sharing it lets the encoder store
  // only the pixels that changed, which is most of the size saving.
  // `quantize` and `applyPalette` read the buffer as RGBA and cast it to a
  // Uint32Array, one element per pixel, so the alpha channel has to survive. A
  // 3-byte RGB buffer reads as 25% fewer pixels than it holds, which produces a
  // plausible-looking file of garbage rather than an error.
  const rgba = (buffer) =>
    sharp(buffer)
      .resize(GIF_WIDTH, GIF_HEIGHT, { fit: 'cover', position: 'top' })
      .ensureAlpha()
      .raw()
      .toBuffer();

  const encoder = GIFEncoder();
  const first = await rgba(frames[0]);
  const palette = quantize(first, COLORS, { format: 'rgb565' });
  for (const [i, frame] of frames.entries()) {
    const pixels = i === 0 ? first : await rgba(frame);
    encoder.writeFrame(applyPalette(pixels, palette), GIF_WIDTH, GIF_HEIGHT, {
      // Only the first frame carries the palette table; the rest reuse it,
      // which is what lets the encoder emit a frame as just its changed pixels.
      ...(i === 0 ? { palette } : {}),
      delay: 1000 / FPS,
    });
  }
  encoder.finish();
  const animated = Buffer.from(encoder.bytes());

  mkdirSync(path.dirname(outFile), { recursive: true });
  writeFileSync(outFile, animated);
  console.log(
    `[demo] wrote ${path.relative(repoRoot, outFile)} (${(animated.length / 1024).toFixed(0)} kB, ` +
      `${frames.length} frames, ${(frames.length / FPS).toFixed(1)}s)`,
  );
} finally {
  await browser.close().catch(() => {});
  rmSync(frameDir, { recursive: true, force: true, maxRetries: 5, retryDelay: 300 });
}
