import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, expect, test } from 'vitest'
import App from './App'
import { createApiClient } from './api/client'

const client = createApiClient('http://api.example.test', async (request) => new Response(JSON.stringify(
  new URL((request as Request).url).pathname === '/api/calendar' ? { timezone: 'Europe/Berlin' } : [],
), {
  headers: { 'content-type': 'application/json' },
}))

beforeEach(() => {
  window.history.replaceState(null, '', '/')
})

test('общая шапка и основное действие ведут на согласованные адреса', () => {
  render(<App />)

  expect(screen.getByRole('link', { name: 'Calendar' })).toHaveAttribute('href', '/')
  const bookingLinks = screen.getAllByRole('link', { name: 'Записаться' })
  expect(bookingLinks).toHaveLength(2)
  for (const link of bookingLinks) {
    expect(link).toHaveAttribute('href', '/booking')
  }
  expect(within(screen.getByRole('navigation')).getByRole('link', { name: 'Предстоящие события' })).toHaveAttribute('href', '/events')
})

test('главная объясняет сервис и описывает возможности слотов и записей', () => {
  render(<App />)

  expect(screen.getByRole('heading', { name: 'Calendar', level: 1 })).toBeInTheDocument()
  expect(screen.getByText('БЫСТРАЯ ЗАПИСЬ НА ЗВОНОК')).toBeInTheDocument()
  expect(screen.getByText('Один экран, понятные слоты, быстрая бронь. Выберите время и запишитесь на звонок без лишних шагов.')).toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Что доступно прямо сейчас' })).toBeInTheDocument()
  expect(screen.getByText('Длительность звонка зависит от выбранного типа события.')).toBeInTheDocument()
  expect(screen.getByText('Проверка конфликта при бронировании.')).toBeInTheDocument()
  expect(screen.getByText('Просмотр предстоящих событий в отдельном разделе.')).toBeInTheDocument()
})

test.each(['шапка', 'основное действие'])('переход к записи через %s и возврат на главную', async (source) => {
  const user = userEvent.setup()
  render(<App client={client} />)
  const region = source === 'шапка' ? screen.getByRole('navigation') : screen.getByRole('main')

  await user.click(within(region).getByRole('link', { name: 'Записаться' }))

  expect(window.location.pathname).toBe('/booking')
  expect(screen.getByRole('heading', { name: 'Запись на звонок', level: 1 })).toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Выберите тип события' })).toBeInTheDocument()
  expect(await screen.findByText('Нет типов событий для записи.')).toBeInTheDocument()
  expect(screen.queryByRole('heading', { name: 'Calendar' })).not.toBeInTheDocument()
  expect(within(screen.getByRole('navigation')).getByRole('link', { name: 'Предстоящие события' })).toBeInTheDocument()

  await user.click(screen.getByRole('link', { name: 'Calendar' }))

  expect(window.location.pathname).toBe('/')
  expect(screen.getByRole('heading', { name: 'Calendar', level: 1 })).toBeInTheDocument()
})

test('переход к предстоящим событиям и возврат через общую шапку', async () => {
  const user = userEvent.setup()
  render(<App client={client} />)

  await user.click(screen.getByRole('link', { name: 'Предстоящие события' }))

  expect(window.location.pathname).toBe('/events')
  expect(screen.getByRole('heading', { name: 'Предстоящие события', level: 1 })).toBeInTheDocument()
  expect(screen.getByRole('form', { name: 'Создать тип события' })).toBeInTheDocument()
  expect(await screen.findByText('Нет предстоящих записей.')).toBeInTheDocument()
  expect(screen.queryByRole('heading', { name: 'Calendar' })).not.toBeInTheDocument()
  expect(within(screen.getByRole('navigation')).getByRole('link', { name: 'Записаться' })).toBeInTheDocument()

  await user.click(screen.getByRole('link', { name: 'Calendar' }))

  expect(window.location.pathname).toBe('/')
  expect(screen.getByRole('heading', { name: 'Calendar', level: 1 })).toBeInTheDocument()
})

test.each(['/booking', '/events'])('прямое открытие %s показывает реальную страницу и общую шапку', async (path) => {
  window.history.replaceState(null, '', path)
  const user = userEvent.setup()
  render(<App client={client} />)

  expect(screen.getByRole('link', { name: 'Calendar' })).toHaveAttribute('href', '/')
  expect(screen.getByRole('navigation', { name: 'Основная навигация' })).toBeInTheDocument()
  if (path === '/booking') {
    expect(await screen.findByText('Нет типов событий для записи.')).toBeInTheDocument()
    await user.click(screen.getByRole('link', { name: 'Предстоящие события' }))
    expect(await screen.findByText('Нет предстоящих записей.')).toBeInTheDocument()
    expect(screen.getByRole('form', { name: 'Создать тип события' })).toBeInTheDocument()
  } else {
    expect(await screen.findByText('Нет предстоящих записей.')).toBeInTheDocument()
    await user.click(screen.getByRole('link', { name: 'Записаться' }))
    expect(await screen.findByText('Нет типов событий для записи.')).toBeInTheDocument()
  }
  await user.click(screen.getByRole('link', { name: 'Calendar' }))
  expect(window.location.pathname).toBe('/')
  expect(screen.getByRole('heading', { name: 'Calendar', level: 1 })).toBeInTheDocument()
})
