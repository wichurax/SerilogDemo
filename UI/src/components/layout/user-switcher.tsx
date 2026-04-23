import { startTransition, useMemo } from 'react'
import { UserRound } from 'lucide-react'
import { toast } from 'sonner'

import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { cn } from '@/lib/utils'
import { presetUserIds, useDemoUserStore } from '@/stores/demo-user-store'

type UserSwitcherProps = {
  compact?: boolean
}

export function UserSwitcher({ compact = false }: UserSwitcherProps) {
  const userId = useDemoUserStore((state) => state.userId)
  const setUserId = useDemoUserStore((state) => state.setUserId)
  const userOptions = useMemo(() => Array.from(new Set([userId, ...presetUserIds])), [userId])

  function commit(nextUserId: string) {
    if (nextUserId === userId) return

    startTransition(() => setUserId(nextUserId))
    toast.success(`Using ${nextUserId} for customer requests.`)
  }

  return (
    <div
      className={cn(
        'flex items-center gap-3 rounded-lg border border-border/70 bg-white px-3 py-2 shadow-sm',
        compact && 'w-full rounded-none border-0 bg-transparent px-0 py-0 shadow-none',
      )}
    >
      {!compact ? (
        <div className='hidden size-10 items-center justify-center rounded-full bg-accent-soft text-accent sm:flex'>
          <UserRound className='size-4' />
        </div>
      ) : null}

      <div className='min-w-0 flex-1'>
        <Select value={userId} onValueChange={commit}>
          <SelectTrigger
            className={cn(
              'font-mono text-sm',
              compact && 'h-10 border-border/70 bg-background px-0 lg:px-3',
            )}
          >
            {compact ? (
              <span className='flex size-10 shrink-0 items-center justify-center text-muted-foreground lg:hidden'>
                <UserRound className='size-4' />
              </span>
            ) : null}
            <span className={cn('truncate', compact && 'hidden lg:block lg:flex-1')}>
              <SelectValue />
            </span>
          </SelectTrigger>
          <SelectContent side={compact ? 'top' : 'bottom'} align='start'>
            {userOptions.map((option) => {
              const isPresetUser = presetUserIds.some((id) => id === option)
              return (
                <SelectItem key={option} value={option}>
                  {isPresetUser ? option : `${option} (current custom)`}
                </SelectItem>
              )
            })}
          </SelectContent>
        </Select>
      </div>
    </div>
  )
}