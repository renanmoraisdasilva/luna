// Backend failures carry a structured ApiError body ({ code, message }) from every
// Luna service's exception middleware. This reads only that body, so a transport-level
// message (for example Axios's "Request failed with status code 500") or any raw
// exception text is never shown to a customer — backend failures are always presented
// as either the service's own message or the caller's friendly fallback.
export function getApiErrorMessage(error: unknown, fallback: string): string {
  if (typeof error !== 'object' || error === null || !('response' in error)) {
    return fallback;
  }

  const data = (error as { response?: { data?: unknown } }).response?.data;
  if (typeof data !== 'object' || data === null || !('message' in data)) {
    return fallback;
  }

  const message = (data as { message?: unknown }).message;
  return typeof message === 'string' && message.trim().length > 0 ? message : fallback;
}
