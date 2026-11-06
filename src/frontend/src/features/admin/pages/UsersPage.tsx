import { PageHeader } from '@/components/layout/PageHeader'
import { ButtonLink } from '@/components/ui/ButtonLink'
import { UsersList } from '../components/UsersList'

export function UsersPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Users"
        subtitle="Create people, assign departments, managers and roles."
        actions={<ButtonLink to="/admin/users/new">New user</ButtonLink>}
      />
      <UsersList />
    </div>
  )
}
