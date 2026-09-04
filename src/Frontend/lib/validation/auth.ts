import { z } from 'zod';

export const loginSchema = z.object({
  email: z.string().trim().pipe(z.email('Please enter a valid email address.')),
  password: z.string().min(8, 'Password must be at least 8 characters.'),
});

export type LoginFormValues = z.infer<typeof loginSchema>;

export const registerSchema = z.object({
  firstName: z.string().trim().min(1, 'First name is required.'),
  lastName: z.string().trim().min(1, 'Last name is required.'),
  email: z.string().trim().pipe(z.email('Please enter a valid email address.')),
  password: z.string()
    .min(8, 'Password must be at least 8 characters.')
    .regex(/[a-z]/, "Password must have at least one lowercase ('a'-'z').")
    .regex(/[A-Z]/, "Password must have at least one uppercase ('A'-'Z').")
    .regex(/[^\p{L}\p{N}]/u, 'Password must have at least one non alphanumeric character.'),
  confirmPassword: z.string().min(1, 'Please confirm your password.'),
  terms: z.literal(true, { error: 'You must agree to the Terms of Service and Privacy Policy.' }),
}).refine((values) => values.password === values.confirmPassword, {
  path: ['confirmPassword'],
  message: 'Passwords do not match.',
});

export type RegisterFormValues = z.infer<typeof registerSchema>;
