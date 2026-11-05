import { describe, expect, it } from 'vitest'
import { changePasswordPath, loginPath, safeReturnTo } from './returnTo'

describe('safeReturnTo', () => {
  it.each(['/requests', '/requests?page=2&status=Approved', '/requests/123#top', '/change-password'])(
    'keeps the same-origin path %s',
    (path) => expect(safeReturnTo(path)).toBe(path),
  )

  it.each([
    null,
    undefined,
    '',
    'requests',
    '//evil.example',
    '///evil.example',
    '/\\evil.example',
    'https://evil.example/x',
    'javascript:alert(1)',
    '/login',
    '/login?returnTo=%2F',
  ])('falls back to / for %s', (raw) => expect(safeReturnTo(raw)).toBe('/'))
})

describe('login and change-password paths', () => {
  it('encode the return path', () => {
    expect(loginPath('/requests?page=2')).toBe('/login?returnTo=%2Frequests%3Fpage%3D2')
    expect(changePasswordPath('/inbox')).toBe('/change-password?returnTo=%2Finbox')
  })

  it('omit returnTo for home or unsafe targets', () => {
    expect(loginPath('/')).toBe('/login')
    expect(loginPath('//evil.example')).toBe('/login')
    expect(loginPath()).toBe('/login')
  })
})
