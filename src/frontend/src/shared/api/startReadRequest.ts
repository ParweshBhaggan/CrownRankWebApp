// Defer dispatch so an effect cleaned up immediately (including StrictMode's
// development probe) never opens an HTTP/database request.
export function startReadRequest<T>(
  request: (signal: AbortSignal) => Promise<T>,
  onSuccess: (value: T) => void,
  onError: (error: unknown) => void,
  timeoutMs = 30_000,
): () => void {
  const controller = new AbortController()
  let active = true
  let settled = false
  let timer: ReturnType<typeof setTimeout> | undefined

  const fail = (error: unknown) => {
    if (!active || settled) return
    settled = true
    clearTimeout(timer)
    onError(error)
  }

  queueMicrotask(async () => {
    if (!active) return
    timer = setTimeout(() => {
      controller.abort()
      fail(new Error('The ranking request timed out. Check that the API and database are running, then retry.'))
    }, timeoutMs)
    try {
      const value = await request(controller.signal)
      if (!active || settled) return
      settled = true
      clearTimeout(timer)
      onSuccess(value)
    } catch (error) { fail(error) }
  })

  return () => {
    active = false
    clearTimeout(timer)
    if (!settled) controller.abort()
  }
}
