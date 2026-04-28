import { useEffect, useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import type { FulfillmentAttempt, WarehouseInventory } from '@/lib/api/types'
import { humanizeLabel } from '@/lib/format'

export const fulfillmentStatusOptions = [
  'All',
  'Reserved',
  'Collected',
  'Packed',
  'Shipped',
  'Failed',
] as const

export type FulfillmentDialogState = {
  action: 'collect' | 'pack' | 'ship' | 'fail'
  attempt: FulfillmentAttempt
} | null

export type InventoryDialogState = {
  action: 'restock' | 'writeOff' | 'recount'
  item: WarehouseInventory
} | null

const inventoryAdjustmentSchema = z.object({
  quantity: z.number().int().min(1, 'Enter a value greater than zero.'),
  reason: z.string().trim().max(200, 'Keep reasons under 200 characters.'),
})

export type FulfillmentActionValues = {
  trackingReference: string
  message: string
}

export type InventoryAdjustmentValues = z.infer<typeof inventoryAdjustmentSchema>

export function useDebouncedValue(value: string, delayMilliseconds = 220) {
  const [debouncedValue, setDebouncedValue] = useState(value)

  useEffect(() => {
    const timeoutId = window.setTimeout(() => setDebouncedValue(value), delayMilliseconds)
    return () => window.clearTimeout(timeoutId)
  }, [delayMilliseconds, value])

  return debouncedValue
}

export function FulfillmentActionDialog({
  state,
  isPending,
  onClose,
  onSubmit,
}: {
  state: FulfillmentDialogState
  isPending: boolean
  onClose: () => void
  onSubmit: (values: FulfillmentActionValues) => void
}) {
  const fulfillmentActionSchema = z
    .object({
      trackingReference: z.string().trim().max(80, 'Keep tracking references under 80 characters.'),
      message: z.string().trim().max(200, 'Keep operator notes under 200 characters.'),
    })
    .superRefine((values, context) => {
      if (state?.action === 'ship' && values.trackingReference.length === 0) {
        context.addIssue({
          code: 'custom',
          path: ['trackingReference'],
          message: 'Tracking reference is required when shipping.',
        })
      }
    })

  const form = useForm<FulfillmentActionValues>({
    resolver: zodResolver(fulfillmentActionSchema),
    defaultValues: {
      trackingReference: '',
      message: '',
    },
  })

  useEffect(() => {
    if (!state) {
      return
    }

    form.reset({
      trackingReference: state.action === 'ship' ? `SHIP-${state.attempt.orderNumber}` : '',
      message: '',
    })
  }, [form, state])

  return (
    <Dialog open={Boolean(state)} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {state ? `${humanizeLabel(state.action)} ${state.attempt.orderNumber}` : 'Warehouse action'}
          </DialogTitle>
          <DialogDescription>
            {state
              ? 'Submit an operator note and, when shipping, a tracking reference. The transition still goes through the real fulfillment API.'
              : 'No action selected.'}
          </DialogDescription>
        </DialogHeader>

        {state ? (
          <form className='space-y-4' onSubmit={form.handleSubmit(onSubmit)}>
            {state.action === 'ship' ? (
              <div className='space-y-2'>
                <Label htmlFor='trackingReference'>Tracking reference</Label>
                <Input id='trackingReference' {...form.register('trackingReference')} />
                {form.formState.errors.trackingReference ? (
                  <p className='text-sm text-destructive'>{form.formState.errors.trackingReference.message}</p>
                ) : null}
              </div>
            ) : null}

            <div className='space-y-2'>
              <Label htmlFor='message'>Operator note</Label>
              <Textarea id='message' placeholder='Optional note for the event stream' {...form.register('message')} />
              {form.formState.errors.message ? (
                <p className='text-sm text-destructive'>{form.formState.errors.message.message}</p>
              ) : null}
            </div>

            <DialogFooter>
              <Button type='button' variant='ghost' onClick={onClose}>
                Cancel
              </Button>
              <Button type='submit' disabled={isPending}>
                {isPending ? 'Submitting...' : `Confirm ${humanizeLabel(state.action)}`}
              </Button>
            </DialogFooter>
          </form>
        ) : null}
      </DialogContent>
    </Dialog>
  )
}

export function InventoryActionDialog({
  state,
  isPending,
  onClose,
  onSubmit,
}: {
  state: InventoryDialogState
  isPending: boolean
  onClose: () => void
  onSubmit: (values: InventoryAdjustmentValues) => void
}) {
  const form = useForm<InventoryAdjustmentValues>({
    resolver: zodResolver(inventoryAdjustmentSchema),
    defaultValues: {
      quantity: 1,
      reason: '',
    },
  })

  useEffect(() => {
    if (!state) {
      return
    }

    form.reset({
      quantity: state.action === 'recount' ? state.item.quantityOnHand : 1,
      reason: '',
    })
  }, [form, state])

  return (
    <Dialog open={Boolean(state)} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {state ? `${humanizeLabel(state.action)} ${state.item.itemName}` : 'Inventory action'}
          </DialogTitle>
          <DialogDescription>
            {state ? 'This command uses the existing warehouse endpoints on the main API.' : 'No inventory row selected.'}
          </DialogDescription>
        </DialogHeader>

        {state ? (
          <form className='space-y-4' onSubmit={form.handleSubmit(onSubmit)}>
            <div className='space-y-2'>
              <Label htmlFor='quantity'>{state.action === 'recount' ? 'New on-hand quantity' : 'Quantity'}</Label>
              <Input id='quantity' type='number' min={1} {...form.register('quantity', { valueAsNumber: true })} />
              {form.formState.errors.quantity ? (
                <p className='text-sm text-destructive'>{form.formState.errors.quantity.message}</p>
              ) : null}
            </div>

            <div className='space-y-2'>
              <Label htmlFor='reason'>Reason</Label>
              <Textarea id='reason' placeholder='Optional note for warehouse history' {...form.register('reason')} />
              {form.formState.errors.reason ? (
                <p className='text-sm text-destructive'>{form.formState.errors.reason.message}</p>
              ) : null}
            </div>

            <DialogFooter>
              <Button type='button' variant='ghost' onClick={onClose}>
                Cancel
              </Button>
              <Button type='submit' disabled={isPending} variant={state.action === 'writeOff' ? 'destructive' : 'default'}>
                {isPending ? 'Submitting...' : `Confirm ${humanizeLabel(state.action)}`}
              </Button>
            </DialogFooter>
          </form>
        ) : null}
      </DialogContent>
    </Dialog>
  )
}