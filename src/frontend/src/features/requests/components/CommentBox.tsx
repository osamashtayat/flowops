import { Alert } from '@/components/ui/Alert'
import { Button } from '@/components/ui/Button'
import { FormField } from '@/components/ui/FormField'
import { Textarea } from '@/components/ui/Textarea'
import { serverFieldError, toUserMessage } from '@/lib/api/problem'
import type { RequestType } from '@/lib/api/types'
import { withLeaveVisibilityWarning } from '@/lib/format/visibility'
import { fieldError, setServerFieldErrors, useZodForm } from '@/lib/forms/useZodForm'
import { REQUEST_LIMITS, upToCharacters } from '@/lib/api/limits'
import { useAddComment } from '../api'
import { commentSchema } from '../schemas'

/** Adds a plain-text comment to the request's history. Only rendered when the server says `canComment`. */
export function CommentBox({ requestId, requestType }: { requestId: string; requestType: RequestType }) {
  const add = useAddComment(requestId)
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useZodForm(commentSchema, { defaultValues: { body: '' } })

  const serverBody = serverFieldError(add.error, 'body')
  const bannerMessage = add.isError && !serverBody ? toUserMessage(add.error) : null

  return (
    <form
      noValidate
      className="grid gap-3"
      onSubmit={(event) =>
        void handleSubmit((values) =>
          add.mutate(values.body, {
            onSuccess: () => reset(),
            onError: (error) => setServerFieldErrors(error, ['body'], setError),
          }),
        )(event)
      }
    >
      {bannerMessage && <Alert tone="danger">{bannerMessage}</Alert>}
      <FormField label="Add a comment" helperText={withLeaveVisibilityWarning(upToCharacters(REQUEST_LIMITS.commentMax), requestType)} error={fieldError(errors, 'body')}>
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
