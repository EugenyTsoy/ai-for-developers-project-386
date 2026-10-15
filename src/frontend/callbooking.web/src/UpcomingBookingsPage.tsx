import { useEffect, useState } from 'react'
import { apiClient, toApiResult } from './api/client'
import type { ApiClient, ApiResult } from './api/client'
import type { components } from './api/generated/schema'
import { calendarTimeFormatter } from './calendarTimeFormatter'
import RequestFailure from './RequestFailure'
import CreateEventTypeForm from './CreateEventTypeForm'

type UpcomingBooking = components['schemas']['UpcomingBooking']
type CalendarInfo = components['schemas']['CalendarInfo']

export default function UpcomingBookingsPage({ client = apiClient }: { client?: ApiClient }) {
  const [bookings, setBookings] = useState<ApiResult<UpcomingBooking[]> | null>(null)
  const [calendar, setCalendar] = useState<ApiResult<CalendarInfo> | null>(null)

  useEffect(() => {
    let active = true
    void toApiResult(client.GET('/api/calendar')).then((result) => {
      if (active) setCalendar(result)
    })
    void toApiResult(client.GET('/api/bookings/upcoming')).then((result) => {
      if (active) setBookings(result)
    })
    return () => { active = false }
  }, [client])

  const format = calendar?.ok ? calendarTimeFormatter(calendar.value.timezone) : null

  return (
    <section className="booking-page">
      <h1>Предстоящие события</h1>
      {!calendar && <p role="status">Загрузка календаря…</p>}
      {calendar && !calendar.ok && <RequestFailure result={calendar} subject="календарь" />}
      {calendar?.ok && <p>Часовой пояс календаря: {calendar.value.timezone}</p>}
      {!bookings && <p role="status">Загрузка предстоящих записей…</p>}
      {bookings && !bookings.ok && <RequestFailure result={bookings} subject="предстоящие записи" />}
      {bookings?.ok && bookings.value.length === 0 && <p>Нет предстоящих записей.</p>}
      {bookings?.ok && format && bookings.value.length > 0 && (
        <ul>
          {bookings.value.map((booking) => (
            <li key={booking.start}>
              <h2>{booking.eventTypeName}</h2>
              <p>{booking.guestName}</p>
              <p>{booking.guestEmail}</p>
              <p>{format(booking.start)} — {format(booking.end)}</p>
            </li>
          ))}
        </ul>
      )}
      <CreateEventTypeForm client={client} />
    </section>
  )
}
