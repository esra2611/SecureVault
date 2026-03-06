/**
 * Returns the API base URL for the current context.
 * - Browser: public URL (e.g. http://localhost:8080) so the client can call the API.
 * - Server (SSR): internal URL (e.g. http://api:8080 in Docker) so the frontend container can reach the api service.
 */
export function useApiBase(): string {
  const config = useRuntimeConfig()
  if (import.meta.server) {
    return (config as { apiBaseInternal?: string }).apiBaseInternal ?? config.public.apiBase
  }
  return config.public.apiBase
}
