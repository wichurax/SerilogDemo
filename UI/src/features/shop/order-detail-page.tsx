import type { ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Package } from "lucide-react";
import { Link, useParams } from "react-router-dom";

import { PageHeader } from "@/components/layout/page-header";
import { EmptyState } from "@/components/shared/empty-state";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { getOrder } from "@/lib/api/client";
import { getErrorMessage } from "@/lib/api/http";
import { formatCurrency, formatDateTime, humanizeLabel } from "@/lib/format";
import { useDemoUserStore } from "@/stores/demo-user-store";

const fulfillmentStages = ["Collected", "Packed", "Shipped"];

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
            <CardHeader>
              <CardTitle>Summary</CardTitle>
              <CardDescription>
                Order placed {formatDateTime(order.createdAt)} for{" "}
                {order.userId}
              </CardDescription>
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
              {order.paymentFailureReason ? (
                <div className="rounded-lg border border-destructive/20 bg-destructive/10 p-4 text-sm leading-6 text-destructive">
                  {order.paymentFailureReason}
                </div>
              ) : null}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Delivery and fulfillment</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="status-track space-y-4 pl-1">
                {fulfillmentStages.map((stage, index) => {
                  const reached = activeStageIndex >= index;

                  const stageDate =
                    stage === "Collected"
                      ? order.fulfillment.collectedAtUtc
                      : stage === "Packed"
                        ? order.fulfillment.packedAtUtc
                        : order.fulfillment.dispatchedAtUtc;

                  const dateLabel = reached
                    ? formatDateTime(
                        stageDate ?? order.fulfillment.lastUpdatedAtUtc ?? null,
                      )
                    : null;

                  return (
                    <div
                      key={stage}
                      className="relative flex items-start gap-4 pl-6">
                      <span
                        className={
                          reached
                            ? "absolute left-0 top-1 inline-flex size-3 rounded-full bg-primary"
                            : "absolute left-0 top-1 inline-flex size-3 rounded-full border border-border bg-background"
                        }
                      />
                      <div className="space-y-1">
                        <p className="text-sm font-semibold text-foreground">
                          {humanizeLabel(stage)}
                        </p>
                        <p className="text-sm text-muted-foreground">
                          {dateLabel ?? "Waiting upstream"}
                        </p>
                      </div>
                    </div>
                  );
                })}
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
