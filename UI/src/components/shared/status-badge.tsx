import { Badge } from '@/components/ui/badge'
import { humanizeLabel, toneForStatus } from '@/lib/format'

type StatusBadgeProps = {
  value: string
}

export function StatusBadge({ value }: StatusBadgeProps) {
  return <Badge variant={toneForStatus(value)}>{humanizeLabel(value)}</Badge>
}