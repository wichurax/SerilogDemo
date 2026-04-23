import type { ReactNode } from 'react'

import { cn } from '@/lib/utils'

type PageHeaderProps = {
  eyebrow?: string
  title: ReactNode
  description?: ReactNode
  aside?: ReactNode
  className?: string
}

export function PageHeader({ eyebrow, title, description, aside, className }: PageHeaderProps) {
  return (
    <div className={cn('grid gap-3 lg:grid-cols-[1fr_auto] lg:items-end', className)}>
      <div className='space-y-1.5'>
        {eyebrow ? <p className='text-xs font-semibold uppercase tracking-[0.2em] text-muted-foreground'>{eyebrow}</p> : null}
        <h1 className='max-w-4xl font-serif text-2xl font-semibold tracking-[-0.02em] text-foreground md:text-3xl'>{title}</h1>
        {description ? <p className='max-w-3xl text-sm leading-6 text-muted-foreground'>{description}</p> : null}
      </div>
      {aside ? <div className='lg:justify-self-end'>{aside}</div> : null}
    </div>
  )
}