# SignalCart UI

This folder contains the Bun + Vite + React frontend for the SerilogDemo workflow.

In the full compose stack, a user opens this UI manually in a browser at `http://localhost:8080`. The k6 scripts are the automated counterpart: they can hit the same load-balancer routes instead of driving the UI interactively.

## Stack

- React 19
- Vite 8
- Bun for package management and scripts
- Tailwind CSS v4
- shadcn-style component setup
- React Router
- TanStack Query
- React Hook Form + Zod
- Zustand
- TanStack Table

## Commands

```bash
bun install
bun run dev
bun run build
```

## Proxy Model

The web app calls relative paths:

- `/api/*` for the main e-commerce API
- `/fulfillment-api/*` for the Fulfillment API

The Vite dev server proxies those routes. By default both target `http://localhost:8080`, which works when nginx is running from the root docker compose setup.

When the fulfillment proxy target stays on the shared nginx address, Vite now keeps the `/fulfillment-api` prefix intact so nginx can forward the request correctly. If you point `VITE_PROXY_FULFILLMENT_TARGET` directly at the standalone Fulfillment API host, Vite strips that prefix automatically.

If you want to run services separately, copy `.env.example` to `.env.local` and override the proxy targets.

## Current UI Areas

- Shop catalog with search and category filter
- Cart editing
- Checkout with visible fake payment scenarios
- Customer order list and order detail
- Warehouse backlog with collect, pack, ship, and fail actions
- Warehouse inventory table with restock, recount, and write-off actions

