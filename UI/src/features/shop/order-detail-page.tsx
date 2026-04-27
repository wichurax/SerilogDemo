import type { ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Package } from "lucide-react";
import { Link, useParams } from "react-router-dom";

import { PageHeader } from "@/components/layout/page-header";
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
import { resolveVisibleOrderStatus } from "@/features/shop/order-status";
import { Separator } from "@/components/ui/separator";
import { getOrder } from "@/lib/api/client";
import { getErrorMessage } from "@/lib/api/http";
import { formatCurrency, formatDateTime, humanizeLabel } from "@/lib/format";
import { useDemoUserStore } from "@/stores/demo-user-store";

const fulfillmentStages = ["Collected", "Packed", "Shipped"];

type TimelineEntry = {
  key: string;
  label: string;
  meta: string;
  dotClassName: string;
};

export function OrderDetailPage() {
  const { orderId } = useParams();
  const userId = useDemoUserStore((state) => state.userId);

  const orderQuery = useQuery({
    queryKey: ["order", userId, orderId],
    queryFn: () => getOrder(userId, orderId!),
    enabled: Boolean(orderId),
  });

  if (!orderId) {
    return (
      <EmptyState
        title="Missing order ID"
        description="The route is missing the order identifier needed to fetch details."
      />
    );
  }

  if (orderQuery.isLoading) {
    return (
      <EmptyState
        title="Loading order"
        description="Pulling line items, payment state, and fulfillment projection from the API."
      />
    );
  }

  if (orderQuery.isError) {
    return (
      <EmptyState
        title="Order detail failed"
        description={getErrorMessage(orderQuery.error)}
      />
    );
  }

  const order = orderQuery.data;
  if (!order) {
    return (
      <EmptyState
        title="Order not found"
        description="The API returned no order for this route and user combination."
      />
    );
  }

  const activeStageIndex = fulfillmentStages.findIndex(
    (stage) => stage === order.fulfillment.status,
  );
  const visibleOrderStatus = resolveVisibleOrderStatus(
    order.status,
    order.fulfillment.status,
  );
  const paymentTimelineEntry = getPaymentTimelineEntry(
    order.paymentStatus,
    order.paymentStatusUpdatedAtUtc
  );
  const timelineEntries: TimelineEntry[] = [
    paymentTimelineEntry,
    ...fulfillmentStages.map((stage, index) => {
      const reached = activeStageIndex >= index;

      const stageDate =
        stage === "Collected"
          ? order.fulfillment.collectedAtUtc
          : stage === "Packed"
            ? order.fulfillment.packedAtUtc
            : order.fulfillment.dispatchedAtUtc;

      return {
        key: stage,
        label: humanizeLabel(stage),
        meta: reached
          ? formatDateTime(
              stageDate ?? order.fulfillment.lastUpdatedAtUtc ?? null,
            )
          : "Waiting upstream",
        dotClassName: reached
          ? "absolute left-0 top-1 inline-flex size-3 rounded-full bg-primary"
          : "absolute left-0 top-1 inline-flex size-3 rounded-full border border-border bg-background",
      };
    }),
  ];

  return (
    <div className="section-grid">
      <PageHeader
        title={order.orderNumber}
        aside={
          <Button asChild variant="secondary">
            <Link to="/orders">
              <ArrowLeft className="size-4" />
              Back to orders
            </Link>
          </Button>
        }
      />

      <div className="grid gap-4 xl:grid-cols-[0.95fr_1.05fr]">
        <div className="space-y-4">
          <Card>
            <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div className="space-y-1">
                <CardTitle>Summary</CardTitle>
                <CardDescription>
                  Order placed {formatDateTime(order.createdAt)} for{" "}
                  {order.userId}
                </CardDescription>
              </div>
              <StatusBadge value={visibleOrderStatus} />
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-3 md:grid-cols-2">
                <SummaryTile
                  label="Items total"
                  value={formatCurrency(order.itemsTotal)}
                />
                <SummaryTile
                  label="Grand total"
                  value={formatCurrency(order.totalPrice)}
                />
                <SummaryTile
                  label="Payment option"
                  value={order.paymentOption.name}
                />
                <SummaryTile
                  label="Courier"
                  value={order.deliveryOption.courierName}
                />
                <SummaryTile
                  label="Delivery option"
                  value={order.deliveryOption.name}
                />
                <SummaryTile
                  label="ETA window"
                  value={`${order.deliveryOption.estimatedDaysMin}-${order.deliveryOption.estimatedDaysMax} days`}
                />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Delivery and fulfillment</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="status-track space-y-4 pl-1">
                {timelineEntries.map((entry) => (
                  <div
                    key={entry.key}
                    className="relative flex items-start gap-4 pl-6">
                    <span className={entry.dotClassName} />
                    <div className="space-y-1">
                      <p className="text-sm font-semibold text-foreground">
                        {entry.label}
                      </p>
                      <p className="text-sm text-muted-foreground">
                        {entry.meta}
                      </p>
                      
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </div>

        <div className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Ordered items</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              {order.items.map((item, index) => (
                <div key={item.id} className="space-y-4">
                  <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                    <div>
                      <p className="text-base font-semibold text-foreground">
                        {item.itemName}
                      </p>
                      <p className="font-mono text-sm text-muted-foreground">
                        {item.quantity} × {formatCurrency(item.unitPrice)}
                      </p>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      <BadgeTile
                        icon={<Package className="size-4" />}
                        label="Line total"
                        value={formatCurrency(item.totalPrice)}
                      />
                    </div>
                  </div>
                  {index < order.items.length - 1 ? <Separator /> : null}
                </div>
              ))}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}

function SummaryTile({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-border/70 bg-white p-4">
      <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
        {label}
      </p>
      <p className="mt-2 text-base font-semibold text-foreground">{value}</p>
    </div>
  );
}

function BadgeTile({
  icon,
  label,
  value,
}: {
  icon: ReactNode;
  label: string;
  value: string;
}) {
  return (
    <div className="inline-flex items-center gap-2 rounded-md border border-border bg-background px-3 py-1.5 text-xs font-semibold uppercase tracking-[0.16em] text-secondary-foreground">
      {icon}
      <span>{label}</span>
      <span className="font-mono text-foreground">{value}</span>
    </div>
  );
}

function getPaymentTimelineEntry(
  paymentStatus: string,
  paymentStatusUpdatedAtUtc?: string | null
): TimelineEntry {
  const normalizedStatus = paymentStatus.trim().toLowerCase();
  const timestampLabel = paymentStatusUpdatedAtUtc
    ? formatDateTime(paymentStatusUpdatedAtUtc)
    : normalizedStatus === "pending"
      ? "Waiting for gateway confirmation"
      : "Time not available";

  if (normalizedStatus === "authorized") {
    return {
      key: "payment",
      label: "Payment succeeded",
      meta: timestampLabel,
      dotClassName:
        "absolute left-0 top-1 inline-flex size-3 rounded-full border border-border bg-primary",
    };
  }

  if (normalizedStatus === "declined") {
    return {
      key: "payment",
      label: "Payment declined",
      meta: timestampLabel,
      dotClassName:
        "absolute left-0 top-1 inline-flex size-3 rounded-full border border-border bg-background",
    };
  }

  if (normalizedStatus === "timedout") {
    return {
      key: "payment",
      label: "Payment timed out",
      meta: timestampLabel,
      dotClassName:
        "absolute left-0 top-1 inline-flex size-3 rounded-full border border-border bg-background",
    };
  }

  return {
    key: "payment",
    label: `Payment ${humanizeLabel(paymentStatus).toLowerCase()}`,
    meta: timestampLabel,
    dotClassName:
      "absolute left-0 top-1 inline-flex size-3 rounded-full border border-border bg-background",
  };
}
