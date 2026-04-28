import type { Dispatch, ReactNode, SetStateAction } from "react";
import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Search } from "lucide-react";
import { toast } from "sonner";

import { EmptyState } from "@/components/shared/empty-state";
import { StatusBadge } from "@/components/shared/status-badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  getFulfillmentAttempts,
  transitionFulfillment,
} from "@/lib/api/client";
import { getErrorMessage } from "@/lib/api/http";
import type { FulfillmentAttempt } from "@/lib/api/types";
import { formatDateTime, humanizeLabel } from "@/lib/format";

import {
  FulfillmentActionDialog,
  type FulfillmentDialogState,
  fulfillmentStatusOptions,
  useDebouncedValue,
} from "./warehouse-shared";
import { cn } from "@/lib/utils";

export function WarehouseBacklogPage() {
  const queryClient = useQueryClient();
  const [statusFilter, setStatusFilter] =
    useState<(typeof fulfillmentStatusOptions)[number]>("All");
  const [orderNumberInput, setOrderNumberInput] = useState("");
  const [fulfillmentDialog, setFulfillmentDialog] =
    useState<FulfillmentDialogState>(null);

  const debouncedOrderNumber = useDebouncedValue(orderNumberInput.trim());

  const backlogQuery = useQuery({
    queryKey: ["fulfillment", statusFilter, debouncedOrderNumber],
    queryFn: () =>
      getFulfillmentAttempts({
        status: statusFilter,
        orderNumber: debouncedOrderNumber || undefined,
      }),
    placeholderData: (previousData) => previousData,
  });

  const advanceMutation = useMutation({
    mutationFn: ({
      orderId,
      action,
      payload,
    }: {
      orderId: string;
      action: "collect" | "pack" | "ship" | "fail";
      payload: { message?: string; trackingReference?: string };
    }) => transitionFulfillment(orderId, action, payload),
    onSuccess: async (_, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["fulfillment"] }),
        queryClient.invalidateQueries({ queryKey: ["orders"] }),
        queryClient.invalidateQueries({ queryKey: ["order"] }),
        queryClient.invalidateQueries({ queryKey: ["warehouse-inventory"] }),
      ]);
      toast.success(`${humanizeLabel(variables.action)} action accepted.`);
      setFulfillmentDialog(null);
    },
    onError: (error) => {
      toast.error(getErrorMessage(error));
    },
  });

  const backlogRows = useMemo(
    () => backlogQuery.data ?? [],
    [backlogQuery.data],
  );

  return (
    <div className="section-grid">
      <div className="space-y-2">
        <div className="space-y-1">
          <h1 className="text-3xl font-semibold tracking-tight text-foreground">
            Warehouse Backlog
          </h1>
          <p className="max-w-3xl text-sm leading-6 text-muted-foreground">
            Process reserved orders, move them through packing, and capture
            shipment failures without leaving the worker queue.
          </p>
        </div>
      </div>

      <Card>
        <CardContent className="grid gap-4 lg:grid-cols-[16rem_18rem_minmax(0,1fr)]">
          <div className="space-y-2">
            <Label className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              Status
            </Label>
            <Select
              value={statusFilter}
              onValueChange={(value) =>
                setStatusFilter(
                  value as (typeof fulfillmentStatusOptions)[number],
                )
              }>
              <SelectTrigger>
                <SelectValue placeholder="All statuses" />
              </SelectTrigger>
              <SelectContent>
                {fulfillmentStatusOptions.map((status) => (
                  <SelectItem key={status} value={status}>
                    {status}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              Order number
            </Label>
            <div className="relative">
              <Search className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                className="pl-11"
                value={orderNumberInput}
                onChange={(event) => setOrderNumberInput(event.target.value)}
                placeholder="ORD-..."
              />
            </div>
          </div>
          <div className="rounded-lg border border-border/70 bg-secondary p-4 text-sm leading-6 text-secondary-foreground col-span-2 lg:col-span-3">
            💡 Newly created attempts start in <strong>Reserved</strong>. From
            there the operator path is collect, pack, ship, or fail. Customer
            order details may lag briefly while the main API projection updates.
          </div>
          {backlogQuery.isError ? (
            <EmptyState
              className="col-span-2 lg:col-span-3"
              title="Backlog failed to load"
              description={getErrorMessage(backlogQuery.error)}
            />
          ) : backlogRows.length === 0 ? (
            <EmptyState
              className="col-span-2 lg:col-span-3"
              title="No backlog rows match the current filter"
              description="Switch status focus or remove the exact order number filter to widen the result set."
            />
          ) : (
            <FulfillmentBacklogTable
              className="col-span-2 lg:col-span-3"
              rows={backlogRows}
              onOpenAction={setFulfillmentDialog}
            />
          )}
        </CardContent>
      </Card>

      <FulfillmentActionDialog
        isPending={advanceMutation.isPending}
        state={fulfillmentDialog}
        onClose={() => setFulfillmentDialog(null)}
        onSubmit={(values) => {
          if (!fulfillmentDialog) {
            return;
          }

          advanceMutation.mutate({
            orderId: fulfillmentDialog.attempt.orderId,
            action: fulfillmentDialog.action,
            payload: {
              message: values.message || undefined,
              trackingReference: values.trackingReference || undefined,
            },
          });
        }}
      />
    </div>
  );
}

function FulfillmentBacklogTable({
  className,
  rows,
  onOpenAction,
}: {
  className?: string;
  rows: FulfillmentAttempt[];
  onOpenAction: Dispatch<SetStateAction<FulfillmentDialogState>>;
}) {
  return (
    <div className={cn("overflow-x-auto", className)}>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Order</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Service path</TableHead>
            <TableHead>Items</TableHead>
            <TableHead>Last updated</TableHead>
            <TableHead>Actions</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((row) => (
            <TableRow key={row.orderId}>
              <TableCell>
                <div className="space-y-1">
                  <p className="font-mono text-sm text-foreground">
                    {row.orderNumber}
                  </p>
                  <p className="text-xs text-muted-foreground">{row.userId}</p>
                </div>
              </TableCell>
              <TableCell>
                <StatusBadge value={row.status} />
              </TableCell>
              <TableCell>
                <div className="space-y-1">
                  <p className="text-sm text-foreground">
                    {row.deliveryCourier} · {row.deliveryOptionName}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {row.warehouse}
                  </p>
                </div>
              </TableCell>
              <TableCell>
                <div className="space-y-1">
                  {row.items.map((item) => (
                    <p
                      key={item.itemId}
                      className="text-xs text-muted-foreground">
                      {item.quantity} × {item.itemName}
                    </p>
                  ))}
                </div>
              </TableCell>
              <TableCell>
                <span className="text-xs text-muted-foreground">
                  {formatDateTime(row.lastProcessedAtUtc)}
                </span>
              </TableCell>
              <TableCell>
                <div className="flex flex-wrap gap-2">
                  {renderFulfillmentButtons(row, onOpenAction)}
                </div>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

function renderFulfillmentButtons(
  attempt: FulfillmentAttempt,
  setDialog: Dispatch<SetStateAction<FulfillmentDialogState>>,
) {
  const buttons: ReactNode[] = [];

  if (attempt.status === "Reserved") {
    buttons.push(
      <Button
        key="collect"
        size="sm"
        variant="secondary"
        className="w-20 justify-center"
        onClick={() => setDialog({ action: "collect", attempt })}>
        Collect
      </Button>,
    );
  }

  if (attempt.status === "Collected") {
    buttons.push(
      <Button
        key="pack"
        size="sm"
        variant="secondary"
        className="w-20 justify-center"
        onClick={() => setDialog({ action: "pack", attempt })}>
        Pack
      </Button>,
    );
  }

  if (attempt.status === "Packed") {
    buttons.push(
      <Button
        key="ship"
        size="sm"
        className="w-20 justify-center"
        onClick={() => setDialog({ action: "ship", attempt })}>
        Ship
      </Button>,
    );
  }

  if (attempt.status !== "Failed" && attempt.status !== "Shipped") {
    buttons.push(
      <Button
        key="fail"
        size="sm"
        variant="destructive"
        className="w-20 justify-center"
        onClick={() => setDialog({ action: "fail", attempt })}>
        Fail
      </Button>,
    );
  }

  if (buttons.length === 0) {
    return (
      <span className="text-xs text-muted-foreground">No actions left</span>
    );
  }

  return buttons;
}
