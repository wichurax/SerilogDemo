import { Boxes, CreditCard, PackageSearch, ShoppingCart, ReceiptText, type LucideIcon } from 'lucide-react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'

import { UserSwitcher } from '@/components/layout/user-switcher'
import { cn } from '@/lib/utils'

const customerNavItems = [
  {
    to: '/catalog',
    label: 'Catalog',
    icon: PackageSearch,
  },
  {
    to: '/cart',
    label: 'Cart',
    icon: ShoppingCart,
  },
  {
    to: '/checkout',
    label: 'Checkout',
    icon: CreditCard,
  },
  {
    to: '/orders',
    label: 'Orders',
    icon: ReceiptText,
  },
] as const

const operationsNavItems = [
  {
    to: '/warehouse',
    label: 'Warehouse',
    icon: Boxes,
  },
] as const

export function AppShell() {
  const location = useLocation()

  return (
    <div className='min-h-screen lg:flex'>
      {/* ── Sidebar ── */}
      <aside className='fixed inset-y-0 left-0 z-50 flex w-14 flex-col border-r border-border/70 bg-white shadow-sm lg:w-56'>
        <nav className='flex flex-1 flex-col gap-1 overflow-y-auto px-2 py-4 lg:px-3'>
          <NavSection items={customerNavItems} />
          <div aria-hidden className='my-2 h-px w-full bg-border/80' />
          <NavSection items={operationsNavItems} />
        </nav>

        <div className='border-t border-border/70 px-2 py-3 lg:px-3'>
          <UserSwitcher compact />
        </div>
      </aside>

      {/* ── Main content ── */}
      <main className='min-h-screen flex-1 pl-14 lg:pl-56'>
        <div className='mx-auto max-w-375 space-y-6 px-4 py-6 md:px-6 lg:px-8'>
          <div key={location.pathname}>
            <Outlet />
          </div>
        </div>
      </main>
    </div>
  )
}

type NavSectionProps = {
  items: readonly {
    to: string
    label: string
    icon: LucideIcon
  }[]
}

function NavSection({ items }: NavSectionProps) {
  return (
    <div className='flex flex-col gap-1'>
      {items.map(({ to, label, icon: Icon }) => (
        <NavLink
          key={to}
          to={to}
          end={to !== '/orders'}
          reloadDocument
          className={({ isActive }) =>
            cn(
              'flex items-center gap-3 rounded-md px-2.5 py-2 text-sm font-medium transition lg:px-3',
              isActive
                ? 'bg-primary text-primary-foreground shadow-sm'
                : 'text-secondary-foreground hover:bg-muted',
            )
          }
        >
          <Icon className='size-4 shrink-0' />
          <span className='hidden lg:inline'>{label}</span>
        </NavLink>
      ))}
    </div>
  )
}