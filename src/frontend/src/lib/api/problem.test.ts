import { describe, expect, it } from 'vitest'
import errorCodesSource from '../../../../backend/FlowOps.Domain/Errors/ErrorCodes.cs?raw'
import { ApiError, NETWORK_ERROR_CODE, UNKNOWN_ERROR_CODE } from './apiError'
import { PROBLEM_MESSAGES, serverFieldError, toUserMessage } from './problem'

/** Every `public const string X = "code";` in the backend's ErrorCodes.cs. */
const backendCodes = [...errorCodesSource.matchAll(/public const string \w+ = "([^"]+)";/g)].map((m) => m[1] ?? '')

describe('PROBLEM_MESSAGES', () => {
  it('reads the backend code list (guards the regex itself)', () => {
    expect(backendCodes.length).toBeGreaterThan(15)
    expect(backendCodes).toContain('task.already_decided')
  })

  it.each(backendCodes)('has a user message for backend code %s', (code) => {
    expect(PROBLEM_MESSAGES).toHaveProperty([code])
  })

  it('has no message for a code the backend does not define', () => {
    for (const code of Object.keys(PROBLEM_MESSAGES)) expect(backendCodes).toContain(code)
  })

  it('never exposes internals: no message mentions a stack, trace or SQL', () => {
    for (const message of Object.values(PROBLEM_MESSAGES)) expect(message).not.toMatch(/trace|stack|sql|exception/i)
  })
})

describe('toUserMessage', () => {
  const apiError = (status: number, code: string) => new ApiError({ status, code })

  it.each(Object.entries(PROBLEM_MESSAGES))('maps %s to its message', (code, message) => {
    expect(toUserMessage(apiError(400, code))).toBe(message)
  })

  it('says what to do when the network is down', () => {
    expect(toUserMessage(apiError(0, NETWORK_ERROR_CODE))).toMatch(/connection/i)
  })

  it.each([
    [401, PROBLEM_MESSAGES['auth.unauthenticated']],
    [403, PROBLEM_MESSAGES['auth.forbidden']],
    [404, PROBLEM_MESSAGES.not_found],
    [500, PROBLEM_MESSAGES['server.error']],
    [502, PROBLEM_MESSAGES['server.error']],
  ])('falls back by status %i for an unknown code', (status, message) => {
    expect(toUserMessage(apiError(status, UNKNOWN_ERROR_CODE))).toBe(message)
  })

  it('uses a generic message for anything else', () => {
    expect(toUserMessage(apiError(418, 'something.new'))).toBe('Something went wrong. Try again.')
    expect(toUserMessage(new Error('boom'))).toBe('Something went wrong. Try again.')
    expect(toUserMessage('x')).toBe('Something went wrong. Try again.')
  })

  it('is not fooled by codes that match Object prototype keys', () => {
    expect(toUserMessage(apiError(400, 'constructor'))).toBe('Something went wrong. Try again.')
    expect(toUserMessage(apiError(400, '__proto__'))).toBe('Something went wrong. Try again.')
  })
})

describe('serverFieldError', () => {
  it('returns the first message for a field', () => {
    const error = new ApiError({ status: 400, code: 'validation', fieldErrors: { newPassword: ['Too short.', 'x'] } })
    expect(serverFieldError(error, 'newPassword')).toBe('Too short.')
    expect(serverFieldError(error, 'other')).toBeUndefined()
    expect(serverFieldError(new Error('x'), 'newPassword')).toBeUndefined()
  })
})
