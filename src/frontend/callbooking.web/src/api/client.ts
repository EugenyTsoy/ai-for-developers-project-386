import createClient from 'openapi-fetch'
import type { Client } from 'openapi-fetch'
import type { components, paths } from './generated/schema'

/** RFC 9457 problem details body, guaranteed on contract error responses. */
export type ProblemDetails = components['schemas']['ProblemDetails']

/** A successful call carrying the typed response body. */
export type ApiSuccess<T> = {
  ok: true
  value: T
}

/**
 * A resolved HTTP response whose status is a contract error (e.g. 400/404/409).
 * The body is the RFC 9457 problem detail for that status.
 */
export type ApiHttpFailure = {
  ok: false
  kind: 'http'
  status: number
  problem: ProblemDetails
}

/** A network/transport failure that never produced an HTTP response. */
export type ApiTransportFailure = {
  ok: false
  kind: 'transport'
  cause: unknown
}

/**
 * The approved three-way outcome: success, HTTP contract error, or transport
 * failure. Handwritten UI code matches on `ok`/`kind` — no custom error codes
 * are invented here.
 */
export type ApiResult<T> = ApiSuccess<T> | ApiHttpFailure | ApiTransportFailure

/** The typed openapi-fetch client bound to `paths` from the generated schema. */
export type ApiClient = Client<paths>

type RawApiResponse<T> = {
  data?: T
  error?: unknown
  response: Response
}

/**
 * Resolve the API base URL from the Vite environment, falling back to the
 * current origin (relative `/api/...` paths) when it is not set.
 */
function resolveBaseUrl(): string | undefined {
  const value: unknown = import.meta.env.VITE_API_BASE_URL
  return typeof value === 'string' && value.length > 0 ? value : undefined
}

/**
 * Create a framework-agnostic typed API client.
 *
 * `baseUrl` defaults to the value of `VITE_API_BASE_URL` when set, otherwise
 * requests use the current origin. `fetchImpl` is injectable for tests.
 * No React imports or hooks live here or in the generated schema.
 */
export function createApiClient(baseUrl?: string, fetchImpl?: typeof fetch): ApiClient {
  return createClient<paths>({
    ...(baseUrl ? { baseUrl } : {}),
    ...(fetchImpl ? { fetch: fetchImpl } : {}),
  })
}

/** Shared client used by the application. Configure via `VITE_API_BASE_URL`. */
export const apiClient = createApiClient(resolveBaseUrl())

/**
 * Normalise an openapi-fetch response into the approved three-way error
 * surface. Does not duplicate route paths, HTTP methods or DTOs — those are
 * derived from the generated schema at compile time.
 */
export async function toApiResult<T>(request: Promise<RawApiResponse<T>>): Promise<ApiResult<T>> {
  try {
    const { data, error, response } = await request
    if (response.ok) {
      return { ok: true, value: data as T }
    }
    return {
      ok: false,
      kind: 'http',
      status: response.status,
      problem: error as ProblemDetails,
    }
  } catch (cause) {
    return { ok: false, kind: 'transport', cause }
  }
}