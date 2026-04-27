export function resolveVisibleOrderStatus(
  status: string,
  fulfillmentStatus: string,
) {
  if (status === 'Cancelled') {
    return status
  }

  if (fulfillmentStatus === 'Reserved') {
    return 'Confirmed'
  }

  return fulfillmentStatus !== 'Pending' ? fulfillmentStatus : status
}