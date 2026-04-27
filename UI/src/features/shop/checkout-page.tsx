import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { zodResolver } from '@hookform/resolvers/zod'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { CreditCard, LoaderCircle, Truck } from 'lucide-react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { z } from 'zod'

import { EmptyState } from '@/components/shared/empty-state'
import { StatusBadge } from '@/components/shared/status-badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { getCart, getDeliveryOptions, getPaymentOptions, placeOrder } from '@/lib/api/client'
import { getErrorMessage } from '@/lib/api/http'
import type { CheckoutResult, PaymentScenario } from '@/lib/api/types'
import { formatCurrency } from '@/lib/format'
import { useDemoUserStore } from '@/stores/demo-user-store'

const paymentScenarioValues = ['Success', 'Decline', 'SlowSuccess', 'Timeout'] as const
const guidPattern = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/

const checkoutSchema = z.object({
  deliveryOptionId: z.string().trim().regex(guidPattern, 'Pick a delivery option.'),
  paymentOptionId: z.string().trim().regex(guidPattern, 'Pick a payment option.'),
  paymentScenario: z.enum(paymentScenarioValues),
})

type CheckoutFormValues = z.infer<typeof checkoutSchema>

export function CheckoutPage() {
  const userId = useDemoUserStore((state) => state.userId)
  const queryClient = useQueryClient()
  const [result, setResult] = useState<CheckoutResult | null>(null)

  const cartQuery = useQuery({
    queryKey: ['cart', userId],
    queryFn: () => getCart(userId),
  })

  const deliveryOptionsQuery = useQuery({
    queryKey: ['delivery-options'],
    queryFn: getDeliveryOptions,
  })

  const paymentOptionsQuery = useQuery({
    queryKey: ['payment-options'],
    queryFn: getPaymentOptions,
  })

  const form = useForm<CheckoutFormValues>({
    resolver: zodResolver(checkoutSchema),
    defaultValues: {
      deliveryOptionId: '',
      paymentOptionId: '',
      paymentScenario: 'Success',
    },
  })

  const selectedDeliveryOptionId = useWatch({ control: form.control, name: 'deliveryOptionId' })
  const selectedPaymentOptionId = useWatch({ control: form.control, name: 'paymentOptionId' })

  useEffect(() => {
    const currentDeliveryOptionId = form.getValues('deliveryOptionId')
    const currentPaymentOptionId = form.getValues('paymentOptionId')
    const nextDeliveryOptionId = deliveryOptionsQuery.data?.some((option) => option.id === currentDeliveryOptionId)
      ? currentDeliveryOptionId
      : deliveryOptionsQuery.data?.[0]?.id ?? ''
    const nextPaymentOptionId = paymentOptionsQuery.data?.some((option) => option.id === currentPaymentOptionId)
      ? currentPaymentOptionId
      : paymentOptionsQuery.data?.[0]?.id ?? ''

    if (nextDeliveryOptionId && nextDeliveryOptionId !== currentDeliveryOptionId) {
      form.setValue('deliveryOptionId', nextDeliveryOptionId)
    }

    if (nextPaymentOptionId && nextPaymentOptionId !== currentPaymentOptionId) {
      form.setValue('paymentOptionId', nextPaymentOptionId)
    }

    form.clearErrors(['deliveryOptionId', 'paymentOptionId'])
  }, [deliveryOptionsQuery.data, form, paymentOptionsQuery.data])

  const checkoutMutation = useMutation({
    mutationFn: (values: CheckoutFormValues) =>
      placeOrder(userId, {
        deliveryOptionId: values.deliveryOptionId,
        paymentOptionId: values.paymentOptionId,
        paymentScenario: values.paymentScenario as PaymentScenario,
      }),
    onSuccess: async (nextResult) => {
      setResult(nextResult)
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['cart', userId] }),
        queryClient.invalidateQueries({ queryKey: ['orders'] }),
        queryClient.invalidateQueries({ queryKey: ['items'] }),
      ])

      if (nextResult.kind === 'authorized') {
        toast.success(`Order ${nextResult.order.orderNumber} placed successfully.`)
      } else {
        toast.error(nextResult.data.message)
      }
    },
    onError: (error) => {
      toast.error(getErrorMessage(error))
    },
  })

  if (cartQuery.isLoading) {
    return <EmptyState title='Loading checkout' description='We are pulling the cart and option lookups before the form appears.' />
  }

  if (cartQuery.isError) {
    return <EmptyState title='Checkout is unavailable' description={getErrorMessage(cartQuery.error)} />
  }

  const cart = cartQuery.data
  if (!cart || cart.items.length === 0) {
    return (
      <div className='section-grid'>
        <EmptyState
          title='Cart is empty'
          description='Go back to the catalog, add a few items, then come back here to drive the payment gateway scenarios.'
          action={
            <Button asChild>
              <Link to='/catalog'>Browse items</Link>
            </Button>
          }
        />
      </div>
    )
  }

  const deliveryOptions = deliveryOptionsQuery.data ?? []
  const paymentOptions = paymentOptionsQuery.data ?? []
  const selectedDelivery = deliveryOptions.find((option) => option.id === selectedDeliveryOptionId)
  const selectedPayment = paymentOptions.find((option) => option.id === selectedPaymentOptionId)

  return (
    <div className='section-grid'>
      <div className='grid gap-4 xl:grid-cols-[1.05fr_0.95fr]'>
        <Card>
          <CardHeader>
            <CardTitle>Checkout form</CardTitle>
            <CardDescription>Choose delivery and payment, then decide whether the fake gateway should authorize instantly, authorize slowly, decline, or time out.</CardDescription>
          </CardHeader>
          <CardContent>
            <form className='space-y-5' onSubmit={form.handleSubmit((values) => checkoutMutation.mutate(values))}>
              <div className='space-y-2'>
                <Label htmlFor='deliveryOptionId'>Delivery option</Label>
                <Controller
                  control={form.control}
                  name='deliveryOptionId'
                  render={({ field }) => (
                    <Select
                      name={field.name}
                      value={field.value || undefined}
                      onValueChange={(value) => {
                        field.onChange(value)
                        form.clearErrors('deliveryOptionId')
                      }}
                    >
                      <SelectTrigger id='deliveryOptionId' onBlur={field.onBlur}>
                        <SelectValue placeholder='Pick a delivery option' />
                      </SelectTrigger>
                      <SelectContent>
                        {deliveryOptions.map((option) => (
                          <SelectItem key={option.id} value={option.id}>
                            {option.courierName} · {option.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                />
                {form.formState.errors.deliveryOptionId ? (
                  <p className='text-sm text-destructive'>{form.formState.errors.deliveryOptionId.message}</p>
                ) : null}
              </div>

              <div className='space-y-2'>
                <Label htmlFor='paymentOptionId'>Payment option</Label>
                <Controller
                  control={form.control}
                  name='paymentOptionId'
                  render={({ field }) => (
                    <Select
                      name={field.name}
                      value={field.value || undefined}
                      onValueChange={(value) => {
                        field.onChange(value)
                        form.clearErrors('paymentOptionId')
                      }}
                    >
                      <SelectTrigger id='paymentOptionId' onBlur={field.onBlur}>
                        <SelectValue placeholder='Pick a payment option' />
                      </SelectTrigger>
                      <SelectContent>
                        {paymentOptions.map((option) => (
                          <SelectItem key={option.id} value={option.id}>
                            {option.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                />
                {form.formState.errors.paymentOptionId ? (
                  <p className='text-sm text-destructive'>{form.formState.errors.paymentOptionId.message}</p>
                ) : null}
              </div>

              <div className='space-y-2'>
                <Label htmlFor='paymentScenario'>Fake payment gateway outcome</Label>
                <Controller
                  control={form.control}
                  name='paymentScenario'
                  render={({ field }) => (
                    <Select value={field.value} onValueChange={field.onChange}>
                      <SelectTrigger id='paymentScenario'>
                        <SelectValue placeholder='Pick a demo scenario' />
                      </SelectTrigger>
                      <SelectContent>
                        {paymentScenarioValues.map((scenario) => (
                          <SelectItem key={scenario} value={scenario}>{scenario}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                />
              </div>

              <Button className='w-full' disabled={checkoutMutation.isPending || deliveryOptionsQuery.isLoading || paymentOptionsQuery.isLoading} type='submit'>
                {checkoutMutation.isPending ? (
                  <>
                    <LoaderCircle className='size-4 animate-spin' />
                    Placing order...
                  </>
                ) : (
                  'Place order'
                )}
              </Button>
            </form>
          </CardContent>
        </Card>

        <div className='space-y-4'>
          <Card>
            <CardHeader>
              <CardTitle>Order summary</CardTitle>
              <CardDescription>The totals below are composed from the live cart plus the selected delivery option.</CardDescription>
            </CardHeader>
            <CardContent className='space-y-4'>
              <div className='space-y-3 rounded-xl border border-border/70 bg-secondary p-4'>
                <div className='flex items-center justify-between'>
                  <span className='text-sm text-muted-foreground'>Items</span>
                  <span className='font-mono text-foreground'>{formatCurrency(cart.totalPrice)}</span>
                </div>
                <div className='flex items-center justify-between'>
                  <span className='text-sm text-muted-foreground'>Delivery</span>
                  <span className='font-mono text-foreground'>{formatCurrency(selectedDelivery?.price ?? 0)}</span>
                </div>
                <div className='flex items-center justify-between border-t border-border/70 pt-3'>
                  <span className='text-sm font-semibold uppercase tracking-[0.16em] text-muted-foreground'>Total</span>
                  <span className='font-mono text-2xl text-foreground'>
                    {formatCurrency(cart.totalPrice + (selectedDelivery?.price ?? 0))}
                  </span>
                </div>
              </div>

              <div className='grid gap-3 md:grid-cols-2'>
                <div className='rounded-lg border border-border/70 bg-white p-4'>
                  <p className='mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground'>
                    <Truck className='size-4' />
                    Delivery
                  </p>
                  {selectedDelivery ? (
                    <>
                      <p className='text-sm font-semibold text-foreground'>{selectedDelivery.courierName} · {selectedDelivery.name}</p>
                      <p className='mt-1 text-sm leading-6 text-muted-foreground'>{selectedDelivery.description}</p>
                    </>
                  ) : (
                    <p className='text-sm text-muted-foreground'>Pick an option to see the detail.</p>
                  )}
                </div>
                <div className='rounded-lg border border-border/70 bg-white p-4'>
                  <p className='mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground'>
                    <CreditCard className='size-4' />
                    Payment
                  </p>
                  {selectedPayment ? (
                    <>
                      <p className='text-sm font-semibold text-foreground'>{selectedPayment.name}</p>
                      <p className='mt-1 text-sm leading-6 text-muted-foreground'>{selectedPayment.description}</p>
                    </>
                  ) : (
                    <p className='text-sm text-muted-foreground'>Pick a payment method to see the detail.</p>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>

          {result ? (
            <Card>
              <CardHeader>
                <CardTitle>Checkout result</CardTitle>
                <CardDescription>The backend response is rendered directly instead of being normalized into fake client-side states.</CardDescription>
              </CardHeader>
              <CardContent className='space-y-4'>
                {result.kind === 'authorized' ? (
                  <>
                    <StatusBadge value={result.order.paymentStatus} />
                    <div className='space-y-2'>
                      <p className='text-lg font-semibold text-foreground'>{result.order.orderNumber}</p>
                      <p className='text-sm leading-6 text-muted-foreground'>
                        Payment cleared, the cart should be emptied, and the order is ready for fulfillment reservation to surface in the warehouse view.
                      </p>
                    </div>
                    <Button asChild className='w-full'>
                      <Link to={`/orders/${result.order.id}`}>Open order details</Link>
                    </Button>
                  </>
                ) : (
                  <>
                    <StatusBadge value={result.data.paymentStatus} />
                    <div className='space-y-2'>
                      <p className='text-lg font-semibold text-foreground'>{result.data.orderNumber}</p>
                      <p className='text-sm leading-6 text-muted-foreground'>{result.data.message}</p>
                    </div>
                    <Button asChild variant='secondary' className='w-full'>
                      <Link to='/orders'>Open order history</Link>
                    </Button>
                  </>
                )}
              </CardContent>
            </Card>
          ) : null}
        </div>
      </div>
    </div>
  )
}