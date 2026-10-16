import assert from 'node:assert/strict'

// Creates synthetic data. Run only against a disposable local instance.
const origin = process.argv[2] || 'http://localhost:8080'
const run = `Container ${crypto.randomUUID()}`
const request = (path, body) => fetch(new URL(path, origin), {
  ...(body ? { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) } : {}),
  signal: AbortSignal.timeout(10000),
})
async function json(path, status = 200, body) {
  const response = await request(path, body)
  assert.equal(response.status, status, path)
  assert.match(response.headers.get('content-type'), /application\/json/)
  return response.json()
}

let html
for (const path of ['/', '/booking', '/events']) {
  const response = await request(path)
  assert.equal(response.status, 200, path)
  assert.match(response.headers.get('content-type'), /text\/html/)
  const page = await response.text()
  assert.match(page, /<div id="root"><\/div>/)
  if (html) assert.equal(page, html)
  html = page
}
const assets = [...html.matchAll(/(?:src|href)="(\/assets\/[^\"]+)"/g)].map((match) => match[1])
assert(assets.some((path) => path.endsWith('.js')), 'Built JS referenced by index.html')
assert(assets.some((path) => path.endsWith('.css')), 'Built CSS referenced by index.html')
for (const path of assets) {
  const response = await request(path)
  assert.equal(response.status, 200, path)
  assert(!response.headers.get('content-type')?.includes('text/html'), path)
  assert((await response.text()).length > 0, path)
}
for (const path of ['/api/not-found', '/api/not-found.json', '/assets/not-found.js']) {
  assert.equal((await request(path)).status, 404, path)
}
console.log('PASS: SPA routes, built assets, unknown API/assets return 404')

const calendar = await json('/api/calendar')
assert.equal(typeof calendar.timezone, 'string')
new Intl.DateTimeFormat('en', { timeZone: calendar.timezone })
const types = []
for (const durationMinutes of [30, 45, 60]) {
  types.push(await json('/api/event-types', 201, { name: `${run} ${durationMinutes}`, durationMinutes }))
}
const listed = await json('/api/event-types')
assert(types.every((type) => listed.some((item) => item.id === type.id)))
const slotsPath = (type) => `/api/event-types/${encodeURIComponent(type.id)}/slots`
const slots = await Promise.all(types.map((type) => json(slotsPath(type))))
for (let index = 0; index < types.length; index++) {
  for (const candidate of slots[index]) {
    assert.equal(Date.parse(candidate.end) - Date.parse(candidate.start), types[index].durationMinutes * 60000)
    assert(slots[0].some((short) => short.start === candidate.start), 'All durations share the same start grid')
  }
}
const slot = slots[2].find((candidate) => Date.parse(candidate.start) > Date.now() + 3600000
  && slots.every((list) => list.some((item) => item.start === candidate.start)
    && list.some((item) => Date.parse(item.start) === Date.parse(candidate.start) + 1800000)))
assert(slot, 'Need a free canonical hour slot more than one hour in the future')

const body = (index) => ({
  eventTypeId: types[index % types.length].id, start: slot.start,
  name: `${run} guest ${index}`, email: `container${index}@example.com`,
})
const offGrid = await request('/api/bookings', {
  ...body(1), start: new Date(Date.parse(slot.start) + 900000).toISOString(),
})
assert.equal(offGrid.status, 400)
assert.match(offGrid.headers.get('content-type'), /application\/problem\+json/)
assert.equal((await offGrid.json()).status, 400)
const responses = await Promise.all(Array.from({ length: 8 }, (_, index) => request('/api/bookings', body(index))))
assert.deepEqual(responses.map((response) => response.status).sort(), [201, 409, 409, 409, 409, 409, 409, 409])
let confirmation
for (const [index, response] of responses.entries()) {
  const result = await response.json()
  if (response.status === 201) {
    assert.deepEqual(Object.keys(result).sort(), ['end', 'start'])
    assert.equal(result.start, slot.start)
    assert.equal(Date.parse(result.end) - Date.parse(result.start), types[index % types.length].durationMinutes * 60000)
    confirmation = result
  } else {
    assert.match(response.headers.get('content-type'), /application\/problem\+json/)
    assert.equal(result.status, 409)
    assert.equal(typeof result.title, 'string')
  }
}
const replay = await request('/api/bookings', body(0))
assert.equal(replay.status, 409)
const upcoming = (await json('/api/bookings/upcoming')).filter((booking) => booking.guestName.startsWith(run))
assert.equal(upcoming.length, 1)
assert.equal(upcoming[0].start, confirmation.start)
assert.equal(upcoming[0].end, confirmation.end)
assert(types.some((type) => type.name === upcoming[0].eventTypeName))
for (const type of types) {
  const remaining = await json(slotsPath(type))
  assert(!remaining.some((candidate) => candidate.start < confirmation.end && confirmation.start < candidate.end))
}
console.log(`PASS: calendar (${calendar.timezone}), create/list types, slots, booking/upcoming`)
console.log('PASS: fixed 30-minute start interval for 30/45/60; off-grid 400; 8 concurrent requests across three types -> one 201, seven 409; replay 409; overlaps removed')
