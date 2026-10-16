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
for (const durationMinutes of [30, 60]) {
  types.push(await json('/api/event-types', 201, { name: `${run} ${durationMinutes}`, durationMinutes }))
}
const listed = await json('/api/event-types')
assert(types.every((type) => listed.some((item) => item.id === type.id)))
const slotsPath = (type) => `/api/event-types/${encodeURIComponent(type.id)}/slots`
const slots = await Promise.all(types.map((type) => json(slotsPath(type))))
const slot = slots[1].find((candidate) => Date.parse(candidate.start) > Date.now() + 3600000
  && slots[0].some((half) => half.start === candidate.start))
assert(slot, 'Need a free canonical hour slot more than one hour in the future')

const body = (index) => ({
  eventTypeId: types[index % 2].id, start: slot.start,
  name: `${run} guest ${index}`, email: `container${index}@example.com`,
})
const responses = await Promise.all(Array.from({ length: 8 }, (_, index) => request('/api/bookings', body(index))))
assert.deepEqual(responses.map((response) => response.status).sort(), [201, 409, 409, 409, 409, 409, 409, 409])
let confirmation
for (const response of responses) {
  const result = await response.json()
  if (response.status === 201) {
    assert.deepEqual(Object.keys(result).sort(), ['end', 'start'])
    assert.equal(result.start, slot.start)
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
console.log('PASS: 8 concurrent requests across two types -> one 201, seven 409; replay 409; overlaps removed')
