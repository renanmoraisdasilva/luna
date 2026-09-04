import { describe, expect, it } from 'vitest';
import { registerSchema } from '../../../lib/validation/auth';

const validRegistration = {
  firstName: 'Jane',
  lastName: 'Doe',
  email: 'jane@example.com',
  password: 'Password123!',
  confirmPassword: 'Password123!',
  terms: true as const,
};

describe('registerSchema', () => {
  it('enforces the Identity password requirements', () => {
    const result = registerSchema.safeParse({
      ...validRegistration,
      password: 'password123',
      confirmPassword: 'password123',
    });

    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues.map((issue) => issue.message)).toEqual(expect.arrayContaining([
        "Password must have at least one uppercase ('A'-'Z').",
        'Password must have at least one non alphanumeric character.',
      ]));
    }
  });

  it('accepts a password that satisfies the Identity requirements', () => {
    expect(registerSchema.safeParse(validRegistration).success).toBe(true);
  });
});