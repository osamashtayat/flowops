import { z } from 'zod'
import { SESSION_LIMITS } from './limits'

export const loginSchema = z.object({
  email: z
    .string()
    .trim()
    .min(1, 'Enter your email.')
    .max(SESSION_LIMITS.emailMax, `Use ${SESSION_LIMITS.emailMax} characters or fewer.`),
  password: z
    .string()
    .min(1, 'Enter your password.')
    .max(SESSION_LIMITS.passwordMax, `Use ${SESSION_LIMITS.passwordMax} characters or fewer.`),
})
export type LoginValues = z.infer<typeof loginSchema>

export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Enter your current password.').max(SESSION_LIMITS.passwordMax),
    newPassword: z
      .string()
      .min(SESSION_LIMITS.newPasswordMin, `Use at least ${SESSION_LIMITS.newPasswordMin} characters.`)
      .max(SESSION_LIMITS.passwordMax, `Use ${SESSION_LIMITS.passwordMax} characters or fewer.`),
    // Client-only check: the API never receives this field.
    confirmPassword: z.string(),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    path: ['confirmPassword'],
    message: 'The passwords do not match.',
  })
export type ChangePasswordValues = z.infer<typeof changePasswordSchema>
