import { z } from 'zod'
import { ADMIN_LIMITS } from '@/lib/api/limits'
import { ROLES } from '@/lib/format/labels'

/**
 * Client-side checks mirroring docs/API_CONTRACT.md §8 for fast feedback; the server validates
 * again and its 400 `errors` are shown on the matching field.
 */
const text = (label: string, max: number) => z.string().trim().min(1, `Enter ${label}.`).max(max, `Use ${max} characters or fewer.`)

export const passwordSchema = z
  .string()
  .min(ADMIN_LIMITS.passwordMin, `Use at least ${ADMIN_LIMITS.passwordMin} characters.`)
  .max(ADMIN_LIMITS.passwordMax, `Use ${ADMIN_LIMITS.passwordMax} characters or fewer.`)

const roleSchema = z.enum(ROLES)

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

/**
 * One form for create and edit (they share every profile field). `email` and `initialPassword`
 * are only checked when creating; `isActive` is only sent when editing. "" means none for the
 * department and manager selects.
 */
export function userFormSchema(mode: 'create' | 'edit') {
  return z
    .object({
      displayName: text('a display name', ADMIN_LIMITS.displayNameMax),
      email: z.string().trim(),
      initialPassword: z.string(),
      departmentId: z.string(),
      managerId: z.string(),
      roles: z.array(roleSchema),
      isActive: z.boolean(),
    })
    .superRefine((values, context) => {
      if (mode !== 'create') return
      const issue = (path: 'email' | 'initialPassword', message: string) => context.addIssue({ code: 'custom', path: [path], message })
      if (values.email === '') issue('email', 'Enter an email address.')
      else if (values.email.length > ADMIN_LIMITS.emailMax) issue('email', `Use ${ADMIN_LIMITS.emailMax} characters or fewer.`)
      else if (!EMAIL_PATTERN.test(values.email)) issue('email', 'Enter a valid email address.')
      const password = passwordSchema.safeParse(values.initialPassword)
      if (!password.success) issue('initialPassword', password.error.issues[0]?.message ?? 'Enter a valid password.')
    })
}
export type UserFormValues = z.infer<ReturnType<typeof userFormSchema>>

export const resetPasswordSchema = z.object({ newPassword: passwordSchema })
export type ResetPasswordValues = z.infer<typeof resetPasswordSchema>

export const departmentSchema = z.object({
  name: text('a name', ADMIN_LIMITS.departmentNameMax),
  code: z
    .string()
    .trim()
    .min(1, 'Enter a code.')
    .max(ADMIN_LIMITS.departmentCodeMax, `Use ${ADMIN_LIMITS.departmentCodeMax} characters or fewer.`)
    .regex(/^[A-Za-z0-9-]+$/, 'Use letters, digits and hyphens only.'),
  isActive: z.boolean(),
})
export type DepartmentValues = z.infer<typeof departmentSchema>
