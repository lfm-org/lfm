import { useQuery, useQueryClient } from "@tanstack/react-query";
import api from "../../../lib/api";
import { normalizeGuildHomeResponse, type GuildHomeResponse } from "./guildHome";
import { queryKeys } from "../../../lib/queryKeys";

async function fetchGuildHome(): Promise<GuildHomeResponse> {
  const response = await api.get<GuildHomeResponse>("/guild");
  return normalizeGuildHomeResponse(response.data);
}

export function useGuildHome() {
  const queryClient = useQueryClient();

  const { data = null, isPending: loading, isError } = useQuery({
    queryKey: queryKeys.guild(),
    queryFn: fetchGuildHome,
    staleTime: 60 * 60_000,
  });

  const error = isError ? "Failed to load guild details" : null;

  const reload = async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.guild() });
  };

  const setData = (value: GuildHomeResponse | null) => {
    queryClient.setQueryData<GuildHomeResponse | null>(queryKeys.guild(), value);
  };

  return { data, loading, error, reload, setData };
}
