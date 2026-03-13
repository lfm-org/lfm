export function buildApiUrl(path: string): string {
  const baseUrl = process.env.REACT_APP_API_BASE_URL;
  if (baseUrl !== undefined && baseUrl !== "") {
    return `${baseUrl}${path}`;
  }

  const host = process.env.REACT_APP_API_HOST;
  if (host === undefined || host === "") {
    return `/api${path}`;
  }

  const scheme = process.env.REACT_APP_API_SCHEME || window.location.protocol.replace(":", "") || "http";
  const port = process.env.REACT_APP_API_PORT ? `:${process.env.REACT_APP_API_PORT}` : "";

  return `${scheme}://${host}${port}${path}`;
}
