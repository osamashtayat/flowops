import { Navigate, Outlet } from 'react-router'
import type { Capability } from '@/lib/api/types'
import { useCapability } from '../useCapability'
import { FORBIDDEN_PATH } from '../returnTo'

/** Layout route: renders its children only if the server granted `capability`, else /forbidden. */
export function RequireCapability({ capability }: { capability: Capability }) {
  const allowed = useCapability(capability)
  return allowed ? <Outlet /> : <Navigate to={FORBIDDEN_PATH} replace />
}
