import test from 'node:test'
import assert from 'node:assert/strict'
import { startReadRequest } from '../src/shared/api/startReadRequest.ts'
const tick = () => new Promise(resolve => setImmediate(resolve))

test('immediate cleanup does not dispatch a request; replacement completes', async () => {
  let requests = 0
  const received = []
  const request = async () => { requests++; return ['creator'] }
  const first = startReadRequest(request, value => received.push(value), assert.fail)
  first()
  const second = startReadRequest(request, value => received.push(value), assert.fail)
  await tick()
  assert.equal(requests, 1)
  assert.deepEqual(received, [['creator']])
  second()
})

test('cleanup aborts an active request without reporting a user-facing failure', async () => {
  let signal
  const stop = startReadRequest(s => {
    signal = s
    return new Promise((resolve, reject) => s.addEventListener('abort', () => reject(new Error('aborted'))))
  }, assert.fail, assert.fail)
  await tick()
  stop()
  await tick()
  assert.equal(signal.aborted, true)
})

test('a failed request reaches the error state', async () => {
  const failure = new Error('Database unavailable')
  let reported
  const stop = startReadRequest(async () => { throw failure }, assert.fail, error => { reported = error })
  await tick()
  assert.equal(reported, failure)
  stop()
})

test('a stalled request times out once and ignores a late result', async () => {
  let resolveRequest
  let signal
  const errors = []
  await new Promise(done => {
    startReadRequest(s => {
      signal = s
      return new Promise(resolve => { resolveRequest = resolve })
    }, assert.fail, error => { errors.push(error); done() }, 5)
  })
  assert.equal(signal.aborted, true)
  assert.match(errors[0].message, /timed out/)
  resolveRequest([])
  await tick()
  assert.equal(errors.length, 1)
})
