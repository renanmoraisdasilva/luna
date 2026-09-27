import { afterEach, describe, expect, it, vi } from 'vitest';
import { createIdempotencyKey } from '../../lib/idempotency';

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

function expectVersion4(key: string) {
  expect(key).toMatch(uuidPattern);
  expect(key[14]).toBe('4');
  expect(['8', '9', 'a', 'b']).toContain(key[19]);
}

describe('createIdempotencyKey', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('delegates to the platform UUID generator when it exists', () => {
    vi.stubGlobal('crypto', { randomUUID: () => 'a3f1c2d4-0000-4000-8000-000000000000' });

    expect(createIdempotencyKey()).toBe('a3f1c2d4-0000-4000-8000-000000000000');
  });

  it('builds a version 4 key from random bytes when randomUUID is unavailable', () => {
    vi.stubGlobal('crypto', {
      getRandomValues: (bytes: Uint8Array) => {
        for (let index = 0; index < bytes.length; index += 1) {
          bytes[index] = (index * 7) % 256;
        }
        return bytes;
      },
    });

    expectVersion4(createIdempotencyKey());
  });

  it('builds a version 4 key from Math.random when Web Crypto is unavailable', () => {
    vi.stubGlobal('crypto', undefined);

    expectVersion4(createIdempotencyKey());
  });
});
