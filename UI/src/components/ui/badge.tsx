import { cva, type VariantProps } from 'class-variance-authority'
import type { ComponentProps } from 'react'

import { cn } from '@/lib/utils'

const badgeVariants = cva('inline-flex items-center gap-1 rounded-md border px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.16em]', {
  variants: {
    variant: {
      neutral: 'border-border bg-secondary/85 text-secondary-foreground',
      info: 'border-primary/30 bg-primary/10 text-primary',
      warning: 'border-warning/20 bg-warning/12 text-warning-foreground',
      success: 'border-success/20 bg-success/12 text-success',
      danger: 'border-destructive/20 bg-destructive/12 text-destructive',
      accent: 'border-accent/20 bg-accent/12 text-accent',
    },
  },
  defaultVariants: {
    variant: 'neutral',
  },
})

type BadgeProps = ComponentProps<'span'> & VariantProps<typeof badgeVariants>

export function Badge({ className, variant, ...props }: BadgeProps) {
  return <span className={cn(badgeVariants({ variant }), className)} {...props} />
}