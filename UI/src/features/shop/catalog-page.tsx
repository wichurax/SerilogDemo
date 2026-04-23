import { useDeferredValue, useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ChevronDown,
  Info,
  Minus,
  Plus,
  Search,
  ShoppingBag,
  SlidersHorizontal,
} from "lucide-react";
import { toast } from "sonner";

import { EmptyState } from "@/components/shared/empty-state";
import { LoadingBlock } from "@/components/shared/loading-block";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import { Switch } from "@/components/ui/switch";
import { addToBasket, getCategories, getItems } from "@/lib/api/client";
import { getErrorMessage } from "@/lib/api/http";
import type { Item } from "@/lib/api/types";
import { formatCurrency, pluralize } from "@/lib/format";
import { cn } from "@/lib/utils";
import { getCategoryChipClassName } from "@/features/shop/category-chip";
import { useDemoUserStore } from "@/stores/demo-user-store";

const sortOptions = [
  { value: "name-asc", label: "Product name (A-Z)" },
  { value: "name-desc", label: "Product name (Z-A)" },
  { value: "category-asc", label: "Category (A-Z)" },
  { value: "category-desc", label: "Category (Z-A)" },
] as const;

type SortOption = (typeof sortOptions)[number]["value"];

type AddToBasketMutationState = {
  item: Item;
  quantity: number;
};

export function CatalogPage() {
  const soldOutSettingDescriptionId =
    "catalog-show-sold-out-section-description";
  const userId = useDemoUserStore((state) => state.userId);
  const queryClient = useQueryClient();
  const [searchInput, setSearchInput] = useState("");
  const [selectedCategory, setSelectedCategory] = useState("All");
  const [selectedSort, setSelectedSort] = useState<SortOption>("name-asc");
  const [showSoldOutSection, setShowSoldOutSection] = useState(false);
  const [isSoldOutSectionExpanded, setIsSoldOutSectionExpanded] =
    useState(true);
  const [isSettingsPanelOpen, setIsSettingsPanelOpen] = useState(false);
  const deferredSearch = useDeferredValue(searchInput.trim());
  const settingsPanelRef = useRef<HTMLDivElement | null>(null);

  const categoriesQuery = useQuery({
    queryKey: ["categories"],
    queryFn: getCategories,
  });

  const itemsQuery = useQuery({
    queryKey: ["items", selectedCategory, deferredSearch],
    queryFn: () =>
      getItems({
        category: selectedCategory === "All" ? undefined : selectedCategory,
        search: deferredSearch || undefined,
      }),
  });

  const addToBasketMutation = useMutation({
    mutationFn: ({ item, quantity }: AddToBasketMutationState) =>
      addToBasket(userId, { itemId: item.id, quantity }),
    onSuccess: async (_, variables) => {
      await queryClient.invalidateQueries({ queryKey: ["basket", userId] });
      toast.success(
        `${variables.quantity} × ${variables.item.name} added to the basket.`,
      );
    },
    onError: (error) => {
      toast.error(getErrorMessage(error));
    },
  });

  const categories = ["All", ...(categoriesQuery.data ?? [])];
  const items = useMemo(() => itemsQuery.data ?? [], [itemsQuery.data]);
  const isGroupedByStock = showSoldOutSection;

  useEffect(() => {
    if (!isSettingsPanelOpen) {
      return;
    }

    function handlePointerDown(event: PointerEvent) {
      if (!settingsPanelRef.current?.contains(event.target as Node)) {
        setIsSettingsPanelOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setIsSettingsPanelOpen(false);
      }
    }

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isSettingsPanelOpen]);

  const compareItems = useMemo(() => {
    return (left: Item, right: Item) => {
      switch (selectedSort) {
        case "name-asc":
          return compareByName(left, right);
        case "name-desc":
          return compareByName(right, left);
        case "category-asc":
          return compareByCategory(left, right);
        case "category-desc":
          return compareByCategory(right, left);
      }
    };
  }, [selectedSort]);

  const sortedItems = useMemo(
    () => [...items].sort(compareItems),
    [compareItems, items],
  );
  const availableItems = useMemo(
    () => sortedItems.filter((item) => item.availableQuantity > 0),
    [sortedItems],
  );
  const soldOutItems = useMemo(
    () => sortedItems.filter((item) => item.availableQuantity <= 0),
    [sortedItems],
  );

  return (
    <div className="section-grid">
      <Card className="overflow-visible">
        <CardContent className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_16rem_22rem]">
          <div className="space-y-2">
            <label className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              Search the catalog
            </label>
            <div className="relative">
              <Search className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={searchInput}
                onChange={(event) => setSearchInput(event.target.value)}
                placeholder="Router, planner, speakerphone..."
                className="pl-11"
              />
            </div>
          </div>
          <div className="space-y-2">
            <label className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              Category
            </label>
            <Select
              value={selectedCategory}
              onValueChange={setSelectedCategory}>
              <SelectTrigger>
                <SelectValue placeholder="All categories" />
              </SelectTrigger>
              <SelectContent>
                {categories.map((category) => (
                  <SelectItem key={category} value={category}>
                    {category}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <label className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              Sort by
            </label>
            <div className="flex gap-2">
              <Select
                value={selectedSort}
                onValueChange={(value) => setSelectedSort(value as SortOption)}>
                <SelectTrigger>
                  <SelectValue placeholder="Product name (A-Z)" />
                </SelectTrigger>
                <SelectContent>
                  {sortOptions.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>

              <div ref={settingsPanelRef} className="relative shrink-0">
                <Button
                  type="button"
                  variant="outline"
                  size="icon"
                  aria-expanded={isSettingsPanelOpen}
                  aria-haspopup="dialog"
                  aria-label="Open catalog settings"
                  onClick={() =>
                    setIsSettingsPanelOpen((currentValue) => !currentValue)
                  }>
                  <SlidersHorizontal className="size-4" />
                </Button>

                {isSettingsPanelOpen ? (
                  <div className="absolute right-0 top-full z-60 mt-2 w-[min(22rem,calc(100vw-2rem))] rounded-xl border border-border bg-popover p-2 text-popover-foreground shadow-md">
                    <div className="flex items-center justify-between gap-3 rounded-lg p-3 transition">
                      <Switch
                        checked={showSoldOutSection}
                        aria-labelledby={soldOutSettingDescriptionId}
                        onCheckedChange={(nextValue) => {
                          setShowSoldOutSection(nextValue);
                          if (nextValue) {
                            setIsSoldOutSectionExpanded(true);
                          }
                        }}
                      />
                      <div className="min-w-0 flex-1">
                        <span
                          id={soldOutSettingDescriptionId}
                          className="flex items-center gap-2 text-sm font-medium text-foreground">
                          <span>Show sold out section</span>
                          <InlineInfoTooltip text="Available products stay in the main results. Sold-out items move into a separate section below and keep the same sort order." />
                        </span>
                      </div>
                    </div>
                  </div>
                ) : null}
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {itemsQuery.isLoading ? (
        <div className="grid gap-4 md:grid-cols-2 2xl:grid-cols-3">
          {Array.from({ length: 6 }).map((_, index) => (
            <LoadingBlock key={index} className="h-88" />
          ))}
        </div>
      ) : itemsQuery.isError ? (
        <EmptyState
          title="Catalog query failed"
          description={getErrorMessage(itemsQuery.error)}
        />
      ) : items.length === 0 ? (
        <EmptyState
          title="No items match this search"
          description="Try a broader keyword, remove the category filter, or swap demo scenarios later in checkout."
        />
      ) : (
        <div className="space-y-6">
          <CatalogItemsGrid
            items={isGroupedByStock ? availableItems : sortedItems}
            addToBasketMutation={addToBasketMutation}
          />

          {isGroupedByStock ? (
            soldOutItems.length > 0 ? (
              <div className="space-y-4">
                <div className="flex items-center gap-4">
                  <Separator className="flex-1" />
                  <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                    <span>Sold out items</span>
                    <span className="rounded-full border border-border bg-background px-2 py-1 text-[10px] text-foreground">
                      {pluralize("item", soldOutItems.length)}
                    </span>
                  </div>
                  <Separator className="flex-1" />
                </div>

                <div className="overflow-hidden rounded-2xl border border-border/70 bg-card/60">
                  <button
                    type="button"
                    className="flex w-full items-center justify-between gap-4 px-4 py-4 text-left transition hover:bg-secondary/35"
                    aria-expanded={isSoldOutSectionExpanded}
                    onClick={() =>
                      setIsSoldOutSectionExpanded(
                        (currentValue) => !currentValue,
                      )
                    }>
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <p className="text-sm font-semibold text-foreground">
                          Sold out items
                        </p>
                        <span className="rounded-full border border-border bg-background px-2 py-1 text-[10px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                          {pluralize("item", soldOutItems.length)}
                        </span>
                      </div>
                    </div>
                    <ChevronDown
                      className={cn(
                        "size-5 shrink-0 text-muted-foreground transition-transform",
                        isSoldOutSectionExpanded && "rotate-180",
                      )}
                    />
                  </button>

                  {isSoldOutSectionExpanded ? (
                    <div className="overflow-hidden">
                      <div className="border-t border-border/70 p-4">
                        <CatalogItemsGrid
                          items={soldOutItems}
                          addToBasketMutation={addToBasketMutation}
                        />
                      </div>
                    </div>
                  ) : null}
                </div>
              </div>
            ) : null
          ) : null}
        </div>
      )}
    </div>
  );
}

function InlineInfoTooltip({ text }: { text: string }) {
  return (
    <span className="group/tooltip relative inline-flex">
      <button
        type="button"
        className="inline-flex size-5 items-center justify-center rounded-full text-muted-foreground transition hover:bg-secondary hover:text-foreground focus-visible:bg-secondary focus-visible:text-foreground"
        aria-label="More details about the sold out section setting">
        <Info className="size-3.5" />
      </button>
      <span
        role="tooltip"
        className="pointer-events-none absolute left-1/2 top-full z-60 mt-2 w-60 -translate-x-1/2 rounded-lg border border-border bg-popover px-3 py-2 text-left text-xs leading-5 text-popover-foreground opacity-0 shadow-md transition group-hover/tooltip:opacity-100 group-focus-within/tooltip:opacity-100">
        {text}
      </span>
    </span>
  );
}

function CatalogItemsGrid({
  items,
  addToBasketMutation,
}: {
  items: Item[];
  addToBasketMutation: ReturnType<
    typeof useMutation<unknown, unknown, AddToBasketMutationState, unknown>
  >;
}) {
  if (items.length === 0) {
    return (
      <Card className="border-dashed">
        <CardContent className="py-8 text-center text-sm text-muted-foreground">
          No in-stock items match the current filters.
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="grid gap-4 md:grid-cols-2 2xl:grid-cols-3">
      {items.map((item) => {
        const isAdding =
          addToBasketMutation.isPending &&
          addToBasketMutation.variables?.item.id === item.id;

        return (
          <div key={item.id} className="h-full">
            <CatalogItemCard
              item={item}
              isAdding={isAdding}
              onAdd={(quantity) =>
                addToBasketMutation.mutate({ item, quantity })
              }
            />
          </div>
        );
      })}
    </div>
  );
}

function CatalogItemCard({
  item,
  isAdding,
  onAdd,
}: {
  item: Item;
  isAdding: boolean;
  onAdd: (quantity: number) => void;
}) {
  return (
    <Card className="flex h-full flex-col justify-between">
      <CardHeader className="space-y-2">
        <div className="flex items-start justify-between gap-3">
          <CardTitle className="pr-2 text-lg leading-tight">
            {item.name}
          </CardTitle>
          <Badge
            className={`shrink-0 ${getCategoryChipClassName(item.category)}`}>
            {item.category}
          </Badge>
        </div>
        <CardDescription>{item.description}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <AddToBasketControl
          isPending={isAdding}
          price={item.price}
          availableQuantity={item.availableQuantity}
          onAdd={onAdd}
        />
      </CardContent>
    </Card>
  );
}

function compareByName(left: Item, right: Item) {
  const byName = left.name.localeCompare(right.name);
  if (byName !== 0) {
    return byName;
  }

  return left.category.localeCompare(right.category);
}

function compareByCategory(left: Item, right: Item) {
  const byCategory = left.category.localeCompare(right.category);
  if (byCategory !== 0) {
    return byCategory;
  }

  return left.name.localeCompare(right.name);
}

function AddToBasketControl({
  price,
  availableQuantity,
  isPending,
  onAdd,
}: {
  price: number;
  availableQuantity: number;
  isPending: boolean;
  onAdd: (quantity: number) => void;
}) {
  const normalizedMaxQuantity = Math.max(availableQuantity, 1);
  const [quantity, setQuantity] = useState(1);
  const isOutOfStock = availableQuantity <= 0;
  const clampedQuantity = Math.min(
    Math.max(quantity, 1),
    normalizedMaxQuantity,
  );

  return (
    <div className="space-y-3">
      <div className="flex items-end justify-between gap-4">
        <div className="min-w-0">
          <p className="text-xs font-semibold uppercase tracking-[0.2em] text-muted-foreground">
            Unit price
          </p>
          <p className="font-mono text-2xl text-foreground">
            {formatCurrency(price)}
          </p>
          <p className="mt-1 text-sm text-muted-foreground">
            {isOutOfStock
              ? "Out of stock"
              : `${availableQuantity} available to buy`}
          </p>
        </div>

        <div className="w-35 shrink-0 self-center">
          <div className="inline-flex w-full items-center gap-2 rounded-lg border border-border bg-background p-1">
            <button
              type="button"
              className="inline-flex size-9 items-center justify-center rounded-md text-secondary-foreground transition hover:bg-secondary disabled:cursor-not-allowed disabled:opacity-45"
              disabled={isPending || clampedQuantity <= 1}
              onClick={() => setQuantity(Math.max(1, clampedQuantity - 1))}>
              <Minus className="size-4" />
            </button>
            <span className="min-w-0 flex-1 text-center font-mono text-sm text-foreground">
              {clampedQuantity}
            </span>
            <button
              type="button"
              className="inline-flex size-9 items-center justify-center rounded-md text-secondary-foreground transition hover:bg-secondary disabled:cursor-not-allowed disabled:opacity-45"
              disabled={
                isPending ||
                clampedQuantity >= normalizedMaxQuantity ||
                isOutOfStock
              }
              onClick={() =>
                setQuantity(
                  Math.min(normalizedMaxQuantity, clampedQuantity + 1),
                )
              }>
              <Plus className="size-4" />
            </button>
          </div>
        </div>
      </div>

      <Button
        className="w-full"
        disabled={isOutOfStock || isPending}
        onClick={() => onAdd(clampedQuantity)}>
        <ShoppingBag className="size-4" />
        {isPending ? "Adding..." : `Add ${clampedQuantity} to basket`}
      </Button>
    </div>
  );
}
