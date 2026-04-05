export const queryKeys = {
  me: () => ["me"] as const,
  characters: () => ["battlenet", "characters"] as const,
  characterPortraits: (ids: string[]) => ["battlenet", "portraits", ids.sort().join(",")] as const,
  runs: () => ["runs"] as const,
  instances: () => ["instances"] as const,
  guild: () => ["guild"] as const,
  specializations: () => ["reference", "specializations"] as const,
  raiderCharacters: (battleNetId: string) => ["raider", battleNetId, "characters"] as const,
};
