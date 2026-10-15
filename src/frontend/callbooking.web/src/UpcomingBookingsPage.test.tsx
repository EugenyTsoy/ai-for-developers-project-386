import { act, render, screen, within } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import { createApiClient } from './api/client'
import type { components } from './api/generated/schema'
import UpcomingBookingsPage from './UpcomingBookingsPage'

const bookings: components['schemas']['UpcomingBooking'][] = [
  { eventTypeName: 'Знакомство', guestName: 'Анна', guestEmail: 'anna@example.test',
    start: '2026-10-25T00:00:00Z', end: '2026-10-25T00:30:00Z' },
  { eventTypeName: 'Консультация', guestName: 'Борис', guestEmail: 'boris@example.test',
    start: '2026-10-25T01:00:00Z', end: '2026-10-25T01:30:00Z' },
]

afterEach(() => vi.unstubAllEnvs())

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': status >= 400 ? 'application/problem+json' : 'application/json' },
  })
}

function clientWith({
  calendarResponse = async () => json({ timezone: 'Europe/Berlin' }),
  bookingsResponse = async () => json(bookings),
}: {
  calendarResponse?: () => Promise<Response>
  bookingsResponse?: () => Promise<Response>
} = {}) {
  return createApiClient('http://api.example.test', (request) => {
    const path = new URL((request as Request).url).pathname
    if (path === '/api/calendar') return calendarResponse()
    if (path === '/api/bookings/upcoming') return bookingsResponse()
    throw new Error(`Unexpected request: ${path}`)
  })
}

function deferredResponse() {
  let resolve!: (response: Response) => void
  const promise = new Promise<Response>((complete) => { resolve = complete })
  return { promise, resolve }
}

test('shows loading until the Upcoming Booking response arrives, then a normal empty state', async () => {
  const pending = deferredResponse()
  render(<UpcomingBookingsPage client={clientWith({ bookingsResponse: () => pending.promise })} />)
  expect(screen.getByText('Загрузка предстоящих записей…')).toHaveAttribute('role', 'status')
  expect(screen.queryByText('Нет предстоящих записей.')).not.toBeInTheDocument()
  pending.resolve(json([]))
  expect(await screen.findByText('Нет предстоящих записей.')).toBeInTheDocument()
  expect(screen.queryByText('Загрузка предстоящих записей…')).not.toBeInTheDocument()
  expect(screen.queryByRole('alert')).not.toBeInTheDocument()
})

test.each([
  [503, 'Не удалось загрузить предстоящие записи (HTTP 503).'],
  [null, 'Нет соединения: не удалось загрузить предстоящие записи.'],
])('distinguishes Upcoming Booking failure %s from an empty list without displaying ProblemDetails', async (status, message) => {
  render(<UpcomingBookingsPage client={clientWith({ bookingsResponse: async () => {
    if (status === null) throw new TypeError('offline')
    return json({ status, title: 'Unavailable', detail: 'Do not display me' }, status)
  } })} />)
  expect(await screen.findByRole('alert')).toHaveTextContent(message)
  expect(screen.queryByText('Нет предстоящих записей.')).not.toBeInTheDocument()
  expect(screen.queryByText('Загрузка предстоящих записей…')).not.toBeInTheDocument()
  expect(screen.queryByText('Do not display me')).not.toBeInTheDocument()
})

test('renders every Booking field in calendar timezone, distinguishing repeated local labels by offset', async () => {
  vi.stubEnv('TZ', 'America/Los_Angeles')
  expect(new Intl.DateTimeFormat('en-GB', { hour: '2-digit', minute: '2-digit' })
    .format(new Date(bookings[0].start))).toBe('17:00')
  render(<UpcomingBookingsPage client={clientWith()} />)
  const items = await screen.findAllByRole('listitem')
  expect(items).toHaveLength(2)
  expect(within(items[0]).getByRole('heading', { name: 'Знакомство' })).toBeInTheDocument()
  expect(within(items[0]).getByText('Анна')).toBeInTheDocument()
  expect(within(items[0]).getByText('anna@example.test')).toBeInTheDocument()
  expect(within(items[0]).getByText('25.10.2026, 02:00 UTC+02:00 — 25.10.2026, 02:30 UTC+02:00')).toBeInTheDocument()
  expect(within(items[1]).getByRole('heading', { name: 'Консультация' })).toBeInTheDocument()
  expect(within(items[1]).getByText('Борис')).toBeInTheDocument()
  expect(within(items[1]).getByText('boris@example.test')).toBeInTheDocument()
  expect(within(items[1]).getByText('25.10.2026, 02:00 UTC+01:00 — 25.10.2026, 02:30 UTC+01:00')).toBeInTheDocument()
  expect(screen.getByText('Часовой пояс календаря: Europe/Berlin')).toBeInTheDocument()
  expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  expect(screen.queryByRole('status')).not.toBeInTheDocument()
  expect(screen.queryByText('Нет предстоящих записей.')).not.toBeInTheDocument()
  expect(document.body).not.toHaveTextContent(/undefined|null|2026-10-25T/)
})

test.each([
  [503, 'Не удалось загрузить календарь (HTTP 503).'],
  [null, 'Нет соединения: не удалось загрузить календарь.'],
])('CalendarInfo failure %s never displays browser-local dates or an empty list instead', async (status, message) => {
  render(<UpcomingBookingsPage client={clientWith({ calendarResponse: async () => {
    if (status === null) throw new TypeError('offline')
    return json({ status, title: 'Unavailable', detail: 'Do not display me' }, status)
  } })} />)
  expect(await screen.findByRole('alert')).toHaveTextContent(message)
  expect(screen.queryByRole('list')).not.toBeInTheDocument()
  expect(screen.queryByText('Нет предстоящих записей.')).not.toBeInTheDocument()
  expect(screen.queryByText('Do not display me')).not.toBeInTheDocument()
  expect(screen.queryByRole('status')).not.toBeInTheDocument()
})

test('waits for CalendarInfo before rendering already loaded Booking dates', async () => {
  const pending = deferredResponse()
  const loadedBookings = deferredResponse()
  render(<UpcomingBookingsPage client={clientWith({
    calendarResponse: () => pending.promise, bookingsResponse: () => loadedBookings.promise,
  })} />)
  // Drain the Booking response while the calendar request remains pending.
  await act(async () => { loadedBookings.resolve(json(bookings)) })
  expect(screen.getByText('Загрузка календаря…')).toHaveAttribute('role', 'status')
  expect(screen.queryByText('Загрузка предстоящих записей…')).not.toBeInTheDocument()
  expect(screen.queryByRole('list')).not.toBeInTheDocument()
  pending.resolve(json({ timezone: 'Europe/Berlin' }))
  expect(await screen.findAllByRole('listitem')).toHaveLength(2)
  expect(screen.queryByRole('status')).not.toBeInTheDocument()
})

test.each([
  ['UTC', 'Asia/Kolkata', '25.10.2026, 05:30 UTC+05:30 — 25.10.2026, 06:00 UTC+05:30'],
  ['Asia/Tokyo', 'America/Los_Angeles', '24.10.2026, 17:00 UTC-07:00 — 24.10.2026, 17:30 UTC-07:00'],
])('calendar timezone %s → %s determines both endpoint dates and offsets', async (ambient, timezone, label) => {
  vi.stubEnv('TZ', ambient)
  render(<UpcomingBookingsPage client={clientWith({
    calendarResponse: async () => json({ timezone }),
    bookingsResponse: async () => json([bookings[0]]),
  })} />)
  expect(await screen.findByText(label)).toBeInTheDocument()
  expect(screen.getByText(`Часовой пояс календаря: ${timezone}`)).toBeInTheDocument()
})

test('a late response after leaving the page cannot overwrite a freshly mounted list', async () => {
  const late = deferredResponse()
  const first = render(<UpcomingBookingsPage client={clientWith({ bookingsResponse: () => late.promise })} />)
  first.unmount()
  render(<UpcomingBookingsPage client={clientWith({ bookingsResponse: async () => json([]) })} />)
  expect(await screen.findByText('Нет предстоящих записей.')).toBeInTheDocument()
  await act(async () => { late.resolve(json(bookings)) })
  expect(screen.getByText('Нет предстоящих записей.')).toBeInTheDocument()
  expect(screen.queryByRole('list')).not.toBeInTheDocument()
})
