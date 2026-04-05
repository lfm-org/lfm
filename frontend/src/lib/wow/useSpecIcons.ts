import { useQuery } from "@tanstack/react-query";
import api from "../api";
import { queryKeys } from "../queryKeys";

interface SpecializationResponse {
  id: number;
  name: string;
  classId: number;
  role: string;
  iconUrl?: string;
}

/** Fetches spec icons from the API. Used as the queryFn and exported for tests. */
export async function fetchSpecIcons(): Promise<Map<number, string>> {
  const res = await api.get<{ specializations: SpecializationResponse[] }>("/reference/specializations");
  const map = new Map<number, string>();
  for (const spec of res.data.specializations) {
    if (spec.iconUrl) map.set(spec.id, spec.iconUrl);
  }
  return map;
}

/** No-op — cache is now managed by TanStack Query. Retained for test compatibility. */
export function _resetCache(): void {}

export function useSpecIcons(): { specIcons: Map<number, string>; loading: boolean } {
  const { data: specIcons = new Map<number, string>(), isPending: loading } = useQuery({
    queryKey: queryKeys.specializations(),
    queryFn: fetchSpecIcons,
    staleTime: Infinity,
    gcTime: Infinity,
  });

  return { specIcons, loading };
}
