import axios from "axios";

export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || "/api";

export function resolveApiAssetUrl(url: string, apiBaseUrl = API_BASE_URL): string {
  if (!/^https?:\/\//i.test(apiBaseUrl)) return url;
  if (/^(?:[a-z][a-z\d+\-.]*:)?\/\//i.test(url)) return url;
  return new URL(url, apiBaseUrl).toString();
}

export function getApiErrorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError(error)) {
    const responseData = error.response?.data;
    if (
      typeof responseData === "object" &&
      responseData !== null &&
      typeof (responseData as { error?: unknown }).error === "string"
    ) {
      return (responseData as { error: string }).error;
    }
  }

  return fallback;
}

const api = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true,
});

export default api;
