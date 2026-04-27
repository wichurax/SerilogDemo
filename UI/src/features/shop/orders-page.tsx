import { startTransition } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Search } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { z } from 'zod'

import { EmptyState } from '@/components/shared/empty-state'
import { StatusBadge } from '@/components/shared/status-badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { resolveVisibleOrderStatus } from '@/features/shop/order-status'
import { getOrderByNumber, getOrders } from '@/lib/api/client'
import { getErrorMessage } from '@/lib/api/http'
import { formatCurrency, formatDateTime, pluralize } from '@/lib/format'
import { useDemoUserStore } from '@/stores/demo-user-store'

const lookupSchema = z.object({
  orderNumber: z.string().trim().min(1, 'Enter an order number first.'),
})

type LookupValues = z.infer<typeof lookupSchema>

export function OrdersPage() {
  const userId = useDemoUserStore((state) => state.userId)
  const navigate = useNavigate()

  const ordersQuery = useQuery({
    queryKey: ['orders', userId],
    queryFn: () => getOrders(userId),
  })

  const lookupForm = useForm<LookupValues>({
    resolver: zodResolver(lookupSchema),
    defaultValues: { orderNumber: '' },
  })

  const lookupMutation = useMutation({
    mutationFn: (values: LookupValues) => getOrderByNumber(userId, values.orderNumber),
    onSuccess: (order) => {
      startTransition(() => navigate(`/orders/${order.id}`))
    },
    onError: (error) => {
      toast.error(getErrorMessage(error))
    },
  })

  const orders = ordersQuery.data ?? []

  return (
    <div className='section-grid'>
      <div className='grid gap-4 xl:grid-cols-[0.8fr_1.2fr]'>
        <Card>
          <CardHeader>
            <CardTitle>Lookup by order number</CardTitle>
            <CardDescription>The backend expects an exact order number plus the current X-User-Id context.</CardDescription>
          </CardHeader>
          <CardContent>
            <form className='space-y-4' onSubmit={lookupForm.handleSubmit((values) => lookupMutation.mutate(values))}>
              <div className='space-y-2'>
                <div className='relative'>
                  <Search className='pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-muted-foreground' />
                  <Input className='pl-11' placeholder='ORD-2026...' {...lookupForm.register('orderNumber')} />
                </div>
                {lookupForm.formState.errors.orderNumber ? (
                  <p className='text-sm text-destructive'>{lookupForm.formState.errors.orderNumber.message}</p>
                ) : null}
              </div>
              <Button className='w-full' disabled={lookupMutation.isPending} type='submit'>
                {lookupMutation.isPending ? 'Searching...' : 'Open order'}
              </Button>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{pluralize('order', orders.length)}</CardTitle>
            <CardDescription>Recent orders for {userId}. Shipping status will eventually mirror warehouse progression after projections catch up.</CardDescription>
          </CardHeader>
          <CardContent className='space-y-4'>
            {ordersQuery.isLoading ? (
              <p className='text-sm text-muted-foreground'>Loading orders...</p>
            ) : ordersQuery.isError ? (
              <EmptyState title='Order list failed' description={getErrorMessage(ordersQuery.error)} />
            ) : orders.length === 0 ? (
              <EmptyState
                title='No orders for this user yet'
                description='Go through checkout once and the order history will populate here, even if the payment path fails.'
              />
            ) : (
              orders.map((order) => (
                <div key={order.orderId} className='rounded-xl border border-border/70 bg-white p-4'>
                  <div className='flex flex-col gap-4 md:flex-row md:items-center md:justify-between'>
                    <div className='space-y-2'>
                      <div className='space-y-1'>
                        <p className='text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground'>Order no.</p>
                        <p className='font-mono text-base font-semibold tracking-[0.04em] text-foreground md:text-lg'>{order.orderNumber}</p>
                      </div>
                      <StatusBadge value={resolveVisibleOrderStatus(order.status, order.fulfillmentStatus)} />
                      <p className='text-sm text-muted-foreground'>Placed {formatDateTime(order.createdAt)}</p>
                    </div>
                    <div className='flex items-center gap-3'>
                      <p className='font-mono text-lg text-foreground'>{formatCurrency(order.totalPrice)}</p>
                      <Button asChild variant='secondary'>
                        <Link to={`/orders/${order.orderId}`}>Details</Link>
                      </Button>
                    </div>
                  </div>
                </div>
              ))
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}