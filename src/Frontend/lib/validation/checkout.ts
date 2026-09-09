import { z } from 'zod';

export const checkoutSchema = z.object({
  email: z.string().trim().email('Enter a valid email address.'),
  fullName: z.string().trim().min(2, 'Enter your full name.'),
  addressLine1: z.string().trim().min(3, 'Enter your address.'),
  addressLine2: z.string().trim().optional(),
  city: z.string().trim().min(2, 'Enter your city.'),
  state: z.string().trim().min(2, 'Enter your state or province.'),
  postalCode: z.string().trim().min(3, 'Enter your postal code.'),
  country: z.string().trim().min(2, 'Enter your country.'),
  shippingMethodCode: z.string().min(1, 'Choose a shipping method.'),
  paymentMethod: z.string().trim().min(2, 'Enter a payment method.'),
});

export type CheckoutFormValues = z.infer<typeof checkoutSchema>;
