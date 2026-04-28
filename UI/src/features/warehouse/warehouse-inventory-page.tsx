import type { Dispatch, SetStateAction } from "react";
import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Search } from "lucide-react";
import { toast } from "sonner";

import { EmptyState } from "@/components/shared/empty-state";
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
  getWarehouseInventory,
  recountInventory,
  restockInventory,
  writeOffInventory,
} from "@/lib/api/client";
import { getErrorMessage } from "@/lib/api/http";
import type { WarehouseInventory } from "@/lib/api/types";
import { formatDateTime, humanizeLabel } from "@/lib/format";

import {
  InventoryActionDialog,
  type InventoryDialogState,
  useDebouncedValue,
} from "./warehouse-shared";
import { cn } from "@/lib/utils";

export function WarehouseInventoryPage() {
  const queryClient = useQueryClient();
  const [inventorySearchInput, setInventorySearchInput] = useState("");
  const [inventoryCategory, setInventoryCategory] = useState("All");
  const [inventoryDialog, setInventoryDialog] =
    useState<InventoryDialogState>(null);

  const debouncedInventorySearch = useDebouncedValue(
    inventorySearchInput.trim().toLowerCase(),
  );

  const inventoryQuery = useQuery({
    queryKey: ["warehouse-inventory"],
    queryFn: getWarehouseInventory,
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
      <div className="space-y-2">
        <div className="space-y-1">
          <h1 className="text-3xl font-semibold tracking-tight text-foreground">
            Warehouse Inventory
          </h1>
          <p className="text-sm leading-6 text-muted-foreground">
            Review on-hand stock, restock damaged rows, and reconcile counts
            using the same warehouse endpoints as the backend demo.
          </p>
        </div>
      </div>

      <Card>
        <CardContent className="grid gap-4 lg:grid-cols-[1fr_18rem]">
          <div className="space-y-2">
            <Label className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              Product name
            </Label>
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
            <Label className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              Category
            </Label>
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
          {inventoryQuery.isError ? (
            <EmptyState
              className="col-span-2 lg:col-span-3"
              title="Inventory failed to load"
              description={getErrorMessage(inventoryQuery.error)}
            />
          ) : filteredInventoryItems.length === 0 ? (
            <EmptyState
              className="col-span-2 lg:col-span-3"
              title="No inventory rows match the current filter"
              description="Clear the search box or switch category to broaden the current slice."
            />
          ) : (
            <InventoryDataTable
              className="col-span-2 lg:col-span-3"
              rows={filteredInventoryItems}
              onOpenAction={setInventoryDialog}
            />
          )}
        </CardContent>
      </Card>

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

function InventoryDataTable({
  className,
  rows,
  onOpenAction,
}: {
  className?: string;
  rows: WarehouseInventory[];
  onOpenAction: Dispatch<SetStateAction<InventoryDialogState>>;
}) {
  return (
    <div className={cn("overflow-x-auto", className)}>
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
