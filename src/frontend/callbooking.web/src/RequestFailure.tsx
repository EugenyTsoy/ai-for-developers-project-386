import type { ApiHttpFailure, ApiTransportFailure } from './api/client'

export default function RequestFailure({ result, subject }: {
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
