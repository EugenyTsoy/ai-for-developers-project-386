import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, expect, test } from 'vitest'
import App from './App'

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
  expect(screen.getByText('Фиксированные 30-минутные слоты с 09:00 до 18:00.')).toBeInTheDocument()
  expect(screen.getByText('Проверка конфликта при бронировании.')).toBeInTheDocument()
  expect(screen.getByText('Просмотр предстоящих событий в отдельном разделе.')).toBeInTheDocument()
})

test.each(['шапка', 'основное действие'])('переход к записи через %s и возврат на главную', async (source) => {
  const user = userEvent.setup()
  render(<App />)
  const region = source === 'шапка' ? screen.getByRole('navigation') : screen.getByRole('main')

  await user.click(within(region).getByRole('link', { name: 'Записаться' }))

  expect(window.location.pathname).toBe('/booking')
  expect(screen.getByRole('heading', { name: 'Запись на звонок', level: 1 })).toBeInTheDocument()
  expect(screen.getByText('Раздел пока не реализован')).toBeInTheDocument()
  expect(screen.queryByRole('heading', { name: 'Calendar' })).not.toBeInTheDocument()
  expect(within(screen.getByRole('navigation')).getByRole('link', { name: 'Предстоящие события' })).toBeInTheDocument()

  await user.click(screen.getByRole('link', { name: 'Calendar' }))

  expect(window.location.pathname).toBe('/')
  expect(screen.getByRole('heading', { name: 'Calendar', level: 1 })).toBeInTheDocument()
  expect(screen.queryByText('Раздел пока не реализован')).not.toBeInTheDocument()
})

test('переход к предстоящим событиям и возврат через общую шапку', async () => {
  const user = userEvent.setup()
  render(<App />)

  await user.click(screen.getByRole('link', { name: 'Предстоящие события' }))

  expect(window.location.pathname).toBe('/events')
  expect(screen.getByRole('heading', { name: 'Предстоящие события', level: 1 })).toBeInTheDocument()
  expect(screen.getByText('Раздел пока не реализован')).toBeInTheDocument()
  expect(screen.queryByRole('heading', { name: 'Calendar' })).not.toBeInTheDocument()
  expect(within(screen.getByRole('navigation')).getByRole('link', { name: 'Записаться' })).toBeInTheDocument()

  await user.click(screen.getByRole('link', { name: 'Calendar' }))

  expect(window.location.pathname).toBe('/')
  expect(screen.getByRole('heading', { name: 'Calendar', level: 1 })).toBeInTheDocument()
  expect(screen.queryByText('Раздел пока не реализован')).not.toBeInTheDocument()
})
