import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
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

function clientWith({
  calendarResponse = async () => json({ timezone: 'Europe/Berlin' }),
  eventTypesResponse = async () => json(eventTypes),
  slotsResponse = async () => json(slots),
  bookingResponse = async () => json(slots[0], 201),
  requests = [],
}: {
  calendarResponse?: () => Promise<Response>
  eventTypesResponse?: () => Promise<Response>
  slotsResponse?: (path: string) => Promise<Response>
  bookingResponse?: () => Promise<Response>
  requests?: Request[]
} = {}) {
  return createApiClient('http://api.example.test', (request) => {
    requests.push(request as Request)
    const path = new URL((request as Request).url).pathname
    if (path === '/api/calendar') return calendarResponse()
    if (path === '/api/event-types') return eventTypesResponse()
    if (path === '/api/bookings') return bookingResponse()
    return slotsResponse(path)
  })
}

function deferredResponse() {
  let resolve!: (response: Response) => void
  const promise = new Promise<Response>((complete) => { resolve = complete })
  return { promise, resolve }
}

async function selectDate(date = '2026-10-25') {
  fireEvent.change(await screen.findByLabelText('Дата в часовом поясе календаря'), { target: { value: date } })
}

async function selectSlotAndFillGuest(client = clientWith()) {
  const user = userEvent.setup()
  render(<BookingPage client={client} />)
  await user.click(await screen.findByRole('button', { name: /Знакомство/ }))
  await selectDate()
  await user.click(await screen.findByRole('button', { name: /02:00 UTC\+02:00/ }))
  await user.type(screen.getByRole('textbox', { name: 'Имя' }), 'Guest Person')
  await user.type(screen.getByRole('textbox', { name: 'Email' }), 'guest@example.test')
  return user
}

test('409 invalidates the stale Slot, reloads availability and allows a new choice without automatic POST retry', async () => {
  const requests: Request[] = []
  let reads = 0
  let posts = 0
  const refresh = deferredResponse()
  const user = await selectSlotAndFillGuest(clientWith({
    requests,
    slotsResponse: async () => ++reads === 1 ? json(slots) : refresh.promise,
    bookingResponse: async () => ++posts === 1
      ? json({ status: 409, title: 'Conflict', detail: 'Do not display me' }, 409)
      : json(slots[1], 201),
  }))
  await user.click(screen.getByRole('button', { name: 'Записаться' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Слот больше недоступен, выберите другой.')
  expect(screen.queryByRole('button', { name: 'Записаться' })).not.toBeInTheDocument()
  expect(screen.getByText('Загрузка доступного времени…')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: /Знакомство/ })).toHaveAttribute('aria-pressed', 'true')
  refresh.resolve(json([slots[1]]))
  const other = await screen.findByRole('button', { name: /02:00 UTC\+01:00/ })
  expect(screen.getByLabelText('Дата в часовом поясе календаря')).toHaveValue('2026-10-25')
  expect(other).toHaveAttribute('aria-pressed', 'false')
  expect(screen.queryByRole('button', { name: /02:00 UTC\+02:00/ })).not.toBeInTheDocument()
  await user.click(other)
  expect(other).toHaveAttribute('aria-pressed', 'true')
  expect(screen.getByRole('textbox', { name: 'Имя' })).toBeInTheDocument()
  expect(requests.filter((request) => request.method === 'POST')).toHaveLength(1)
  expect(reads).toBe(2)
  expect(screen.queryByText('Запись подтверждена')).not.toBeInTheDocument()
  expect(screen.queryByText('Do not display me')).not.toBeInTheDocument()
  await user.type(screen.getByRole('textbox', { name: 'Имя' }), 'Guest Person')
  await user.type(screen.getByRole('textbox', { name: 'Email' }), 'guest@example.test')
  await user.click(screen.getByRole('button', { name: 'Записаться' }))
  expect(await screen.findByText('Запись подтверждена')).toBeInTheDocument()
  expect(requests.filter((request) => request.method === 'POST')).toHaveLength(2)
  expect(await requests.filter((request) => request.method === 'POST')[1].json()).toEqual({
    eventTypeId: 'intro', start: slots[1].start, name: 'Guest Person', email: 'guest@example.test',
  })
})

test('a late 409 availability refresh cannot overwrite a different EventType selection', async () => {
  const refresh = deferredResponse()
  let reads = 0
  const user = await selectSlotAndFillGuest(clientWith({
    slotsResponse: async (path) => path.includes('intro') && ++reads === 2 ? refresh.promise : json(slots),
    bookingResponse: async () => json({ status: 409, title: 'Conflict' }, 409),
  }))
  await user.click(screen.getByRole('button', { name: 'Записаться' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Слот больше недоступен')
  await user.click(screen.getByRole('button', { name: /Консультация/ }))
  await selectDate()
  const choice = await screen.findByRole('button', { name: /02:00 UTC\+01:00/ })
  await user.click(choice)
  await act(async () => { refresh.resolve(json([])) })
  expect(screen.getByRole('button', { name: /Консультация/ })).toHaveAttribute('aria-pressed', 'true')
  expect(choice).toHaveAttribute('aria-pressed', 'true')
  expect(screen.queryByText('Нет доступного времени для выбранного типа события.')).not.toBeInTheDocument()
  expect(screen.queryByRole('alert')).not.toBeInTheDocument()
})

test('a late POST response from an unmounted booking flow cannot confirm a fresh flow', async () => {
  const pending = deferredResponse()
  const user = userEvent.setup()
  const first = render(<BookingPage client={clientWith({ bookingResponse: () => pending.promise })} />)
  await user.click(await screen.findByRole('button', { name: /Знакомство/ }))
  await selectDate()
  await user.click(await screen.findByRole('button', { name: /02:00 UTC\+02:00/ }))
  await user.type(screen.getByRole('textbox', { name: 'Имя' }), 'Guest Person')
  await user.type(screen.getByRole('textbox', { name: 'Email' }), 'guest@example.test')
  await user.click(screen.getByRole('button', { name: 'Записаться' }))
  first.unmount()
  render(<BookingPage client={clientWith()} />)
  await user.click(await screen.findByRole('button', { name: /Консультация/ }))
  await selectDate()
  const choice = await screen.findByRole('button', { name: /02:00 UTC\+01:00/ })
  await user.click(choice)
  await act(async () => { pending.resolve(json(slots[0], 201)) })
  expect(screen.queryByText('Запись подтверждена')).not.toBeInTheDocument()
  expect(choice).toHaveAttribute('aria-pressed', 'true')
  expect(screen.getByRole('button', { name: 'Записаться' })).toBeEnabled()
})

test.each([
  [400, 'Не удалось создать запись (HTTP 400).'],
  [404, 'Не удалось создать запись (HTTP 404).'],
  [503, 'Не удалось создать запись (HTTP 503).'],
  [null, 'Нет соединения: подтверждение записи не получено.'],
])('Booking failure %s never confirms or automatically retries', async (status, message) => {
  const requests: Request[] = []
  const user = await selectSlotAndFillGuest(clientWith({
    requests,
    bookingResponse: async () => {
      if (status === null) throw new TypeError('offline')
      return json({ status, title: 'Failure', detail: 'Do not display me' }, status)
    },
  }))
  await user.click(screen.getByRole('button', { name: 'Записаться' }))
  expect(await screen.findByRole('alert')).toHaveTextContent(message)
  expect(screen.queryByText('Запись подтверждена')).not.toBeInTheDocument()
  expect(screen.queryByText('Do not display me')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Записаться' })).toBeEnabled()
  expect(requests.filter((request) => request.method === 'POST')).toHaveLength(1)
})

test('Guest can reload availability after a failed 409 refresh', async () => {
  let reads = 0
  const user = await selectSlotAndFillGuest(clientWith({
    slotsResponse: async () => ++reads === 2 ? json({ status: 503, title: 'Unavailable' }, 503) : json(slots),
    bookingResponse: async () => json({ status: 409, title: 'Conflict' }, 409),
  }))
  await user.click(screen.getByRole('button', { name: 'Записаться' }))
  expect(await screen.findByText('Не удалось загрузить доступное время (HTTP 503).')).toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Обновить доступное время' }))
  expect(await screen.findByRole('button', { name: /02:00 UTC\+01:00/ })).toHaveAttribute('aria-pressed', 'false')
  expect(reads).toBe(3)
})

test('changes before submit send only the final EventType and Slot despite late availability', async () => {
  const requests: Request[] = []
  const late = deferredResponse()
  let consultationReads = 0
  const user = await selectSlotAndFillGuest(clientWith({
    requests,
    slotsResponse: async (path) => path.includes('consultation') && ++consultationReads === 1
      ? late.promise : json(slots),
  }))
  await user.click(screen.getByRole('button', { name: /Консультация/ }))
  expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: /Знакомство/ }))
  await selectDate()
  await user.click(await screen.findByRole('button', { name: /02:00 UTC\+02:00/ }))
  await user.click(screen.getByRole('button', { name: /02:00 UTC\+01:00/ }))
  await user.click(screen.getByRole('button', { name: /Консультация/ }))
  await selectDate()
  await user.click(await screen.findByRole('button', { name: /02:00 UTC\+01:00/ }))
  await act(async () => { late.resolve(json([])) })
  expect(screen.getByRole('button', { name: /02:00 UTC\+01:00/ })).toHaveAttribute('aria-pressed', 'true')
  await user.type(screen.getByRole('textbox', { name: 'Имя' }), 'Final Guest')
  await user.type(screen.getByRole('textbox', { name: 'Email' }), 'final@example.test')
  await user.click(screen.getByRole('button', { name: 'Записаться' }))
  expect(await screen.findByText('Запись подтверждена')).toBeInTheDocument()
  const posts = requests.filter((request) => request.method === 'POST')
  expect(posts).toHaveLength(1)
  expect(await posts[0].json()).toEqual({
    eventTypeId: 'consultation', start: slots[1].start, name: 'Final Guest', email: 'final@example.test',
  })
})

test.each([
  ['Europe/Berlin', slots[0], '25.10.2026, 02:00 UTC+02:00 — 25.10.2026, 02:30 UTC+02:00'],
  ['Asia/Kolkata', slots[0], '25.10.2026, 05:30 UTC+05:30 — 25.10.2026, 06:00 UTC+05:30'],
  ['America/Los_Angeles', slots[0], '24.10.2026, 17:00 UTC-07:00 — 24.10.2026, 17:30 UTC-07:00'],
])('confirmation explicitly uses %s rather than ambient UTC', async (timezone, confirmation, label) => {
  vi.stubEnv('TZ', 'UTC')
  const user = userEvent.setup()
  render(<BookingPage client={clientWith({
    calendarResponse: async () => json({ timezone }),
    bookingResponse: async () => json(confirmation, 201),
  })} />)
  await user.click(await screen.findByRole('button', { name: /Знакомство/ }))
  await selectDate(timezone === 'America/Los_Angeles' ? '2026-10-24' : '2026-10-25')
  await user.click((await screen.findAllByRole('button', { name: /UTC[+-]/ }))[0])
  await user.type(screen.getByRole('textbox', { name: 'Имя' }), 'Guest Person')
  await user.type(screen.getByRole('textbox', { name: 'Email' }), 'guest@example.test')
  await user.click(screen.getByRole('button', { name: 'Записаться' }))
  expect(await screen.findByText('Запись подтверждена')).toBeInTheDocument()
  expect(screen.getByText(label)).toBeInTheDocument()
  expect(screen.getByText(`Часовой пояс календаря: ${timezone}`)).toBeInTheDocument()
})

test('pending submit protects against duplicates and confirms only the returned Booking in calendar timezone', async () => {
  vi.stubEnv('TZ', 'America/Los_Angeles')
  const pending = deferredResponse()
  const requests: Request[] = []
  const user = await selectSlotAndFillGuest(clientWith({ bookingResponse: () => pending.promise, requests }))
  await user.dblClick(screen.getByRole('button', { name: 'Записаться' }))
  expect(screen.getByRole('button', { name: 'Запись…' })).toBeDisabled()
  expect(screen.getByRole('button', { name: /Консультация/ })).toBeDisabled()
  expect(screen.getByLabelText('Дата в часовом поясе календаря')).toBeDisabled()
  expect(screen.getByRole('button', { name: /02:00 UTC\+01:00/ })).toBeDisabled()
  expect(screen.queryByText('Запись подтверждена')).not.toBeInTheDocument()
  const posts = requests.filter((request) => request.method === 'POST')
  expect(posts).toHaveLength(1)
  expect(await posts[0].json()).toEqual({
    eventTypeId: 'intro', start: slots[0].start, name: 'Guest Person', email: 'guest@example.test',
  })
  pending.resolve(json(slots[1], 201))
  expect(await screen.findByText('Запись подтверждена')).toBeInTheDocument()
  expect(screen.getByText('25.10.2026, 02:00 UTC+01:00 — 25.10.2026, 02:30 UTC+01:00')).toBeInTheDocument()
  expect(screen.getByText('Часовой пояс календаря: Europe/Berlin')).toBeInTheDocument()
  expect(document.body).not.toHaveTextContent('Guest Person')
  expect(document.body).not.toHaveTextContent('guest@example.test')
  expect(document.body).not.toHaveTextContent('intro')
  expect(document.body).not.toHaveTextContent('consultation')
  expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
})

test('calendar date precedes time, filters by calendar-local date and clears guest state on date changes', async () => {
  vi.stubEnv('TZ', 'Asia/Tokyo')
  const user = userEvent.setup()
  const requests: Request[] = []
  const next = { start: '2026-10-26T00:30:00Z', end: '2026-10-26T01:15:00Z' }
  render(<BookingPage client={clientWith({
    requests,
    calendarResponse: async () => json({ timezone: 'America/Los_Angeles' }),
    slotsResponse: async () => json([slots[0], next,
      { start: '2026-10-28T00:30:00Z', end: '2026-10-28T01:15:00Z' }]),
  })} />)
  await user.click(await screen.findByRole('button', { name: /Знакомство/ }))
  const date = await screen.findByLabelText('Дата в часовом поясе календаря')
  expect(date).toHaveAttribute('type', 'date')
  expect(date).toHaveValue('')
  expect(screen.queryByRole('button', { name: /UTC[+-]/ })).not.toBeInTheDocument()
  await selectDate('2026-10-24')
  const early = await screen.findByRole('button', { name: /24.10.2026, 17:00/ })
  expect(screen.queryByRole('button', { name: /25.10.2026, 17:30/ })).not.toBeInTheDocument()
  await user.click(early)
  await user.type(screen.getByLabelText('Имя'), 'Discarded Guest')
  await selectDate('2026-10-25')
  expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: /24.10.2026, 17:00/ })).not.toBeInTheDocument()
  const later = screen.getByRole('button', { name: /25.10.2026, 17:30/ })
  expect(later).toHaveAttribute('aria-pressed', 'false')
  await user.click(later)
  expect(screen.getByLabelText('Имя')).toHaveValue('')
  await selectDate('2026-10-26')
  expect(screen.getByText('На выбранную дату нет доступного времени.')).toBeInTheDocument()
  expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
  await selectDate('')
  expect(screen.queryByRole('button', { name: /UTC[+-]/ })).not.toBeInTheDocument()
  expect(requests).toHaveLength(3)
  expect(requests.every((request) => request.method === 'GET')).toBe(true)
})

test('shows separate CalendarInfo and EventTypes loading states', () => {
  const client = createApiClient('http://api.example.test', () => new Promise<Response>(() => {}))
  render(<BookingPage client={client} />)

  expect(screen.getByText('Загрузка календаря…')).toBeInTheDocument()
  expect(screen.getByText('Загрузка типов событий…')).toBeInTheDocument()
  expect(screen.queryByText('Нет типов событий для записи.')).not.toBeInTheDocument()
})

test('validates required Guest fields before submitting through the typed client', async () => {
  const user = userEvent.setup()
  const requests: Request[] = []
  render(<BookingPage client={clientWith({ requests })} />)
  await user.click(await screen.findByRole('button', { name: /Знакомство/ }))
  await selectDate()
  await user.click(await screen.findByRole('button', { name: /02:00 UTC\+02:00/ }))
  expect(screen.getByRole('textbox', { name: 'Имя' })).toBeRequired()
  expect(screen.getByRole('textbox', { name: 'Email' })).toBeRequired()
  await user.click(screen.getByRole('button', { name: 'Записаться' }))
  expect(screen.getByText('Введите имя.')).toBeInTheDocument()
  expect(screen.getByText('Введите email.')).toBeInTheDocument()
  await user.type(screen.getByRole('textbox', { name: 'Имя' }), '   ')
  await user.type(screen.getByRole('textbox', { name: 'Email' }), 'not-an-email')
  await user.click(screen.getByRole('button', { name: 'Записаться' }))
  expect(screen.getByText('Введите имя.')).toBeInTheDocument()
  expect(screen.getByText('Введите корректный email.')).toBeInTheDocument()
  expect(requests.filter((request) => request.method === 'POST')).toHaveLength(0)
})

test.each([
  ['\u00a0guest@example.test\u00a0', true],
  ['\u00a0not-an-email\u00a0', false],
  ['\u00a0\u00a0', false],
])('validates trimmed pasted email %j before POST', async (email, valid) => {
  const requests: Request[] = []
  const user = await selectSlotAndFillGuest(clientWith({ requests }))
  const input = screen.getByRole('textbox', { name: 'Email' })
  await user.clear(input)
  await user.paste(email)
  await user.click(screen.getByRole('button', { name: 'Записаться' }))

  const posts = requests.filter((request) => request.method === 'POST')
  expect(posts).toHaveLength(valid ? 1 : 0)
  if (valid) {
    expect(await posts[0].json()).toEqual({
      eventTypeId: 'intro', start: slots[0].start, name: 'Guest Person', email: email.trim(),
    })
    expect(await screen.findByText('Запись подтверждена')).toBeInTheDocument()
  } else {
    expect(screen.getByRole('alert')).toHaveTextContent(email.trim() ? 'Введите корректный email.' : 'Введите email.')
  }
})

test('renders all EventTypes with optional descriptions and duration', async () => {
  render(<BookingPage client={clientWith()} />)

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
  render(<BookingPage client={clientWith({ eventTypesResponse: response })} />)

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
  const requests: Request[] = []
  const client = clientWith({ slotsResponse: () => pending.promise, requests })
  render(<BookingPage client={client} />)
  const intro = await screen.findByRole('button', { name: /Знакомство/ })
  expect(requests.map((request) => new URL(request.url).pathname)).not.toContain('/api/event-types/intro/slots')

  await user.click(intro)

  expect(intro).toHaveAttribute('aria-pressed', 'true')
  expect(screen.getByRole('button', { name: /Консультация/ })).toHaveAttribute('aria-pressed', 'false')
  expect(screen.getByText('Загрузка доступного времени…')).toBeInTheDocument()
  await waitFor(() => expect(requests.map((request) => new URL(request.url).pathname)).toContain('/api/event-types/intro/slots'))
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
  const client = clientWith({ slotsResponse: response })
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
  const client = clientWith({ requests })
  render(<BookingPage client={client} />)
  await user.click(await screen.findByRole('button', { name: /Знакомство/ }))

  await selectDate()
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
  const client = clientWith({ calendarResponse: response })
  render(<BookingPage client={client} />)
  expect(await screen.findByRole('alert')).toHaveTextContent(message)
  await user.click(await screen.findByRole('button', { name: /Знакомство/ }))
  await waitFor(() => expect(screen.queryByText('Загрузка доступного времени…')).not.toBeInTheDocument())
  expect(screen.queryByRole('button', { name: /02:00/ })).not.toBeInTheDocument()
})

test('waits for calendar metadata before displaying loaded Slot dates', async () => {
  const pending = deferredResponse()
  const user = userEvent.setup()
  const client = clientWith({ calendarResponse: () => pending.promise })
  render(<BookingPage client={client} />)
  await user.click(await screen.findByRole('button', { name: /Знакомство/ }))
  await waitFor(() => expect(screen.queryByText('Загрузка доступного времени…')).not.toBeInTheDocument())
  expect(screen.getByText('Загрузка календаря…')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: /02:00/ })).not.toBeInTheDocument()
  pending.resolve(json({ timezone: 'Europe/Berlin' }))
  await selectDate()
  expect(await screen.findByRole('button', { name: /02:00 UTC\+02:00/ })).toBeInTheDocument()
})

test('switching EventTypes clears Slot selection and ignores late availability responses', async () => {
  const user = userEvent.setup()
  const pending = deferredResponse()
  const client = clientWith({
    slotsResponse: async (path) => path === '/api/event-types/consultation/slots' ? pending.promise : json(slots),
  })
  render(<BookingPage client={client} />)
  await user.click(await screen.findByRole('button', { name: /Знакомство/ }))
  await selectDate()
  const early = await screen.findByRole('button', { name: /02:00 UTC\+02:00/ })
  await user.click(early)
  await user.click(screen.getByRole('button', { name: /Знакомство/ }))
  expect(early).toHaveAttribute('aria-pressed', 'true')

  await user.click(screen.getByRole('button', { name: /Консультация/ }))
  expect(screen.queryByRole('button', { name: /02:00/ })).not.toBeInTheDocument()
  expect(screen.getByText('Загрузка доступного времени…')).toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: /Знакомство/ }))
  expect(await screen.findByLabelText('Дата в часовом поясе календаря')).toHaveValue('')
  await selectDate()
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
  const client = clientWith({
    calendarResponse: async () => json({ timezone }),
    slotsResponse: async () => json([slots[0]]),
  })
  render(<BookingPage client={client} />)
  await user.click(await screen.findByRole('button', { name: /Знакомство/ }))

  await selectDate(timezone === 'America/Los_Angeles' ? '2026-10-24' : '2026-10-25')
  expect(await screen.findByRole('button', { name: label })).toBeInTheDocument()
  expect(screen.getByText(`Часовой пояс календаря: ${timezone}`)).toBeInTheDocument()
})
