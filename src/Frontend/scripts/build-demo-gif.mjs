// Builds `documentation/luna-demo.gif` by driving the running Luna stack.
//
// Five screens, two of them the operations console:
//
//   1. storefront        the catalogue
//   2. cart              real line items and a real subtotal
//   3. checkout          the order being submitted
//   4. fulfillment       operations: the queue, with the demo's own order in it
//   5. shipments         operations: the shipment that fulfilment created
//
// Why drive the real app instead of hand-drawing a mock-up: a demo that does
// not match the product is worse than no demo. The same rule applies to the
// data. An empty cart, a queue with nothing in it and a shipments list at zero
// are all truthful renderings of an unused system, and none of them is a demo —
// so this script places a real order and then walks it through fulfilment
// before it captures anything.
//
// The stack is expected to be already up (see infrastructure/README.md); this
// script only reads and writes through the public HTTP surface, plus one
// INSERT that grants its own throwaway account the Admin role.
//
// Regenerate with: npm run demo:build   (from src/Frontend)
// It is committed, not built in CI: the GIF is documentation, and rebuilding it
// on every push would put a binary diff in every commit.
import { execFileSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
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
const SQL_CONTAINER = process.env.LUNA_SQL_CONTAINER ?? 'infrastructure-sqlserver-1';
const outFile = path.join(repoRoot, 'documentation', 'luna-demo.gif');
const imageDir = path.join(repoRoot, 'documentation', 'images');

// Two outputs from one walkthrough. `--stills` writes the captioned screenshots
// the README uses; without it the same run writes the animated GIF. The setup is
// all but identical, so keeping them in one script is what stops the stills
// drifting away from the GIF.
const STILLS = process.argv.includes('--stills');

// 1440 rather than 1280: the operations tables carry a seventh column, and at
// 1280 the ACTION button was clipped off the right edge of the frame.
const VIEW = { width: 1440, height: 900 };

// A GIF is LZW over indexed colour, so the file size tracks pixels x frames
// x colour count, and a portfolio page should not ship a multi-megabyte asset.
// 5fps over ~12s is 60 frames at 680x425 over 96 colours. Two numbers here are
// load-bearing rather than cosmetic: the GIF canvas caps at 65,535px tall and
// `sharp` stacks the frames into one buffer to quantise the first one, so
// FRAMES x GIF_HEIGHT has to stay under that (60 x 425 = 25,500).
const FPS = 5;
const GIF_WIDTH = 680;
const GIF_HEIGHT = 425;
const COLORS = 96;

const frameDir = mkdtempSync(path.join(tmpdir(), 'luna-demo-frames-'));
const frames = [];
let index = 0;

/**
 * A fixed demo customer, not a generated one.
 *
 * The name shows up in the operations queue, so `demo1738900000000@luna.test`
 * would be in the middle of the GIF. A stable address also makes the script
 * re-runnable: it signs in if the account is there and registers if it is not,
 * rather than accumulating a new throwaway user on every build.
 */
const CUSTOMER = {
  email: 'ada.lovenace@luna.test',
  password: 'Demo!Passw0rd',
  firstName: 'Ada',
  lastName: 'Lovelace',
  fullName: 'Ada Lovelace',
  addressLine1: '12 Analytical Way',
  city: 'Sao Paulo',
  state: 'SP',
  postalCode: '01310-100',
  country: 'BR',
};

/** Run one statement against the local dev database. */
function sql(query) {
  // The SA password lives in the gitignored dev .env, so no credential is ever
  // written into this repository.
  const pw = readFileSync(path.join(repoRoot, 'infrastructure', '.env'), 'utf8')
    .match(/^MSSQL_SA_PASSWORD=(.*)$/m)[1]
    .trim()
    .replace(/^"|"$/g, '');
  return execFileSync(
    'docker',
    ['exec', SQL_CONTAINER, '/opt/mssql-tools18/bin/sqlcmd', '-S', 'localhost', '-U', 'sa',
      '-P', pw, '-C', '-h', '-1', '-W', '-Q', query],
    { encoding: 'utf8' },
  );
}

/**
 * Hold the shot for `ms` so the eye can read it, or write one still.
 *
 * In stills mode this takes a single full-page screenshot and returns
 * immediately, so the walkthrough below is the same in both modes and the
 * screenshots cannot drift away from the GIF.
 */
async function capture(name, ms = 1200) {
  if (STILLS) {
    // Playwright only writes PNG and JPEG, so the buffer is re-encoded to WebP
    // rather than screenshotting straight to it. WebP at q82 roughly halves the
    // bytes at this width, which matters because these are committed and the
    // storefront alone was 772 kB as a PNG. Keeping the encode here means
    // `npm run demo:stills` keeps producing WebP rather than a format the
    // README no longer references.
    const png = await page.screenshot({ fullPage: true });
    await sharp(png).webp({ quality: 82 }).toFile(path.join(imageDir, `${name}.webp`));
    console.log(`[demo] still ${name}.webp`);
    return;
  }
  const shots = Math.max(1, Math.round((ms / 1000) * FPS));
  for (let i = 0; i < shots; i++) {
    const file = path.join(frameDir, `frame-${String(index++).padStart(3, '0')}.png`);
    await page.screenshot({ path: file });
    frames.push(file);
  }
}

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: VIEW, deviceScaleFactor: 1 });
if (STILLS) mkdirSync(imageDir, { recursive: true });

try {
  // --- sign in, registering the demo customer on first run ------------------
  // Registration shows a "check your email" screen, but the backend only creates
  // the user and never sends mail, so the account is usable immediately.
  console.log('[demo] signing in');
  const signIn = async () => {
    await page.goto(`${BASE}/login`, { waitUntil: 'networkidle' });
    await page.fill('#email', CUSTOMER.email);
    await page.fill('#password', CUSTOMER.password);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await page.waitForTimeout(3500);
    return !page.url().includes('/login');
  };

  if (!(await signIn())) {
    console.log('[demo] demo account not found, registering it');
    await page.goto(`${BASE}/register`, { waitUntil: 'networkidle' });
    await page.fill('#firstName', CUSTOMER.firstName);
    await page.fill('#lastName', CUSTOMER.lastName);
    await page.fill('#email', CUSTOMER.email);
    await page.fill('#password', CUSTOMER.password);
    await page.fill('#confirmPassword', CUSTOMER.password);
    await page.check('input[name="terms"]');
    await page.getByRole('button', { name: 'Create account' }).click();
    await page.waitForTimeout(2500);
    if (!(await signIn())) throw new Error('could not sign in after registering');
  }

  // --- operations access ----------------------------------------------------
  // `/operations/*` is gated on the Admin role, and Luna seeds roles but no
  // users, so there is no way in through the UI. The role is a real backend
  // authorization policy, not just a frontend redirect, so the demo grants it
  // to its own throwaway account directly rather than pretending otherwise.
  // The JWT is minted at sign-in, so the new role only lands after a fresh one.
  console.log('[demo] ensuring Admin role');
  // QUOTED_IDENTIFIER has to be ON for this write: the Identity tables carry
  // filtered indexes, and SQL Server refuses the statement with Msg 1934
  // otherwise — which looks like a permissions problem and is not one.
  const userId = await page.evaluate(async () => (await (await fetch('/api/auth/me')).json()).id);
  sql(
    `SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
IF NOT EXISTS (SELECT 1 FROM IdentityDb.dbo.AspNetUserRoles
               WHERE UserId='${userId}'
                 AND RoleId=(SELECT Id FROM IdentityDb.dbo.AspNetRoles WHERE Name='Admin'))
  INSERT INTO IdentityDb.dbo.AspNetUserRoles (UserId, RoleId)
  SELECT '${userId}', Id FROM IdentityDb.dbo.AspNetRoles WHERE Name='Admin';`,
  );
  if (!(await signIn())) throw new Error('re-authentication after the role grant failed');
  const roles = await page.evaluate(async () => (await (await fetch('/api/auth/me')).json()).roles);
  if (!roles.some((r) => r.toLowerCase() === 'admin')) {
    throw new Error(`Admin role did not take effect, roles are ${JSON.stringify(roles)}`);
  }

  // --- 1. storefront --------------------------------------------------------
  console.log('[demo] 1/5 storefront');
  await page.goto(`${BASE}/shop`, { waitUntil: 'networkidle' });
  await page.waitForSelector('img[src*="unsplash"]', { timeout: 30_000 });
  await page.waitForTimeout(3500);
  await capture('storefront', 2200);

  // --- 2. cart --------------------------------------------------------------
  console.log('[demo] 2/5 cart');
  const addToCart = page.getByRole('button', { name: 'Add to Cart' });
  for (const i of [0, 2, 5]) {
    await addToCart.nth(i).click();
    await page.waitForTimeout(1000);
  }
  // The walkthrough needs the cart filled, because checkout reads it, but the
  // cart screen is not one of the README stills: checkout shows the same money
  // plus the address and shipping method, so a cart still would be redundant.
  await page.goto(`${BASE}/cart`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(3000);
  if (/your cart is empty/i.test(await page.locator('body').innerText())) {
    throw new Error('the cart is empty; the demo would show a zero subtotal');
  }
  if (!STILLS) await capture('cart', 2000);

  // --- 3. checkout ----------------------------------------------------------
  console.log('[demo] 3/5 checkout');
  await page.goto(`${BASE}/checkout`, { waitUntil: 'networkidle' });
  await page.waitForSelector('#fullName', { timeout: 30_000 });
  await page.fill('#email', CUSTOMER.email);
  await page.fill('#fullName', CUSTOMER.fullName);
  await page.fill('#paymentMethod', '4242 4242 4242 4242');
  await page.fill('#addressLine1', CUSTOMER.addressLine1);
  await page.fill('#city', CUSTOMER.city);
  await page.fill('#state', CUSTOMER.state);
  await page.fill('#postalCode', CUSTOMER.postalCode);
  await page.fill('#country', CUSTOMER.country);
  await page.waitForTimeout(800);
  await capture('checkout', 1800);

  await page.getByRole('button', { name: /place order/i }).first().click();
  await page.waitForTimeout(6000);
  const orderId = new URL(page.url()).searchParams.get('orderId');
  if (!orderId) throw new Error(`no orderId on ${page.url()}; the order was not placed`);
  console.log(`[demo] placed order ${orderId}`);

  // --- 4. fulfilment queue --------------------------------------------------
  // Captured *before* fulfilment is driven, and the order matters. The queue
  // only lists Confirmed and Preparing orders, so the moment Create Shipment
  // runs the order becomes Shipped and drops out of it. Capturing afterwards
  // produced a queue reading "TOTAL ORDERS 0" — technically truthful, and
  // exactly the dead screen this script exists to avoid.
  console.log('[demo] 4/5 fulfilment');
  await page.goto(`${BASE}/operations/fulfillment`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(3000);
  // The empty state is the honest signal to check. The metric cards put the
  // count and its caption in separate elements, so matching on the caption
  // text ("Total active orders 0") never fires and the guard guards nothing.
  if (/no fulfillment orders match/i.test(await page.locator('body').innerText())) {
    throw new Error('the fulfilment queue is empty; the demo would show three zeros');
  }
  await capture('fulfillment-queue', 2200);

  // --- walk the order through fulfilment ------------------------------------
  // Preparing and creating the shipment are what put a row in the shipments
  // list, so without these two clicks screen 5 is an empty table.
  console.log('[demo] driving fulfilment');
  await page.goto(`${BASE}/operations/fulfillment/${orderId}`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(2500);

  // The five-step pipeline on the detail page is the clearest single picture of
  // what Luna does, and it is worth more as a captioned still than as another
  // second of the GIF, so it is captured in stills mode only.
  if (STILLS) await capture('fulfillment-detail', 0);

  for (const action of ['Start Order Preparation', 'Create Shipment']) {
    const button = page.getByRole('button', { name: action, exact: true }).first();
    if (await button.count()) {
      await button.click();
      await page.waitForTimeout(3500);
    } else {
      // A re-run may find the order already past this step, which is fine.
      console.log(`[demo] "${action}" not offered, continuing`);
    }
  }

  // --- 5. shipments ---------------------------------------------------------
  console.log('[demo] 5/5 shipments');
  await page.goto(`${BASE}/operations/shipments`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(3000);
  if (!/LUNA-[A-Z0-9]+/.test(await page.locator('body').innerText())) {
    throw new Error('no tracking number on the shipments screen; fulfilment did not create one');
  }
  await capture('shipments', 2200);

  await browser.close();

  if (STILLS) {
    console.log(`[demo] wrote stills to ${path.relative(repoRoot, imageDir)}`);
    process.exit(0);
  }

  // --- stitch ---------------------------------------------------------------
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
