import { useEffect, useState } from 'react'
import { apiClient, toApiResult } from './api/client'
import type { ApiClient, ApiHttpFailure, ApiResult, ApiTransportFailure } from './api/client'
import type { components } from './api/generated/schema'

type CalendarInfo = components['schemas']['CalendarInfo']
type EventType = components['schemas']['EventType']
type Slot = components['schemas']['Slot']

function RequestFailure({ result, subject }: {
  result: ApiHttpFailure | ApiTransportFailure
  subject: string
}) {
  return (
    <p role="alert">
      {result.kind === 'http'
        ? `Не удалось загрузить ${subject} (HTTP ${result.status}).`
        : `Нет соединения: не удалось загрузить ${subject}.`}
    </p>
  )
}

function SlotSelection({ client, eventType, timezone, selectedSlot, onSelect }: {
  client: ApiClient
  eventType: EventType
  timezone?: string
  selectedSlot: Slot | null
  onSelect: (slot: Slot) => void
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

  const formatter = timezone ? new Intl.DateTimeFormat('ru-RU', {
    timeZone: timezone,
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
    timeZoneName: 'longOffset',
  }) : null
  const format = (instant: string) => formatter!.format(new Date(instant)).replace('GMT', 'UTC')

  return (
    <section aria-labelledby="slot-title">
      <h2 id="slot-title">Выберите время: {eventType.name}</h2>
      {!slots && <p role="status">Загрузка доступного времени…</p>}
      {slots && !slots.ok && <RequestFailure result={slots} subject="доступное время" />}
      {slots?.ok && slots.value.length === 0 && <p>Нет доступного времени для выбранного типа события.</p>}
      {slots?.ok && formatter && slots.value.length > 0 && (
        <>
          <ul className="selection-list">
            {slots.value.map((slot) => (
              <li key={slot.start}>
                <button type="button" aria-pressed={selectedSlot?.start === slot.start}
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
              <button type="button" aria-pressed={selectedEventType?.id === eventType.id}
                onClick={() => {
                  if (selectedEventType?.id !== eventType.id) {
                    setSelectedEventType(eventType)
                    setSelectedSlot(null)
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
        <SlotSelection key={selectedEventType.id} client={client} eventType={selectedEventType}
          timezone={calendar?.ok ? calendar.value.timezone : undefined}
          selectedSlot={selectedSlot} onSelect={setSelectedSlot} />
      )}
    </section>
  )
}
