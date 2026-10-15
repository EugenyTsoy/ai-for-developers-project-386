export function calendarTimeFormatter(timezone: string) {
  const formatter = new Intl.DateTimeFormat('ru-RU', {
    timeZone: timezone,
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
    timeZoneName: 'longOffset',
  })
  return (instant: string) => formatter.format(new Date(instant)).replace('GMT', 'UTC')
}
