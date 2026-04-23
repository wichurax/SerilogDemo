import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export const presetUserIds = ['demo-user-123', 'demo-user-002', 'workshop-user-001'] as const

type DemoUserStore = {
  userId: string
  setUserId: (userId: string) => void
}

export const useDemoUserStore = create<DemoUserStore>()(
  persist(
    (set) => ({
      userId: presetUserIds[0],
      setUserId: (userId) => set({ userId }),
    }),
    {
      name: 'signalcart-demo-user',
    },
  ),
)