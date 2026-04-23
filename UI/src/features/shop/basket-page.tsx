import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Minus, Plus, Trash2 } from 'lucide-react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'

import { EmptyState } from '@/components/shared/empty-state'
import { LoadingBlock } from '@/components/shared/loading-block'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { clearBasket, getBasket, removeBasketItem, updateBasketItem } from '@/lib/api/client'
import { getErrorMessage } from '@/lib/api/http'
import { formatCurrency, pluralize } from '@/lib/format'
import { useDemoUserStore } from '@/stores/demo-user-store'

export function BasketPage() {
  const userId = useDemoUserStore((state) => state.userId)
  const queryClient = useQueryClient()

  const basketQuery = useQuery({
    queryKey: ['basket', userId],
    queryFn: () => getBasket(userId),
  })

  async function refreshViews() {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['basket', userId] }),
      queryClient.invalidateQueries({ queryKey: ['orders', userId] }),
    ])
  }

  const updateQuantityMutation = useMutation({
    mutationFn: ({ itemId, quantity }: { itemId: string; quantity: number }) => updateBasketItem(userId, itemId, quantity),
    onSuccess: async () => {
      await refreshViews()
    },
    onError: (error) => {
      toast.error(getErrorMessage(error))
    },
  })

  const removeItemMutation = useMutation({
    mutationFn: (itemId: string) => removeBasketItem(userId, itemId),
    onSuccess: async () => {
      await refreshViews()
      toast.success('Item removed from basket.')
    },
    onError: (error) => {
      toast.error(getErrorMessage(error))
    },
  })

  const clearBasketMutation = useMutation({
    mutationFn: () => clearBasket(userId),
    onSuccess: async () => {
      await refreshViews()
      toast.success('Basket cleared.')
    },
    onError: (error) => {
      toast.error(getErrorMessage(error))
    },
  })

  if (basketQuery.isLoading) {
    return (
      <div className='section-grid'>
        <LoadingBlock className='h-32' />
        <LoadingBlock className='h-80' />
      </div>
    )
  }

  if (basketQuery.isError) {
    return <EmptyState title='Basket failed to load' description={getErrorMessage(basketQuery.error)} />
  }

  const basket = basketQuery.data

  if (!basket || basket.items.length === 0) {
    return (
      <div className='section-grid'>
        <EmptyState
          title='Your basket is empty'
          description='Start in the shop, add inventory-backed items, and return here when you want to shape the order before payment.'
          action={
            <Button asChild>
              <Link to='/catalog'>Browse the catalog</Link>
            </Button>
          }
        />
      </div>
    )
  }

  return (
    <div className='section-grid'>
      <div className='grid gap-4 xl:grid-cols-[1.3fr_0.7fr]'>
        <Card>
          <CardHeader>
            <CardTitle>{pluralize('line item', basket.items.length)}</CardTitle>
            <CardDescription>
              Adjust counts inline or remove anything you do not want to reserve during checkout.
            </CardDescription>
          </CardHeader>
          <CardContent className='space-y-4'>
            {basket.items.map((item) => {
              const isUpdating = updateQuantityMutation.isPending && updateQuantityMutation.variables?.itemId === item.itemId
              const isRemoving = removeItemMutation.isPending && removeItemMutation.variables === item.itemId

              return (
                <div key={item.id} className='rounded-xl border border-border/70 bg-white p-4'>
                  <div className='flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between'>
                    <div className='space-y-1'>
                      <h2 className='text-lg font-semibold tracking-[-0.03em] text-foreground'>{item.itemName}</h2>
                      <p className='font-mono text-sm text-muted-foreground'>
                        {formatCurrency(item.unitPrice)} each · {formatCurrency(item.totalPrice)} line total
                      </p>
                    </div>

                    <div className='flex flex-wrap items-center gap-3'>
                      <div className='inline-flex items-center gap-2 rounded-lg border border-border bg-background p-1'>
                        <button
                          type='button'
                          className='inline-flex size-9 items-center justify-center rounded-md text-secondary-foreground transition hover:bg-secondary'
                          disabled={isUpdating}
                          onClick={() =>
                            updateQuantityMutation.mutate({
                              itemId: item.itemId,
                              quantity: item.quantity - 1,
                            })
                          }
                        >
                          <Minus className='size-4' />
                        </button>
                        <span className='min-w-10 text-center font-mono text-sm text-foreground'>
                          {isUpdating ? '...' : item.quantity}
                        </span>
                        <button
                          type='button'
                          className='inline-flex size-9 items-center justify-center rounded-md text-secondary-foreground transition hover:bg-secondary'
                          disabled={isUpdating}
                          onClick={() =>
                            updateQuantityMutation.mutate({
                              itemId: item.itemId,
                              quantity: item.quantity + 1,
                            })
                          }
                        >
                          <Plus className='size-4' />
                        </button>
                      </div>

                      <Button
                        variant='ghost'
                        size='sm'
                        disabled={isRemoving}
                        onClick={() => removeItemMutation.mutate(item.itemId)}
                      >
                        <Trash2 className='size-4' />
                        {isRemoving ? 'Removing...' : 'Remove'}
                      </Button>
                    </div>
                  </div>
                </div>
              )
            })}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Order snapshot</CardTitle>
            <CardDescription>The backend will add delivery cost later, but this is the basket value about to enter checkout.</CardDescription>
          </CardHeader>
          <CardContent className='space-y-5'>
            <div className='rounded-xl border border-border/70 bg-secondary p-4'>
              <p className='text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground'>Subtotal</p>
              <p className='mt-2 font-mono text-3xl text-foreground'>{formatCurrency(basket.totalPrice)}</p>
            </div>
            <div className='space-y-3 text-sm text-muted-foreground'>
              <div className='flex items-center justify-between'>
                <span>Total units</span>
                <span className='font-mono text-foreground'>{basket.totalItems}</span>
              </div>
              <div className='flex items-center justify-between'>
                <span>User scope</span>
                <span className='font-mono text-foreground'>{basket.userId}</span>
              </div>
            </div>
            <div className='grid gap-3'>
              <Button asChild className='w-full'>
                <Link to='/checkout'>Continue to checkout</Link>
              </Button>
              <Button
                variant='secondary'
                className='w-full'
                disabled={clearBasketMutation.isPending}
                onClick={() => clearBasketMutation.mutate()}
              >
                {clearBasketMutation.isPending ? 'Clearing basket...' : 'Clear basket'}
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}