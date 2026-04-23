import { cn } from '@/lib/utils'

type LoadingBlockProps = {
  className?: string
}

export function LoadingBlock({ className }: LoadingBlockProps) {
  return <div className={cn('animate-pulse rounded-lg bg-muted/50', className)} />
}