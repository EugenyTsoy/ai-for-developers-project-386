import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, test, vi } from 'vitest'
import { createApiClient } from './api/client'
import CreateEventTypeForm from './CreateEventTypeForm'

function json(body: unknown, status = 201) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': status >= 400 ? 'application/problem+json' : 'application/json' },
  })
}

test('creates an EventType with optional description using the typed client', async () => {
  const user = userEvent.setup()
  const fetchImpl = vi.fn<typeof fetch>(async (input) => {
    const request = input as Request
    expect(request.method).toBe('POST')
    expect(new URL(request.url).pathname).toBe('/api/event-types')
    expect(await request.json()).toEqual({ name: 'Консультация', description: 'Разбор проекта', durationMinutes: 45 })
    return json({ id: 'server-id', name: 'Консультация', description: 'Разбор проекта', durationMinutes: 45 })
  })
  render(<CreateEventTypeForm client={createApiClient('http://api.example.test', fetchImpl)} />)
  await user.type(screen.getByLabelText('Название'), 'Консультация')
  await user.type(screen.getByLabelText('Описание (необязательно)'), 'Разбор проекта')
  await user.type(screen.getByLabelText('Длительность в минутах'), '45')
  await user.click(screen.getByRole('button', { name: 'Создать тип события' }))
  expect(await screen.findByRole('status')).toHaveTextContent('Тип события «Консультация» создан.')
  expect(fetchImpl).toHaveBeenCalledTimes(1)
})

test.each(['', '0', '-1', '1.5', '2147483648'])('rejects invalid duration %s and a blank name without a request', async (duration) => {
  const user = userEvent.setup()
  const fetchImpl = vi.fn<typeof fetch>()
  render(<CreateEventTypeForm client={createApiClient('http://api.example.test', fetchImpl)} />)
  await user.type(screen.getByLabelText('Название'), '   ')
  if (duration) await user.type(screen.getByLabelText('Длительность в минутах'), duration)
  await user.click(screen.getByRole('button', { name: 'Создать тип события' }))
  expect(screen.getByText('Введите название.')).toHaveAttribute('role', 'alert')
  expect(screen.getByLabelText('Название')).toHaveAttribute('aria-invalid', 'true')
  expect(screen.getByText('Введите положительную целую длительность в пределах int32.')).toHaveAttribute('role', 'alert')
  expect(screen.getByLabelText('Длительность в минутах')).toHaveAttribute('aria-invalid', 'true')
  expect(fetchImpl).not.toHaveBeenCalled()
})

test.each([1, 1000, 2147483647])('allows duration %s without an optional description and permits duplicate names', async (duration) => {
  const user = userEvent.setup()
  const fetchImpl = vi.fn<typeof fetch>(async (input) => {
    expect(await (input as Request).json()).toEqual({ name: 'Звонок', durationMinutes: duration })
    return json({ id: 'server-id', name: 'Звонок', durationMinutes: duration })
  })
  render(<CreateEventTypeForm client={createApiClient('http://api.example.test', fetchImpl)} />)
  await user.type(screen.getByLabelText('Название'), 'Звонок')
  await user.type(screen.getByLabelText('Длительность в минутах'), String(duration))
  await user.click(screen.getByRole('button', { name: 'Создать тип события' }))
  expect(await screen.findByRole('status')).toHaveTextContent('Тип события «Звонок» создан.')
  await user.click(screen.getByRole('button', { name: 'Создать тип события' }))
  expect(await screen.findByRole('status')).toHaveTextContent('Тип события «Звонок» создан.')
  expect(fetchImpl).toHaveBeenCalledTimes(2)
})

test('waits for the POST response before confirming and prevents duplicate pending submits', async () => {
  const user = userEvent.setup()
  let resolve!: (response: Response) => void
  const response = new Promise<Response>((complete) => { resolve = complete })
  const fetchImpl = vi.fn<typeof fetch>(() => response)
  render(<CreateEventTypeForm client={createApiClient('http://api.example.test', fetchImpl)} />)
  await user.type(screen.getByLabelText('Название'), 'Звонок')
  await user.type(screen.getByLabelText('Длительность в минутах'), '30')
  await user.dblClick(screen.getByRole('button', { name: 'Создать тип события' }))
  expect(screen.getByRole('status')).toHaveTextContent('Сохранение типа события…')
  expect(screen.queryByText('Тип события «Звонок» создан.')).not.toBeInTheDocument()
  expect(screen.getByLabelText('Название')).toBeDisabled()
  expect(screen.getByLabelText('Описание (необязательно)')).toBeDisabled()
  expect(screen.getByLabelText('Длительность в минутах')).toBeDisabled()
  expect(screen.getByRole('button')).toBeDisabled()
  expect(fetchImpl).toHaveBeenCalledTimes(1)
  resolve(json({ id: 'server-id', name: 'Звонок', durationMinutes: 30 }))
  expect(await screen.findByText('Тип события «Звонок» создан.')).toHaveAttribute('role', 'status')
  expect(screen.getByRole('button')).toBeEnabled()
})

test.each([
  [400, 'Не удалось создать тип события (HTTP 400).'],
  [503, 'Не удалось создать тип события (HTTP 503).'],
  [null, 'Нет соединения: подтверждение создания типа события не получено.'],
])('handles failure %s without confirmation or automatic retry, retaining input for a manual retry', async (status, message) => {
  const user = userEvent.setup()
  const fetchImpl = vi.fn<typeof fetch>(async () => {
    if (status === null) throw new TypeError('offline')
    return json({ status, title: 'Failure', detail: 'Do not display me' }, status)
  })
  render(<CreateEventTypeForm client={createApiClient('http://api.example.test', fetchImpl)} />)
  await user.type(screen.getByLabelText('Название'), 'Звонок')
  await user.type(screen.getByLabelText('Описание (необязательно)'), 'Описание')
  await user.type(screen.getByLabelText('Длительность в минутах'), '30')
  await user.click(screen.getByRole('button', { name: 'Создать тип события' }))
  expect(await screen.findByRole('alert')).toHaveTextContent(message)
  expect(screen.queryByRole('status')).not.toBeInTheDocument()
  expect(screen.queryByText('Do not display me')).not.toBeInTheDocument()
  expect(screen.getByLabelText('Название')).toHaveValue('Звонок')
  expect(screen.getByLabelText('Описание (необязательно)')).toHaveValue('Описание')
  expect(screen.getByLabelText('Длительность в минутах')).toHaveValue(30)
  expect(screen.getByRole('button')).toBeEnabled()
  expect(fetchImpl).toHaveBeenCalledTimes(1)
  fetchImpl.mockImplementation(async () => json({ id: 'server-id', name: 'Звонок', durationMinutes: 30 }))
  await user.click(screen.getByRole('button', { name: 'Создать тип события' }))
  expect(await screen.findByRole('status')).toHaveTextContent('Тип события «Звонок» создан.')
  expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  expect(fetchImpl).toHaveBeenCalledTimes(2)
})

test('a response after leaving the form cannot confirm a newly mounted form', async () => {
  const user = userEvent.setup()
  let resolve!: (response: Response) => void
  const response = new Promise<Response>((complete) => { resolve = complete })
  const client = createApiClient('http://api.example.test', () => response)
  const first = render(<CreateEventTypeForm client={client} />)
  await user.type(screen.getByLabelText('Название'), 'Звонок')
  await user.type(screen.getByLabelText('Длительность в минутах'), '30')
  await user.click(screen.getByRole('button'))
  first.unmount()
  render(<CreateEventTypeForm client={client} />)
  await act(async () => { resolve(json({ id: 'server-id', name: 'Звонок', durationMinutes: 30 })) })
  expect(screen.queryByRole('status')).not.toBeInTheDocument()
  expect(screen.getByRole('button')).toBeEnabled()
})
