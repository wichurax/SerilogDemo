import * as SelectPrimitive from '@radix-ui/react-select'
import { Check, ChevronDown, ChevronUp } from 'lucide-react'

import { cn } from '@/lib/utils'

export const Select = SelectPrimitive.Root
export const SelectValue = SelectPrimitive.Value

export function SelectTrigger({ className, children, ...props }: React.ComponentProps<typeof SelectPrimitive.Trigger>) {
  return (
    <SelectPrimitive.Trigger
      className={cn(
        'flex h-11 w-full items-center justify-between rounded-lg border border-border bg-input px-4 py-2 text-left text-sm text-foreground outline-none transition data-placeholder:text-muted-foreground focus-visible:ring-2 focus-visible:ring-ring',
        className,
      )}
      {...props}
    >
      {children}
      <SelectPrimitive.Icon asChild>
        <ChevronDown className='size-4 text-muted-foreground' />
      </SelectPrimitive.Icon>
    </SelectPrimitive.Trigger>
  )
}

export function SelectContent({ className, children, position = 'popper', ...props }: React.ComponentProps<typeof SelectPrimitive.Content>) {
  return (
    <SelectPrimitive.Portal>
      <SelectPrimitive.Content
        className={cn(
          'z-50 min-w-48 overflow-hidden rounded-lg border border-border bg-popover text-popover-foreground shadow-md',
          position === 'popper' && 'translate-y-1',
          className,
        )}
        position={position}
        {...props}
      >
        <SelectPrimitive.ScrollUpButton className='flex items-center justify-center py-2'>
          <ChevronUp className='size-4 text-muted-foreground' />
        </SelectPrimitive.ScrollUpButton>
        <SelectPrimitive.Viewport className='p-2'>{children}</SelectPrimitive.Viewport>
        <SelectPrimitive.ScrollDownButton className='flex items-center justify-center py-2'>
          <ChevronDown className='size-4 text-muted-foreground' />
        </SelectPrimitive.ScrollDownButton>
      </SelectPrimitive.Content>
    </SelectPrimitive.Portal>
  )
}

export function SelectItem({ className, children, ...props }: React.ComponentProps<typeof SelectPrimitive.Item>) {
  return (
    <SelectPrimitive.Item
      className={cn(
        'relative flex w-full cursor-default select-none items-center rounded-md py-2.5 pl-9 pr-3 text-sm text-foreground outline-none transition focus:bg-secondary',
        className,
      )}
      {...props}
    >
      <span className='absolute left-3 flex size-4 items-center justify-center'>
        <SelectPrimitive.ItemIndicator>
          <Check className='size-4 text-primary' />
        </SelectPrimitive.ItemIndicator>
      </span>
      <SelectPrimitive.ItemText>{children}</SelectPrimitive.ItemText>
    </SelectPrimitive.Item>
  )
}