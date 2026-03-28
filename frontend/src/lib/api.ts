import axios from "axios";

export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || "/api";

export function resolveApiAssetUrl(url: string, apiBaseUrl = API_BASE_URL): string {
  if (!/^https?:\/\//i.test(apiBaseUrl)) return url;
  if (/^(?:[a-z][a-z\d+\-.]*:)?\/\//i.test(url)) return url;
  return new URL(url, apiBaseUrl).toString();
}

const api = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true,
});

export default api;
