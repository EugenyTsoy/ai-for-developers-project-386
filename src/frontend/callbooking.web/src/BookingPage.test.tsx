import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, test, vi } from 'vitest'
import { createApiClient } from './api/client'
import type { components } from './api/generated/schema'
import BookingPage from './BookingPage'

const eventTypes: components['schemas']['EventType'][] = [
  { id: 'intro', name: 'Знакомство', description: 'Обсудим задачу', durationMinutes: 30 },
  { id: 'consultation', name: 'Консультация', durationMinutes: 60 },
]

const slots: components['schemas']['Slot'][] = [
  { start: '2026-10-25T00:00:00Z', end: '2026-10-25T00:30:00Z' },
  { start: '2026-10-25T01:00:00Z', end: '2026-10-25T01:30:00Z' },
]

afterEach(() => vi.unstubAllEnvs())

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': status >= 400 ? 'application/problem+json' : 'application/json' },
  })
}

function clientWith(eventTypesResponse: () => Promise<Response>) {
  return createApiClient('http://api.example.test', (request) => {
    const path = new URL((request as Request).url).pathname
    return path === '/api/calendar'
      ? Promise.resolve(json({ timezone: 'Europe/Berlin' }))
      : eventTypesResponse()
  })
}

function deferredResponse() {
  let resolve!: (response: Response) => void
  const promise = new Promise<Response>((complete) => { resolve = complete })
  return { promise, resolve }
}

test('shows separate CalendarInfo and EventTypes loading states', () => {
  const client = createApiClient('http://api.example.test', () => new Promise<Response>(() => {}))
  render(<BookingPage client={client} />)

  expect(screen.getByText('Загрузка календаря…')).toBeInTheDocument()
  expect(screen.getByText('Загрузка типов событий…')).toBeInTheDocument()
  expect(screen.queryByText('Нет типов событий для записи.')).not.toBeInTheDocument()
})

test('renders all EventTypes with optional descriptions and duration', async () => {
  render(<BookingPage client={clientWith(async () => json(eventTypes))} />)

  expect(await screen.findByText('Часовой пояс календаря: Europe/Berlin')).toBeInTheDocument()
  expect(await screen.findByText('Знакомство')).toBeInTheDocument()
  expect(screen.getByText('Обсудим задачу')).toBeInTheDocument()
  expect(screen.getByText('Консультация')).toBeInTheDocument()
  expect(screen.getByText('30 мин')).toBeInTheDocument()
  expect(screen.getByText('60 мин')).toBeInTheDocument()
  expect(screen.queryByText(/undefined|null/)).not.toBeInTheDocument()
})

test.each([
  ['empty', async () => json([]), 'Нет типов событий для записи.'],
  ['HTTP', async () => json({ status: 503, title: 'Unavailable', detail: 'Do not display me' }, 503), 'Не удалось загрузить типы событий (HTTP 503).'],
  ['transport', async () => { throw new TypeError('offline') }, 'Нет соединения: не удалось загрузить типы событий.'],
])('distinguishes EventTypes %s from loading and data', async (outcome, response, message) => {
  render(<BookingPage client={clientWith(response)} />)

  expect(await screen.findByText(message)).toBeInTheDocument()
  expect(screen.queryByText('Загрузка типов событий…')).not.toBeInTheDocument()
  expect(screen.queryByText('Знакомство')).not.toBeInTheDocument()
  expect(screen.queryByText('Do not display me')).not.toBeInTheDocument()
  if (outcome === 'empty') expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  else expect(screen.getByRole('alert')).toHaveTextContent(message)
})

test('selecting an EventType requests its Slots and shows loading without hiding types', async () => {
  const user = userEvent.setup()
  const pending = deferredResponse()
  const requests: string[] = []
  const client = createApiClient('http://api.example.test', async (request) => {
    const path = new URL((request as Request).url).pathname
    requests.push(path)
    if (path === '/api/calendar') return json({ timezone: 'Europe/Berlin' })
    if (path === '/api/event-types') return json(eventTypes)
    return pending.promise
  })
  render(<BookingPage client={client} />)
  const intro = await screen.findByRole('button', { name: /Знакомство/ })
  expect(requests).not.toContain('/api/event-types/intro/slots')

  await user.click(intro)

  expect(intro).toHaveAttribute('aria-pressed', 'true')
  expect(screen.getByRole('button', { name: /Консультация/ })).toHaveAttribute('aria-pressed', 'false')
  expect(screen.getByText('Загрузка доступного времени…')).toBeInTheDocument()
  await waitFor(() => expect(requests).toContain('/api/event-types/intro/slots'))
  pending.resolve(json([]))
  expect(await screen.findByText('Нет доступного времени для выбранного типа события.')).toBeInTheDocument()
  expect(intro).toBeInTheDocument()
  expect(screen.getByRole('button', { name: /Консультация/ })).toBeInTheDocument()
  expect(screen.queryByRole('alert')).not.toBeInTheDocument()
})

test.each([
  ['HTTP', async () => json({ status: 404, title: 'Not Found', detail: 'Do not display me' }, 404), 'Не удалось загрузить доступное время (HTTP 404).'],
  ['transport', async () => { throw new TypeError('offline') }, 'Нет соединения: не удалось загрузить доступное время.'],
])('distinguishes Slots %s failure from empty availability', async (_, response, message) => {
  const user = userEvent.setup()
  const client = createApiClient('http://api.example.test', (request) => {
    const path = new URL((request as Request).url).pathname
    if (path === '/api/calendar') return Promise.resolve(json({ timezone: 'Europe/Berlin' }))
    if (path === '/api/event-types') return Promise.resolve(json(eventTypes))
    return response()
  })
  render(<BookingPage client={client} />)
  await user.click(await screen.findByRole('button', { name: /Знакомство/ }))

  expect(await screen.findByRole('alert')).toHaveTextContent(message)
  expect(screen.queryByText('Нет доступного времени для выбранного типа события.')).not.toBeInTheDocument()
  expect(screen.queryByText('Загрузка доступного времени…')).not.toBeInTheDocument()
  expect(screen.queryByText('Do not display me')).not.toBeInTheDocument()
})

test('renders calendar-local Slots, distinguishes repeated labels by UTC offset and preserves selection', async () => {
  vi.stubEnv('TZ', 'America/Los_Angeles')
  // The same instant is 17:00 on the previous day in the ambient timezone.
  expect(new Intl.DateTimeFormat('en-GB', { hour: '2-digit', minute: '2-digit' })
    .format(new Date(slots[0].start))).toBe('17:00')
  const user = userEvent.setup()
  const requests: Request[] = []
  const client = createApiClient('http://api.example.test', async (request) => {
    requests.push(request as Request)
    const path = new URL((request as Request).url).pathname
    if (path === '/api/calendar') return json({ timezone: 'Europe/Berlin' })
    if (path === '/api/event-types') return json(eventTypes)
    return json(slots)
  })
  render(<BookingPage client={client} />)
  await user.click(await screen.findByRole('button', { name: /Знакомство/ }))

  const early = await screen.findByRole('button', { name: '25.10.2026, 02:00 UTC+02:00 — 25.10.2026, 02:30 UTC+02:00' })
  const late = screen.getByRole('button', { name: '25.10.2026, 02:00 UTC+01:00 — 25.10.2026, 02:30 UTC+01:00' })
  await user.click(early)
  expect(early).toHaveAttribute('aria-pressed', 'true')
  expect(late).toHaveAttribute('aria-pressed', 'false')
  await user.click(late)
  expect(early).toHaveAttribute('aria-pressed', 'false')
  expect(late).toHaveAttribute('aria-pressed', 'true')
  expect(screen.getByRole('button', { name: /Знакомство/ })).toHaveAttribute('aria-pressed', 'true')
  expect(screen.getByText('Выбор времени не резервирует слот.')).toBeInTheDocument()
  expect(requests.every((request) => request.method === 'GET')).toBe(true)
  expect(requests).toHaveLength(3)
})

test.each([
  ['HTTP', async () => json({ status: 503, title: 'Unavailable' }, 503), 'Не удалось загрузить календарь (HTTP 503).'],
  ['transport', async () => { throw new TypeError('offline') }, 'Нет соединения: не удалось загрузить календарь.'],
])('CalendarInfo %s failure never falls back to browser-local Slot labels', async (_, response, message) => {
  const user = userEvent.setup()
  const client = createApiClient('http://api.example.test', (request) => {
    const path = new URL((request as Request).url).pathname
    if (path === '/api/calendar') return response()
    return Promise.resolve(json(path === '/api/event-types' ? eventTypes : slots))
  })
  render(<BookingPage client={client} />)
  expect(await screen.findByRole('alert')).toHaveTextContent(message)
  await user.click(await screen.findByRole('button', { name: /Знакомство/ }))
  await waitFor(() => expect(screen.queryByText('Загрузка доступного времени…')).not.toBeInTheDocument())
  expect(screen.queryByRole('button', { name: /02:00/ })).not.toBeInTheDocument()
})

test('waits for calendar metadata before displaying loaded Slot dates', async () => {
  const pending = deferredResponse()
  const user = userEvent.setup()
  const client = createApiClient('http://api.example.test', async (request) => {
    const path = new URL((request as Request).url).pathname
    if (path === '/api/calendar') return pending.promise
    return json(path === '/api/event-types' ? eventTypes : slots)
  })
  render(<BookingPage client={client} />)
  await user.click(await screen.findByRole('button', { name: /Знакомство/ }))
  await waitFor(() => expect(screen.queryByText('Загрузка доступного времени…')).not.toBeInTheDocument())
  expect(screen.getByText('Загрузка календаря…')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: /02:00/ })).not.toBeInTheDocument()
  pending.resolve(json({ timezone: 'Europe/Berlin' }))
  expect(await screen.findByRole('button', { name: /02:00 UTC\+02:00/ })).toBeInTheDocument()
})

test('switching EventTypes clears Slot selection and ignores late availability responses', async () => {
  const user = userEvent.setup()
  const pending = deferredResponse()
  const client = createApiClient('http://api.example.test', async (request) => {
    const path = new URL((request as Request).url).pathname
    if (path === '/api/calendar') return json({ timezone: 'Europe/Berlin' })
    if (path === '/api/event-types') return json(eventTypes)
    if (path === '/api/event-types/consultation/slots') return pending.promise
    return json(slots)
  })
  render(<BookingPage client={client} />)
  await user.click(await screen.findByRole('button', { name: /Знакомство/ }))
  const early = await screen.findByRole('button', { name: /02:00 UTC\+02:00/ })
  await user.click(early)
  await user.click(screen.getByRole('button', { name: /Знакомство/ }))
  expect(early).toHaveAttribute('aria-pressed', 'true')

  await user.click(screen.getByRole('button', { name: /Консультация/ }))
  expect(screen.queryByRole('button', { name: /02:00/ })).not.toBeInTheDocument()
  expect(screen.getByText('Загрузка доступного времени…')).toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: /Знакомство/ }))
  expect(await screen.findByRole('button', { name: /02:00 UTC\+02:00/ })).toHaveAttribute('aria-pressed', 'false')
  await act(async () => { pending.resolve(json([])) })
  expect(screen.queryByText('Нет доступного времени для выбранного типа события.')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: /02:00 UTC\+02:00/ })).toBeInTheDocument()
})

test.each([
  ['Asia/Kolkata', '25.10.2026, 05:30 UTC+05:30 — 25.10.2026, 06:00 UTC+05:30'],
  ['America/Los_Angeles', '24.10.2026, 17:00 UTC-07:00 — 24.10.2026, 17:30 UTC-07:00'],
])('CalendarInfo %s determines the date, time and offset of both Slot endpoints', async (timezone, label) => {
  vi.stubEnv('TZ', 'UTC')
  const user = userEvent.setup()
  const client = createApiClient('http://api.example.test', async (request) => {
    const path = new URL((request as Request).url).pathname
    if (path === '/api/calendar') return json({ timezone })
    return json(path === '/api/event-types' ? eventTypes : [slots[0]])
  })
  render(<BookingPage client={client} />)
  await user.click(await screen.findByRole('button', { name: /Знакомство/ }))

  expect(await screen.findByRole('button', { name: label })).toBeInTheDocument()
  expect(screen.getByText(`Часовой пояс календаря: ${timezone}`)).toBeInTheDocument()
})
