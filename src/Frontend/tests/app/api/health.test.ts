import { describe, expect, it } from 'vitest';
import { GET } from '../../../app/api/health/route';

describe('health route', () => {
  it('returns an ok status payload', async () => {
    const response = GET();

    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ status: 'ok' });
  });
});
