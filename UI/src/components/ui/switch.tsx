import type { ComponentProps } from 'react'

import { cn } from '@/lib/utils'

type SwitchProps = Omit<ComponentProps<'button'>, 'onChange'> & {
  checked: boolean
  onCheckedChange?: (checked: boolean) => void
}

export function Switch({
  checked,
  onCheckedChange,
  className,
  disabled,
  type,
  onClick,
  ...props
}: SwitchProps) {
  return (
    <button
      type={type ?? 'button'}
      role="switch"
      aria-checked={checked}
      data-state={checked ? 'checked' : 'unchecked'}
      disabled={disabled}
      className={cn(
        'peer inline-flex h-6 w-11 shrink-0 cursor-pointer items-center rounded-full border border-transparent bg-muted px-0.5 transition-colors outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background disabled:cursor-not-allowed disabled:opacity-45 data-[state=checked]:bg-primary',
        className,
      )}
      onClick={(event) => {
        onClick?.(event)

        if (!event.defaultPrevented) {
          onCheckedChange?.(!checked)
        }
      }}
      {...props}>
      <span
        aria-hidden="true"
        className={cn(
          'block size-5 rounded-full bg-background shadow-sm transition-transform',
          checked && 'translate-x-5',
        )}
      />
    </button>
  )
}