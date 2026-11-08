/**
 * Success / failure callbacks a mutation hook runs at hook level. Use it when the mutation's own
 * refetch can unmount the component that started it (a decided row leaves its list): TanStack
 * Query drops callbacks passed to `mutate()` once their caller is gone, hook-level ones still run.
 */
export interface MutationOutcome<TInput> {
  onSuccess?: (input: TInput) => void
  onError?: (error: Error, input: TInput) => void
}
