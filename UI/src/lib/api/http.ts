const servicePrefixes = {
  api: '',
  fulfillment: '/fulfillment-api',
} as const

type ServiceKind = keyof typeof servicePrefixes

type RequestOptions = Omit<RequestInit, 'body'> & {
  body?: BodyInit | Record<string, unknown> | undefined
  service?: ServiceKind
  userId?: string
}

export class ApiError extends Error {
  status: number
  data: unknown

  constructor(message: string, status: number, data: unknown) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.data = data
  }
}

export async function requestJson<T>(
  path: string,
  options?: RequestOptions,
  okStatuses: number[] = [200, 201],
) {
  const { data } = await requestWithStatus<T>(path, okStatuses, options)
  return data as T
}

export async function requestWithStatus<T>(
  path: string,
  okStatuses: number[],
  options: RequestOptions = {},
) {
  const { body, headers: rawHeaders, service = 'api', userId, ...init } = options
  const headers = new Headers(rawHeaders)

  if (userId) {
    headers.set('X-User-Id', userId)
  }

  const isBodyPassthrough = typeof body === 'string' || body instanceof FormData || body instanceof Blob

  if (body !== undefined && !isBodyPassthrough && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(`${servicePrefixes[service]}${path}`, {
    ...init,
    headers,
    body: body === undefined ? undefined : isBodyPassthrough ? body : JSON.stringify(body),
  })

  const responseData = await parseResponse(response)

  if (!okStatuses.includes(response.status)) {
    throw new ApiError(extractMessage(responseData) ?? response.statusText, response.status, responseData)
  }

  return {
    status: response.status,
    data: responseData as T,
  }
}

export function getErrorMessage(error: unknown) {
  if (error instanceof ApiError) {
    return error.message
  }

  if (error instanceof Error) {
    return error.message
  }

  return 'Unexpected error'
}

async function parseResponse(response: Response) {
  if (response.status === 204) {
    return undefined
  }

  const contentType = response.headers.get('content-type') ?? ''
  if (contentType.includes('application/json')) {
    return response.json()
  }

  const text = await response.text()
  return text.length > 0 ? text : undefined
}

function extractMessage(data: unknown) {
  if (typeof data === 'string' && data.length > 0 && !isHtmlLikeText(data)) {
    return data
  }

  if (
    typeof data === 'object' &&
    data !== null &&
    'message' in data &&
    typeof data.message === 'string'
  ) {
    return data.message
  }

  return undefined
}

function isHtmlLikeText(value: string) {
  return /^<(?:!doctype html|html|head|body)\b/i.test(value.trim())
}