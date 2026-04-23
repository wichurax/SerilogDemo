export function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 2,
  }).format(value)
}

export function formatDateTime(value: Date | string | null | undefined) {
  if (!value) {
    return 'Not available yet'
  }

  return new Intl.DateTimeFormat('en-US', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(typeof value === 'string' ? new Date(value) : value)
}

export function humanizeLabel(value: string) {
  return value
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/[-_]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/\b\w/g, (character) => character.toUpperCase())
}

export function toneForStatus(value: string) {
  const normalized = value.toLowerCase()

  if (
    normalized.includes('authorized') ||
    normalized.includes('confirmed') ||
    normalized.includes('delivered') ||
    normalized.includes('shipped') ||
    normalized.includes('success')
  ) {
    return 'success' as const
  }

  if (
    normalized.includes('declined') ||
    normalized.includes('cancelled') ||
    normalized.includes('timeout') ||
    normalized.includes('failed')
  ) {
    return 'danger' as const
  }

  if (
    normalized.includes('pending') ||
    normalized.includes('reserved') ||
    normalized.includes('collected') ||
    normalized.includes('packed') ||
    normalized.includes('processing')
  ) {
    return 'warning' as const
  }

  return 'neutral' as const
}

export function pluralize(noun: string, count: number) {
  return `${count} ${noun}${count === 1 ? '' : 's'}`
}