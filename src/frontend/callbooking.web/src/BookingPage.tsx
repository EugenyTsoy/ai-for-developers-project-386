import { useEffect, useRef, useState } from 'react'
import { apiClient, toApiResult } from './api/client'
import type { ApiClient, ApiResult } from './api/client'
import type { components } from './api/generated/schema'
import { calendarTimeFormatter } from './calendarTimeFormatter'
import RequestFailure from './RequestFailure'

type CalendarInfo = components['schemas']['CalendarInfo']
type EventType = components['schemas']['EventType']
type Slot = components['schemas']['Slot']
type BookingConfirmation = components['schemas']['BookingConfirmation']

function calendarDate(instant: string, timezone: string) {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: timezone, year: 'numeric', month: '2-digit', day: '2-digit',
  }).format(new Date(instant))
}

function GuestForm({ client, eventTypeId, slot, pending, onPending, onResult }: {
  client: ApiClient
  eventTypeId: string
  slot: Slot
  pending: boolean
  onPending: () => void
  onResult: (result: ApiResult<BookingConfirmation>) => void
}) {
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [errors, setErrors] = useState({ name: '', email: '' })
  const active = useRef(false)
  const inFlight = useRef(false)
  useEffect(() => {
    active.current = true
    return () => { active.current = false }
  }, [])

  return (
    <form className="guest-form" noValidate onSubmit={(event) => {
      event.preventDefault()
      if (inFlight.current) return
      const trimmedName = name.trim()
      const trimmedEmail = email.trim()
      const emailValidation = document.createElement('input')
      emailValidation.type = 'email'
      emailValidation.value = trimmedEmail
      const validation = {
        name: trimmedName ? '' : 'Введите имя.',
        email: !trimmedEmail ? 'Введите email.'
          : emailValidation.validity.typeMismatch
            ? 'Введите корректный email.' : '',
      }
      setErrors(validation)
      if (validation.name || validation.email) return
      inFlight.current = true
      onPending()
      void toApiResult(client.POST('/api/bookings', {
        body: { eventTypeId, start: slot.start, name: trimmedName, email: trimmedEmail },
      })).then((result) => {
        if (!active.current) return
        inFlight.current = false
        onResult(result)
      })
    }}>
      <h2>Ваши данные</h2>
      <label htmlFor="guest-name">Имя</label>
      <input id="guest-name" autoComplete="name" required value={name} disabled={pending}
        aria-invalid={!!errors.name} aria-describedby={errors.name ? 'name-error' : undefined}
        onChange={(event) => setName(event.target.value)} />
      {errors.name && <p id="name-error" role="alert">{errors.name}</p>}
      <label htmlFor="guest-email">Email</label>
      <input id="guest-email" type="email" autoComplete="email" required value={email} disabled={pending}
        aria-invalid={!!errors.email} aria-describedby={errors.email ? 'email-error' : undefined}
        onChange={(event) => setEmail(event.target.value)} />
      {errors.email && <p id="email-error" role="alert">{errors.email}</p>}
      <button type="submit" disabled={pending}>{pending ? 'Запись…' : 'Записаться'}</button>
      {pending && <p role="status">Отправка записи… Подтверждение ещё не получено.</p>}
    </form>
  )
}

function SlotSelection({ client, eventType, timezone, selectedDate, onDateChange, selectedSlot, onSelect, disabled }: {
  client: ApiClient
  eventType: EventType
  timezone?: string
  selectedDate: string
  onDateChange: (date: string) => void
  selectedSlot: Slot | null
  onSelect: (slot: Slot) => void
  disabled: boolean
}) {
  const [slots, setSlots] = useState<ApiResult<Slot[]> | null>(null)
  useEffect(() => {
    let active = true
    void toApiResult(client.GET('/api/event-types/{eventTypeId}/slots', {
      params: { path: { eventTypeId: eventType.id } },
    })).then((result) => {
      if (active) setSlots(result)
    })
    return () => { active = false }
  }, [client, eventType.id])

  const format = timezone ? calendarTimeFormatter(timezone) : null
  const dates = slots?.ok && timezone
    ? [...new Set(slots.value.map((slot) => calendarDate(slot.start, timezone)))].sort() : []
  const dateSlots = slots?.ok && timezone
    ? slots.value.filter((slot) => calendarDate(slot.start, timezone) === selectedDate) : []

  return (
    <section aria-labelledby="slot-title">
      <h2 id="slot-title">Выберите дату: {eventType.name}</h2>
      {!slots && <p role="status">Загрузка доступного времени…</p>}
      {slots && !slots.ok && <RequestFailure result={slots} subject="доступное время" />}
      {slots?.ok && slots.value.length === 0 && <p>Нет доступного времени для выбранного типа события.</p>}
      {slots?.ok && format && slots.value.length > 0 && (
        <>
          <label htmlFor="booking-date">Дата в часовом поясе календаря</label>
          <input id="booking-date" type="date" value={selectedDate} disabled={disabled}
            min={dates[0]} max={dates.at(-1)}
            onChange={(event) => onDateChange(event.target.value)} />
          {!selectedDate && <p>Выберите дату, чтобы увидеть доступное время.</p>}
          {selectedDate && <h3>Выберите время</h3>}
          {selectedDate && dateSlots.length === 0 && <p>На выбранную дату нет доступного времени.</p>}
          <ul className="selection-list">
            {dateSlots.map((slot) => (
              <li key={slot.start}>
                <button type="button" disabled={disabled} aria-pressed={selectedSlot?.start === slot.start}
                  onClick={() => onSelect(slot)}>
                  {format(slot.start)} — {format(slot.end)}
                </button>
              </li>
            ))}
          </ul>
          <p>Выбор времени не резервирует слот.</p>
        </>
      )}
    </section>
  )
}

export default function BookingPage({ client = apiClient }: { client?: ApiClient }) {
  const [calendar, setCalendar] = useState<ApiResult<CalendarInfo> | null>(null)
  const [eventTypes, setEventTypes] = useState<ApiResult<EventType[]> | null>(null)
  const [selectedEventType, setSelectedEventType] = useState<EventType | null>(null)
  const [selectedSlot, setSelectedSlot] = useState<Slot | null>(null)
  const [selectedDate, setSelectedDate] = useState('')
  const [pending, setPending] = useState(false)
  const [booking, setBooking] = useState<ApiResult<BookingConfirmation> | null>(null)
  const [availabilityVersion, setAvailabilityVersion] = useState(0)

  useEffect(() => {
    let active = true
    void toApiResult(client.GET('/api/calendar')).then((result) => {
      if (active) setCalendar(result)
    })
    void toApiResult(client.GET('/api/event-types')).then((result) => {
      if (active) setEventTypes(result)
    })
    return () => { active = false }
  }, [client])

  if (booking?.ok && calendar?.ok) {
    const format = calendarTimeFormatter(calendar.value.timezone)
    return (
      <section className="booking-page" aria-labelledby="confirmation-title">
        <h1 id="confirmation-title">Запись подтверждена</h1>
        <p>{format(booking.value.start)} — {format(booking.value.end)}</p>
        <p>Часовой пояс календаря: {calendar.value.timezone}</p>
      </section>
    )
  }

  return (
    <section className="booking-page">
      <h1>Запись на звонок</h1>
      {!calendar && <p role="status">Загрузка календаря…</p>}
      {calendar && !calendar.ok && <RequestFailure result={calendar} subject="календарь" />}
      {calendar?.ok && <p>Часовой пояс календаря: {calendar.value.timezone}</p>}
      <h2>Выберите тип события</h2>
      {!eventTypes && <p role="status">Загрузка типов событий…</p>}
      {eventTypes && !eventTypes.ok && <RequestFailure result={eventTypes} subject="типы событий" />}
      {eventTypes?.ok && eventTypes.value.length === 0 && <p>Нет типов событий для записи.</p>}
      {eventTypes?.ok && (
        <ul className="selection-list">
          {eventTypes.value.map((eventType) => (
            <li key={eventType.id}>
              <button type="button" disabled={pending} aria-pressed={selectedEventType?.id === eventType.id}
                onClick={() => {
                  if (selectedEventType?.id !== eventType.id) {
                    setSelectedEventType(eventType)
                    setSelectedDate('')
                    setSelectedSlot(null)
                    setBooking(null)
                  }
                }}>
                {eventType.name}
              </button>
              {eventType.description && <p>{eventType.description}</p>}
              <span>{eventType.durationMinutes} мин</span>
            </li>
          ))}
        </ul>
      )}
      {selectedEventType && (
        <SlotSelection key={`${selectedEventType.id}:${availabilityVersion}`} client={client} eventType={selectedEventType}
          timezone={calendar?.ok ? calendar.value.timezone : undefined}
          selectedDate={selectedDate} onDateChange={(date) => {
            setSelectedDate(date)
            setSelectedSlot(null)
            setBooking(null)
          }}
          disabled={pending} selectedSlot={selectedSlot} onSelect={(slot) => {
            setSelectedSlot(slot)
            setBooking(null)
          }} />
      )}
      {booking && !booking.ok && <p role="alert">
        {booking.kind === 'transport'
          ? 'Нет соединения: подтверждение записи не получено.'
          : booking.status === 409 ? 'Слот больше недоступен, выберите другой.'
          : `Не удалось создать запись (HTTP ${booking.status}).`}
      </p>}
      {booking && !booking.ok && booking.kind === 'http' && booking.status === 409 && (
        <button type="button" onClick={() => {
          setSelectedSlot(null)
          setAvailabilityVersion((version) => version + 1)
        }}>Обновить доступное время</button>
      )}
      {selectedEventType && selectedSlot && <GuestForm key={`${selectedEventType.id}:${selectedSlot.start}`}
        client={client} eventTypeId={selectedEventType.id} slot={selectedSlot} pending={pending}
        onPending={() => { setPending(true); setBooking(null) }}
        onResult={(result) => {
          setPending(false)
          setBooking(result)
          if (!result.ok && result.kind === 'http' && result.status === 409) {
            setSelectedSlot(null)
            setAvailabilityVersion((version) => version + 1)
          }
        }} />}
    </section>
  )
}
