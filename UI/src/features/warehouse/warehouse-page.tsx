import type { Dispatch, ReactNode, SetStateAction } from "react";
import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { Search } from "lucide-react";
import { toast } from "sonner";
import { z } from "zod";

import { EmptyState } from "@/components/shared/empty-state";
import { StatusBadge } from "@/components/shared/status-badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
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
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import {
  getFulfillmentAttempts,
  getWarehouseInventory,
  recountInventory,
  restockInventory,
  transitionFulfillment,
  writeOffInventory,
} from "@/lib/api/client";
import { getErrorMessage } from "@/lib/api/http";
import type { FulfillmentAttempt, WarehouseInventory } from "@/lib/api/types";
import { formatDateTime, humanizeLabel } from "@/lib/format";

const fulfillmentStatusOptions = [
  "All",
  "Reserved",
  "Collected",
  "Packed",
  "Shipped",
  "Failed",
] as const;

type FulfillmentDialogState = {
  action: "collect" | "pack" | "ship" | "fail";
  attempt: FulfillmentAttempt;
} | null;

type InventoryDialogState = {
  action: "restock" | "writeOff" | "recount";
  item: WarehouseInventory;
} | null;

const inventoryAdjustmentSchema = z.object({
  quantity: z.number().int().min(1, "Enter a value greater than zero."),
  reason: z.string().trim().max(200, "Keep reasons under 200 characters."),
});

type FulfillmentActionValues = {
  trackingReference: string;
  message: string;
};

type InventoryAdjustmentValues = z.infer<typeof inventoryAdjustmentSchema>;

export function WarehousePage() {
  const queryClient = useQueryClient();
  const [tab, setTab] = useState("backlog");
  const [statusFilter, setStatusFilter] =
    useState<(typeof fulfillmentStatusOptions)[number]>("All");
  const [orderNumberInput, setOrderNumberInput] = useState("");
  const [inventorySearchInput, setInventorySearchInput] = useState("");
  const [inventoryCategory, setInventoryCategory] = useState("All");
  const [fulfillmentDialog, setFulfillmentDialog] =
    useState<FulfillmentDialogState>(null);
  const [inventoryDialog, setInventoryDialog] =
    useState<InventoryDialogState>(null);

  const debouncedOrderNumber = useDebouncedValue(orderNumberInput.trim());
  const debouncedInventorySearch = useDebouncedValue(
    inventorySearchInput.trim().toLowerCase(),
  );

  const backlogQuery = useQuery({
    queryKey: ["fulfillment", statusFilter, debouncedOrderNumber],
    queryFn: () =>
      getFulfillmentAttempts({
        status: statusFilter,
        orderNumber: debouncedOrderNumber || undefined,
      }),
    placeholderData: (previousData) => previousData,
  });

  const inventoryQuery = useQuery({
    queryKey: ["warehouse-inventory"],
    queryFn: getWarehouseInventory,
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

  const inventoryMutation = useMutation({
    mutationFn: ({
      action,
      itemId,
      quantity,
      reason,
    }: {
      action: "restock" | "writeOff" | "recount";
      itemId: string;
      quantity: number;
      reason?: string;
    }) => {
      if (action === "restock") {
        return restockInventory(itemId, quantity, reason);
      }

      if (action === "writeOff") {
        return writeOffInventory(itemId, quantity, reason);
      }

      return recountInventory(itemId, quantity, reason);
    },
    onSuccess: async (_, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["warehouse-inventory"] }),
        queryClient.invalidateQueries({ queryKey: ["items"] }),
      ]);
      toast.success(`${humanizeLabel(variables.action)} applied.`);
      setInventoryDialog(null);
    },
    onError: (error) => {
      toast.error(getErrorMessage(error));
    },
  });

  const backlogRows = useMemo(
    () => backlogQuery.data ?? [],
    [backlogQuery.data],
  );
  const inventoryItems = useMemo(
    () => inventoryQuery.data ?? [],
    [inventoryQuery.data],
  );
  const inventoryCategories = useMemo(
    () => [
      "All",
      ...Array.from(
        new Set(inventoryItems.map((item) => item.category)),
      ).sort(),
    ],
    [inventoryItems],
  );
  const filteredInventoryItems = useMemo(
    () =>
      inventoryItems.filter((item) => {
        const matchesCategory =
          inventoryCategory === "All" || item.category === inventoryCategory;
        const haystack = `${item.itemName} ${item.category}`.toLowerCase();
        const matchesSearch =
          debouncedInventorySearch.length === 0 ||
          haystack.includes(debouncedInventorySearch);
        return matchesCategory && matchesSearch;
      }),
    [debouncedInventorySearch, inventoryCategory, inventoryItems],
  );

  return (
    <div className="section-grid">
      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="backlog">Backlog</TabsTrigger>
          <TabsTrigger value="inventory">Inventory</TabsTrigger>
        </TabsList>

        <TabsContent value="backlog" className="space-y-4">
          <Card>
            <CardContent className="grid gap-4 pt-5 lg:grid-cols-[16rem_18rem_minmax(0,1fr)]">
              <div className="space-y-2">
                <Label>Status focus</Label>
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
                <Label>Exact order number</Label>
                <div className="relative">
                  <Search className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    className="pl-11"
                    value={orderNumberInput}
                    onChange={(event) =>
                      setOrderNumberInput(event.target.value)
                    }
                    placeholder="ORD-..."
                  />
                </div>
              </div>
              <div className="rounded-lg border border-border/70 bg-secondary p-4 text-sm leading-6 text-secondary-foreground lg:col-span-3">
                💡 Newly created attempts start in <strong>Reserved</strong>.
                From there the operator path is collect, pack, ship, or fail.
                Customer order details may lag briefly while the main API
                projection updates.
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Fulfillment backlog</CardTitle>
              <CardDescription>
                Use the action buttons to move the selected order forward
                through the real operator workflow.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {backlogQuery.isError ? (
                <EmptyState
                  title="Backlog failed to load"
                  description={getErrorMessage(backlogQuery.error)}
                />
              ) : backlogRows.length === 0 ? (
                <EmptyState
                  title="No backlog rows match the current filter"
                  description="Switch status focus or remove the exact order number filter to widen the result set."
                />
              ) : (
                <FulfillmentBacklogTable
                  rows={backlogRows}
                  onOpenAction={setFulfillmentDialog}
                />
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="inventory" className="space-y-4">
          <Card>
            <CardContent className="grid gap-4 pt-5 lg:grid-cols-[1fr_18rem]">
              <div className="space-y-2">
                <Label>Find inventory row</Label>
                <div className="relative">
                  <Search className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    className="pl-11"
                    value={inventorySearchInput}
                    onChange={(event) =>
                      setInventorySearchInput(event.target.value)
                    }
                    placeholder="Monitor, furniture, accessories..."
                  />
                </div>
              </div>
              <div className="space-y-2">
                <Label>Category</Label>
                <Select
                  value={inventoryCategory}
                  onValueChange={setInventoryCategory}>
                  <SelectTrigger>
                    <SelectValue placeholder="All categories" />
                  </SelectTrigger>
                  <SelectContent>
                    {inventoryCategories.map((category) => (
                      <SelectItem key={category} value={category}>
                        {category}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Warehouse inventory</CardTitle>
              <CardDescription>
                Restock adds stock, recount resets on-hand quantity, and
                write-off removes damaged or missing units.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {inventoryQuery.isError ? (
                <EmptyState
                  title="Inventory failed to load"
                  description={getErrorMessage(inventoryQuery.error)}
                />
              ) : filteredInventoryItems.length === 0 ? (
                <EmptyState
                  title="No inventory rows match the current filter"
                  description="Clear the search box or switch category to broaden the current slice."
                />
              ) : (
                <InventoryDataTable
                  rows={filteredInventoryItems}
                  onOpenAction={setInventoryDialog}
                />
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

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

      <InventoryActionDialog
        isPending={inventoryMutation.isPending}
        state={inventoryDialog}
        onClose={() => setInventoryDialog(null)}
        onSubmit={(values) => {
          if (!inventoryDialog) {
            return;
          }

          inventoryMutation.mutate({
            action: inventoryDialog.action,
            itemId: inventoryDialog.item.itemId,
            quantity: values.quantity,
            reason: values.reason || undefined,
          });
        }}
      />
    </div>
  );
}

function FulfillmentBacklogTable({
  rows,
  onOpenAction,
}: {
  rows: FulfillmentAttempt[];
  onOpenAction: Dispatch<SetStateAction<FulfillmentDialogState>>;
}) {
  return (
    <div className="overflow-x-auto">
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

function InventoryDataTable({
  rows,
  onOpenAction,
}: {
  rows: WarehouseInventory[];
  onOpenAction: Dispatch<SetStateAction<InventoryDialogState>>;
}) {
  return (
    <div className="overflow-x-auto">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Item</TableHead>
            <TableHead>Available</TableHead>
            <TableHead>Reserved</TableHead>
            <TableHead>On hand</TableHead>
            <TableHead>Updated</TableHead>
            <TableHead>Adjust</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((row) => (
            <TableRow key={row.itemId}>
              <TableCell>
                <div className="space-y-1">
                  <p className="text-sm font-semibold text-foreground">
                    {row.itemName}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {row.category}
                  </p>
                </div>
              </TableCell>
              <TableCell>
                <span className="font-mono text-sm text-foreground">
                  {row.availableQuantity}
                </span>
              </TableCell>
              <TableCell>
                <span className="font-mono text-sm text-foreground">
                  {row.quantityReserved}
                </span>
              </TableCell>
              <TableCell>
                <span className="font-mono text-sm text-foreground">
                  {row.quantityOnHand}
                </span>
              </TableCell>
              <TableCell>
                <span className="text-xs text-muted-foreground">
                  {formatDateTime(row.updatedAtUtc)}
                </span>
              </TableCell>
              <TableCell>
                <div className="flex flex-wrap gap-2">
                  <Button
                    size="sm"
                    variant="secondary"
                    onClick={() =>
                      onOpenAction({ action: "restock", item: row })
                    }>
                    Restock
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() =>
                      onOpenAction({ action: "recount", item: row })
                    }>
                    Recount
                  </Button>
                  <Button
                    size="sm"
                    variant="destructive"
                    onClick={() =>
                      onOpenAction({ action: "writeOff", item: row })
                    }>
                    Write-off
                  </Button>
                </div>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

function useDebouncedValue(value: string, delayMilliseconds = 220) {
  const [debouncedValue, setDebouncedValue] = useState(value);

  useEffect(() => {
    const timeoutId = window.setTimeout(
      () => setDebouncedValue(value),
      delayMilliseconds,
    );
    return () => window.clearTimeout(timeoutId);
  }, [delayMilliseconds, value]);

  return debouncedValue;
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

function FulfillmentActionDialog({
  state,
  isPending,
  onClose,
  onSubmit,
}: {
  state: FulfillmentDialogState;
  isPending: boolean;
  onClose: () => void;
  onSubmit: (values: FulfillmentActionValues) => void;
}) {
  const fulfillmentActionSchema = z
    .object({
      trackingReference: z
        .string()
        .trim()
        .max(80, "Keep tracking references under 80 characters."),
      message: z
        .string()
        .trim()
        .max(200, "Keep operator notes under 200 characters."),
    })
    .superRefine((values, context) => {
      if (state?.action === "ship" && values.trackingReference.length === 0) {
        context.addIssue({
          code: "custom",
          path: ["trackingReference"],
          message: "Tracking reference is required when shipping.",
        });
      }
    });

  const form = useForm<FulfillmentActionValues>({
    resolver: zodResolver(fulfillmentActionSchema),
    defaultValues: {
      trackingReference: "",
      message: "",
    },
  });

  useEffect(() => {
    if (!state) {
      return;
    }

    form.reset({
      trackingReference:
        state.action === "ship" ? `SHIP-${state.attempt.orderNumber}` : "",
      message: "",
    });
  }, [form, state]);

  return (
    <Dialog open={Boolean(state)} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {state
              ? `${humanizeLabel(state.action)} ${state.attempt.orderNumber}`
              : "Warehouse action"}
          </DialogTitle>
          <DialogDescription>
            {state
              ? "Submit an operator note and, when shipping, a tracking reference. The transition still goes through the real fulfillment API."
              : "No action selected."}
          </DialogDescription>
        </DialogHeader>

        {state ? (
          <form className="space-y-4" onSubmit={form.handleSubmit(onSubmit)}>
            {state.action === "ship" ? (
              <div className="space-y-2">
                <Label htmlFor="trackingReference">Tracking reference</Label>
                <Input
                  id="trackingReference"
                  {...form.register("trackingReference")}
                />
                {form.formState.errors.trackingReference ? (
                  <p className="text-sm text-destructive">
                    {form.formState.errors.trackingReference.message}
                  </p>
                ) : null}
              </div>
            ) : null}

            <div className="space-y-2">
              <Label htmlFor="message">Operator note</Label>
              <Textarea
                id="message"
                placeholder="Optional note for the event stream"
                {...form.register("message")}
              />
              {form.formState.errors.message ? (
                <p className="text-sm text-destructive">
                  {form.formState.errors.message.message}
                </p>
              ) : null}
            </div>

            <DialogFooter>
              <Button type="button" variant="ghost" onClick={onClose}>
                Cancel
              </Button>
              <Button type="submit" disabled={isPending}>
                {isPending
                  ? "Submitting..."
                  : `Confirm ${humanizeLabel(state.action)}`}
              </Button>
            </DialogFooter>
          </form>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}

function InventoryActionDialog({
  state,
  isPending,
  onClose,
  onSubmit,
}: {
  state: InventoryDialogState;
  isPending: boolean;
  onClose: () => void;
  onSubmit: (values: InventoryAdjustmentValues) => void;
}) {
  const form = useForm<InventoryAdjustmentValues>({
    resolver: zodResolver(inventoryAdjustmentSchema),
    defaultValues: {
      quantity: 1,
      reason: "",
    },
  });

  useEffect(() => {
    if (!state) {
      return;
    }

    form.reset({
      quantity: state.action === "recount" ? state.item.quantityOnHand : 1,
      reason: "",
    });
  }, [form, state]);

  return (
    <Dialog open={Boolean(state)} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {state
              ? `${humanizeLabel(state.action)} ${state.item.itemName}`
              : "Inventory action"}
          </DialogTitle>
          <DialogDescription>
            {state
              ? "This command uses the existing warehouse endpoints on the main API."
              : "No inventory row selected."}
          </DialogDescription>
        </DialogHeader>

        {state ? (
          <form className="space-y-4" onSubmit={form.handleSubmit(onSubmit)}>
            <div className="space-y-2">
              <Label htmlFor="quantity">
                {state.action === "recount"
                  ? "New on-hand quantity"
                  : "Quantity"}
              </Label>
              <Input
                id="quantity"
                type="number"
                min={1}
                {...form.register("quantity", { valueAsNumber: true })}
              />
              {form.formState.errors.quantity ? (
                <p className="text-sm text-destructive">
                  {form.formState.errors.quantity.message}
                </p>
              ) : null}
            </div>

            <div className="space-y-2">
              <Label htmlFor="reason">Reason</Label>
              <Textarea
                id="reason"
                placeholder="Optional note for warehouse history"
                {...form.register("reason")}
              />
              {form.formState.errors.reason ? (
                <p className="text-sm text-destructive">
                  {form.formState.errors.reason.message}
                </p>
              ) : null}
            </div>

            <DialogFooter>
              <Button type="button" variant="ghost" onClick={onClose}>
                Cancel
              </Button>
              <Button
                type="submit"
                disabled={isPending}
                variant={
                  state.action === "writeOff" ? "destructive" : "default"
                }>
                {isPending
                  ? "Submitting..."
                  : `Confirm ${humanizeLabel(state.action)}`}
              </Button>
            </DialogFooter>
          </form>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}
