import { describe, expect, it } from 'vitest'
import { createApiClient, toApiResult } from './client'
import type { components } from './generated/schema'

type BookingConfirmation = components['schemas']['BookingConfirmation']

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  })
}

const confirmation: BookingConfirmation = {
  start: '2026-10-12T09:00:00Z',
  end: '2026-10-12T09:30:00Z',
}

describe('api client', () => {
  it('maps a 201 response to a success result with the typed value', async () => {
    const client = createApiClient('http://api.example.test', async () => jsonResponse(201, confirmation))

    const result = await toApiResult(
      client.POST('/api/bookings', {
        body: {
          eventTypeId: 'evt_1',
          start: '2026-10-12T09:00:00Z',
          name: 'Evgeniy',
          email: 'evgeniy@example.com',
        },
      }),
    )

    expect(result).toEqual({ ok: true, value: confirmation })
  })

  it('maps a contract error to an http failure with ProblemDetails', async () => {
    const problem = { status: 404, title: 'Not Found', detail: 'Unknown event type' }
    const client = createApiClient('http://api.example.test', async () => jsonResponse(404, problem))

    const result = await toApiResult(
      client.GET('/api/event-types/{eventTypeId}/slots', {
        params: { path: { eventTypeId: 'missing' } },
      }),
    )

    expect(result).toMatchObject({ ok: false, kind: 'http', status: 404 })
    expect((result as { problem?: unknown }).problem).toEqual(problem)
  })

  it('maps a rejected fetch to a transport failure', async () => {
    const client = createApiClient('http://api.example.test', async () => {
      throw new TypeError('network down')
    })

    const result = await toApiResult(client.GET('/api/calendar'))

    expect(result).toMatchObject({ ok: false, kind: 'transport' })
    expect((result as { cause?: unknown }).cause).toBeInstanceOf(TypeError)
  })
})