import { Alert } from '@/components/ui/Alert'
import { Button } from '@/components/ui/Button'
import { FormField } from '@/components/ui/FormField'
import { Textarea } from '@/components/ui/Textarea'
import { toUserMessage } from '@/lib/api/problem'
import { ApiError } from '@/lib/api/apiError'
import { fieldError, useZodForm } from '@/lib/forms/useZodForm'
import { REQUEST_LIMITS, upToCharacters } from '@/lib/api/limits'
import { useAddComment } from '../api'
import { commentSchema } from '../schemas'

/** Adds a plain-text comment to the request's history. Only rendered when the server says `canComment`. */
export function CommentBox({ requestId }: { requestId: string }) {
  const add = useAddComment(requestId)
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useZodForm(commentSchema, { defaultValues: { body: '' } })

  const serverBody = add.error instanceof ApiError ? add.error.fieldErrors.body?.[0] : undefined
  const bannerMessage = add.isError && !serverBody ? toUserMessage(add.error) : null

  return (
    <form
      noValidate
      className="grid gap-3"
      onSubmit={(event) =>
        void handleSubmit((values) =>
          add.mutate(values.body, {
            onSuccess: () => reset(),
            onError: (error) => {
              const message = error instanceof ApiError ? error.fieldErrors.body?.[0] : undefined
              if (message) setError('body', { type: 'server', message })
            },
          }),
        )(event)
      }
    >
      {bannerMessage && <Alert tone="danger">{bannerMessage}</Alert>}
      <FormField label="Add a comment" helperText={upToCharacters(REQUEST_LIMITS.commentMax)} error={fieldError(errors, 'body')}>
        {(c) => <Textarea {...c} rows={3} {...register('body')} />}
      </FormField>
      <div className="flex justify-end">
        <Button type="submit" variant="secondary" loading={add.isPending}>
          Post comment
        </Button>
      </div>
    </form>
  )
}
