import { useEffect, useState } from "react";
import api from "../api";

interface SpecializationResponse {
  id: number;
  name: string;
  classId: number;
  role: string;
  iconUrl?: string;
}

let cachedPromise: Promise<Map<number, string>> | null = null;
let cachedResult: Map<number, string> | null = null;

export function _resetCache(): void {
  cachedPromise = null;
  cachedResult = null;
}

export async function fetchSpecIcons(): Promise<Map<number, string>> {
  if (cachedResult) return cachedResult;
  if (cachedPromise) return cachedPromise;

  cachedPromise = api
    .get<{ specializations: SpecializationResponse[] }>("/reference/specializations")
    .then((res) => {
      const map = new Map<number, string>();
      for (const spec of res.data.specializations) {
        if (spec.iconUrl) map.set(spec.id, spec.iconUrl);
      }
      cachedResult = map;
      return map;
    })
    .catch(() => {
      cachedPromise = null;
      return new Map<number, string>();
    });

  return cachedPromise;
}

export function useSpecIcons(): { specIcons: Map<number, string>; loading: boolean } {
  const [specIcons, setSpecIcons] = useState<Map<number, string>>(cachedResult ?? new Map());
  const [loading, setLoading] = useState(!cachedResult);

  useEffect(() => {
    if (cachedResult) {
      setSpecIcons(cachedResult);
      setLoading(false);
      return;
    }
    let cancelled = false;
    fetchSpecIcons().then((map) => {
      if (!cancelled) {
        setSpecIcons(map);
        setLoading(false);
      }
    });
    return () => { cancelled = true; };
  }, []);

  return { specIcons, loading };
}
