import { configure, fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, test } from 'vitest'
import App from '../App'
import { createApiClient, toApiResult } from '../api/client'
import { calendarTimeFormatter } from '../calendarTimeFormatter'

// Opt-in: exercises a running Vite + API stack or the production container.
const origin = import.meta.env.VITE_RUNTIME_WEB_URL || 'http://localhost:49567'
const client = createApiClient(origin)
configure({ asyncUtilTimeout: 10000 })

async function selectDate(start: string, timezone: string) {
  const date = new Intl.DateTimeFormat('en-CA', {
    timeZone: timezone, year: 'numeric', month: '2-digit', day: '2-digit',
  }).format(new Date(start))
  fireEvent.change(await screen.findByLabelText('Дата в часовом поясе календаря'), { target: { value: date } })
}

test('client routes survive direct requests to the web host', async () => {
  for (const path of ['/', '/booking', '/events']) {
    const response = await fetch(new URL(path, origin))
    expect(response.status).toBe(200)
    expect(response.headers.get('content-type')).toContain('text/html')
    expect(await response.text()).toContain('<div id="root"></div>')
  }
})

test('owner creates an EventType, guest books a Slot, and both views read shared saved state', async () => {
  const metadata = await toApiResult(client.GET('/api/calendar'))
  expect(metadata.ok, 'Start the local stack or production container before running test:runtime').toBe(true)
  if (!metadata.ok) throw new Error('Calendar API unavailable')
  const format = calendarTimeFormatter(metadata.value.timezone)
  const name = `Runtime call ${crypto.randomUUID()}`
  const user = userEvent.setup()
  window.history.replaceState(null, '', '/events')
  render(<App client={client} />)

  await user.type(screen.getByLabelText('Название'), name)
  await user.type(screen.getByLabelText('Описание (необязательно)'), 'Real API smoke check')
  await user.type(screen.getByLabelText('Длительность в минутах'), '45')
  await user.click(screen.getByRole('button', { name: 'Создать тип события' }))
  expect(await screen.findByText(`Тип события «${name}» создан.`)).toBeInTheDocument()

  await user.click(screen.getByRole('link', { name: 'Записаться' }))
  await user.click(await screen.findByRole('button', { name }))
  const types = await toApiResult(client.GET('/api/event-types'))
  if (!types.ok) throw new Error('Cannot read saved EventType')
  const eventType = types.value.find((type) => type.name === name)
  expect(eventType).toMatchObject({ description: 'Real API smoke check', durationMinutes: 45 })
  if (!eventType) throw new Error('Created EventType missing')
  const readSlots = () => toApiResult(client.GET('/api/event-types/{eventTypeId}/slots', {
    params: { path: { eventTypeId: eventType.id } },
  }))
  const availability = await readSlots()
  if (!availability.ok) throw new Error('Cannot read availability')
  // Avoid racing a slot whose start is about to become now. Uses the live calendar configuration.
  const slot = availability.value.find((candidate) => Date.parse(candidate.start) > Date.now() + 3600000)
  if (!slot) throw new Error('Runtime check needs a free 45-minute slot more than one hour in the future')
  const interval = `${format(slot.start)} — ${format(slot.end)}`
  await selectDate(slot.start, metadata.value.timezone)
  await user.click(await screen.findByRole('button', { name: interval }))
  await user.type(screen.getByLabelText('Имя'), 'Runtime Guest')
  await user.click(screen.getByLabelText('Email'))
  await user.paste('\u00a0runtime@example.com\u00a0')
  await user.click(screen.getByRole('button', { name: 'Записаться' }))

  expect(await screen.findByRole('heading', { name: 'Запись подтверждена' })).toBeInTheDocument()
  expect(screen.getByText(interval)).toBeInTheDocument()
  expect(screen.getByText(`Часовой пояс календаря: ${metadata.value.timezone}`)).toBeInTheDocument()
  expect(screen.queryByText('Runtime Guest')).not.toBeInTheDocument()
  expect(screen.queryByText('runtime@example.com')).not.toBeInTheDocument()

  await user.click(screen.getByRole('link', { name: 'Предстоящие события' }))
  const saved = (await screen.findByRole('heading', { name })).closest('li')!
  expect(within(saved).getByText('Runtime Guest')).toBeInTheDocument()
  expect(within(saved).getByText('runtime@example.com')).toBeInTheDocument()
  expect(within(saved).getByText(interval)).toBeInTheDocument()

  const remaining = await readSlots()
  if (!remaining.ok) throw new Error('Cannot refresh availability')
  expect(remaining.value.some((candidate) => candidate.start === slot.start)).toBe(false)
  await user.click(screen.getByRole('link', { name: 'Записаться' }))
  await user.click(await screen.findByRole('button', { name }))
  await selectDate(slot.start, metadata.value.timezone)
  await screen.findByText('Выбор времени не резервирует слот.')
  expect(screen.queryByRole('button', { name: interval })).not.toBeInTheDocument()

  await user.click(screen.getByRole('link', { name: 'Calendar' }))
  expect(window.location.pathname).toBe('/')
  expect(screen.getByRole('heading', { name: 'Calendar', level: 1 })).toBeInTheDocument()
})

test('a slot booked after UI selection produces a conflict and refreshed availability', async () => {
  const name = `Runtime conflict ${crypto.randomUUID()}`
  const created = await toApiResult(client.POST('/api/event-types', {
    body: { name, durationMinutes: 30 },
  }))
  const metadata = await toApiResult(client.GET('/api/calendar'))
  if (!created.ok || !metadata.ok) throw new Error('Runtime API unavailable')
  const availability = await toApiResult(client.GET('/api/event-types/{eventTypeId}/slots', {
    params: { path: { eventTypeId: created.value.id } },
  }))
  if (!availability.ok) throw new Error('Cannot read availability')
  const slot = availability.value.find((candidate) => Date.parse(candidate.start) > Date.now() + 3600000)
  if (!slot) throw new Error('Runtime conflict check needs a free 30-minute slot')
  const format = calendarTimeFormatter(metadata.value.timezone)
  const interval = `${format(slot.start)} — ${format(slot.end)}`
  const user = userEvent.setup()
  window.history.replaceState(null, '', '/booking')
  render(<App client={client} />)
  await user.click(await screen.findByRole('button', { name }))
  await selectDate(slot.start, metadata.value.timezone)
  await user.click(await screen.findByRole('button', { name: interval }))
  await user.type(screen.getByLabelText('Имя'), 'Losing guest')
  await user.type(screen.getByLabelText('Email'), 'losing@example.com')

  const winner = await toApiResult(client.POST('/api/bookings', {
    body: { eventTypeId: created.value.id, start: slot.start, name: 'Winning guest', email: 'winning@example.com' },
  }))
  expect(winner.ok).toBe(true)
  await user.click(screen.getByRole('button', { name: 'Записаться' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Слот больше недоступен, выберите другой.')
  await screen.findByText('Выбор времени не резервирует слот.')
  expect(screen.queryByRole('button', { name: interval })).not.toBeInTheDocument()
  expect(screen.queryByRole('heading', { name: 'Запись подтверждена' })).not.toBeInTheDocument()
})
