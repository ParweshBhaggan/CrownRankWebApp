// Keep navigation separate from HTTP requests. Only redirect when the caller
// explicitly chooses to use a URL returned by an API.
export function redirectToUrl(url: string): void
{
  const target = new URL(url, window.location.href)

  if (target.protocol !== 'http:' && target.protocol !== 'https:') {
    throw new Error('The redirect URL must use HTTP or HTTPS.')
  }

  window.location.assign(target.href)
}
