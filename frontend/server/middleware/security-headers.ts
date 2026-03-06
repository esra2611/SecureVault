/**
 * Sets security-related response headers for all responses.
 * Referrer-Policy: no-referrer prevents the token in the URL from being sent as Referer.
 */
export default defineEventHandler((event) => {
  setResponseHeaders(event, {
    'Referrer-Policy': 'no-referrer',
  })
})
