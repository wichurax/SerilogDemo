import { AlertTriangle } from 'lucide-react'
import {
  Navigate,
  RouterProvider,
  createBrowserRouter,
  isRouteErrorResponse,
  useRouteError,
} from 'react-router-dom'

import { AppShell } from '@/components/layout/app-shell'
import { LoadingBlock } from '@/components/shared/loading-block'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    errorElement: <RouteErrorBoundary />,
    children: [
      {
        index: true,
        element: <Navigate to='/catalog' replace />,
      },
      {
        path: 'catalog',
        hydrateFallbackElement: <RouteLoadingState />,
        lazy: async () => {
          const { CatalogPage } = await import('@/features/shop/catalog-page')
          return { Component: CatalogPage }
        },
      },
      {
        path: 'cart',
        hydrateFallbackElement: <RouteLoadingState />,
        lazy: async () => {
          const { CartPage } = await import('@/features/shop/cart-page')
          return { Component: CartPage }
        },
      },
      {
        path: 'checkout',
        hydrateFallbackElement: <RouteLoadingState />,
        lazy: async () => {
          const { CheckoutPage } = await import('@/features/shop/checkout-page')
          return { Component: CheckoutPage }
        },
      },
      {
        path: 'orders',
        hydrateFallbackElement: <RouteLoadingState />,
        lazy: async () => {
          const { OrdersPage } = await import('@/features/shop/orders-page')
          return { Component: OrdersPage }
        },
      },
      {
        path: 'orders/:orderId',
        hydrateFallbackElement: <RouteLoadingState />,
        lazy: async () => {
          const { OrderDetailPage } = await import('@/features/shop/order-detail-page')
          return { Component: OrderDetailPage }
        },
      },
      {
        path: 'warehouse',
        hydrateFallbackElement: <RouteLoadingState />,
        lazy: async () => {
          const { WarehousePage } = await import('@/features/warehouse/warehouse-page')
          return { Component: WarehousePage }
        },
      },
    ],
  },
])

export default function App() {
  return <RouterProvider router={router} />
}

function RouteLoadingState() {
  return (
    <div className='section-grid'>
      <div className='space-y-3'>
        <LoadingBlock className='h-4 w-28' />
        <LoadingBlock className='h-10 max-w-lg' />
      </div>

      <div className='grid gap-4 xl:grid-cols-[1.2fr_0.8fr]'>
        <LoadingBlock className='h-56 w-full' />
        <LoadingBlock className='h-56 w-full' />
      </div>
    </div>
  )
}

function RouteErrorBoundary() {
  const error = useRouteError()

  return (
    <div className='flex min-h-screen items-center justify-center px-4'>
      <Card className='w-full max-w-xl'>
        <CardHeader>
          <div className='mb-3 inline-flex size-12 items-center justify-center rounded-full bg-destructive/10 text-destructive'>
            <AlertTriangle className='size-5' />
          </div>
          <CardTitle>Something broke in the route tree.</CardTitle>
          <p className='text-sm leading-6 text-muted-foreground'>
            {isRouteErrorResponse(error)
              ? `${error.status} ${error.statusText}`
              : error instanceof Error
                ? error.message
                : 'Unexpected routing error'}
          </p>
        </CardHeader>
        <CardContent>
          <Button asChild>
            <a href='/catalog'>Return to the shop</a>
          </Button>
        </CardContent>
      </Card>
    </div>
  )
}
