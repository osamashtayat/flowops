import { describe, expect, it } from 'vitest'
import contractJson from '../../../../../contracts/openapi.json'
import { SESSION_LIMITS } from './limits'

interface Property {
  maxLength?: number
  minLength?: number
}
const schemas = (contractJson as unknown as { components: { schemas: Record<string, { properties: Record<string, Property> }> } })
  .components.schemas

const property = (schema: string, name: string) => schemas[schema]?.properties[name]

describe('SESSION_LIMITS match contracts/openapi.json', () => {
  it('login email and password', () => {
    expect(property('LoginRequest', 'email')?.maxLength).toBe(SESSION_LIMITS.emailMax)
    expect(property('LoginRequest', 'password')?.maxLength).toBe(SESSION_LIMITS.passwordMax)
  })

  it('change password', () => {
    expect(property('ChangePasswordRequest', 'currentPassword')?.maxLength).toBe(SESSION_LIMITS.passwordMax)
    expect(property('ChangePasswordRequest', 'newPassword')?.maxLength).toBe(SESSION_LIMITS.passwordMax)
    expect(property('ChangePasswordRequest', 'newPassword')?.minLength).toBe(SESSION_LIMITS.newPasswordMin)
  })
})
