/**
 * Sign-in and password limits, from `LoginRequest` / `ChangePasswordRequest` in
 * contracts/openapi.json (limits.test.ts fails if they drift). Request limits live in
 * features/requests/limits.ts.
 */
export const SESSION_LIMITS = {
  /** LoginRequest.email, 1–256 */
  emailMax: 256,
  /** LoginRequest.password / ChangePasswordRequest.currentPassword, 1–256 */
  passwordMax: 256,
  /** ChangePasswordRequest.newPassword, 12–256 */
  newPasswordMin: 12,
} as const
