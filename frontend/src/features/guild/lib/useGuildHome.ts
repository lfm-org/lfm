import { useCallback, useEffect, useState } from "react";
import api from "../../../lib/api";
import type { GuildHomeResponse } from "./guildHome";

export function useGuildHome() {
  const [data, setData] = useState<GuildHomeResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await api.get<GuildHomeResponse>("/guild");
      setData(response.data);
      return response.data;
    } catch {
      setError("Failed to load guild details");
      return null;
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  return { data, loading, error, reload, setData };
}
