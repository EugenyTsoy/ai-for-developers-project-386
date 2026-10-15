import { useEffect, useRef, useState } from 'react'
import { apiClient, toApiResult } from './api/client'
import type { ApiClient, ApiResult } from './api/client'
import type { components } from './api/generated/schema'

type EventType = components['schemas']['EventType']

export default function CreateEventTypeForm({ client = apiClient }: { client?: ApiClient }) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [duration, setDuration] = useState('')
  const [result, setResult] = useState<ApiResult<EventType> | null>(null)
  const [errors, setErrors] = useState({ name: '', duration: '' })
  const [pending, setPending] = useState(false)
  const inFlight = useRef(false)
  const active = useRef(false)
  useEffect(() => {
    active.current = true
    return () => { active.current = false }
  }, [])

  return (
    <form className="event-type-form" aria-labelledby="create-event-type-title" noValidate onSubmit={(event) => {
      event.preventDefault()
      if (inFlight.current) return
      setResult(null)
      const durationMinutes = Number(duration)
      const validation = {
        name: name.trim() ? '' : 'Введите название.',
        duration: Number.isInteger(durationMinutes) && durationMinutes >= 1 && durationMinutes <= 2147483647
          ? '' : 'Введите положительную целую длительность в пределах int32.',
      }
      setErrors(validation)
      if (validation.name || validation.duration) return
      inFlight.current = true
      setPending(true)
      void toApiResult(client.POST('/api/event-types', {
        body: { name, ...(description ? { description } : {}), durationMinutes },
      })).then((outcome) => {
        if (!active.current) return
        inFlight.current = false
        setPending(false)
        setResult(outcome)
      })
    }}>
      <h2 id="create-event-type-title">Создать тип события</h2>
      <label htmlFor="event-type-name">Название</label>
      <input id="event-type-name" required value={name} disabled={pending}
        aria-invalid={!!errors.name} aria-describedby={errors.name ? 'event-type-name-error' : undefined}
        onChange={(event) => setName(event.target.value)} />
      {errors.name && <p id="event-type-name-error" role="alert">{errors.name}</p>}
      <label htmlFor="event-type-description">Описание (необязательно)</label>
      <textarea id="event-type-description" value={description} disabled={pending} onChange={(event) => setDescription(event.target.value)} />
      <label htmlFor="event-type-duration">Длительность в минутах</label>
      <input id="event-type-duration" type="number" min="1" max="2147483647" step="1" required value={duration} disabled={pending}
        aria-invalid={!!errors.duration} aria-describedby={errors.duration ? 'event-type-duration-error' : undefined}
        onChange={(event) => setDuration(event.target.value)} />
      {errors.duration && <p id="event-type-duration-error" role="alert">{errors.duration}</p>}
      <button type="submit" disabled={pending}>{pending ? 'Сохранение…' : 'Создать тип события'}</button>
      {pending && <p role="status">Сохранение типа события…</p>}
      {result?.ok && <p role="status">Тип события «{result.value.name}» создан.</p>}
      {result && !result.ok && <p role="alert">
        {result.kind === 'transport'
          ? 'Нет соединения: подтверждение создания типа события не получено.'
          : `Не удалось создать тип события (HTTP ${result.status}).`}
      </p>}
    </form>
  )
}
