import { useEffect, useRef, useState } from "react";
import api from "../../../lib/api";
import { normalizePortraitMap, normalizePortraitUrlField } from "../../../lib/portraitUrls";

interface AccountCharacter {
  name: string;
  realm: string;
  realmName: string;
  level: number;
  region: string;
  classId?: number;
  className?: string;
  portraitUrl?: string;
  activeSpecId?: number | null;
  specName?: string | null;
}

export function useCharacters(visibleChars: AccountCharacter[]): {
  characters: AccountCharacter[];
  loading: boolean;
  portraits: Record<string, string>;
  loadingPortraits: boolean;
} {
  const [characters, setCharacters] = useState<AccountCharacter[]>([]);
  const [loading, setLoading] = useState(true);
  const [portraits, setPortraits] = useState<Record<string, string>>({});
  const [loadingPortraits, setLoadingPortraits] = useState(false);
  const fetchedPortraitIds = useRef(new Set<string>());

  useEffect(() => {
    const fetchCharacters = async () => {
      try {
        const res = await api.get<AccountCharacter[]>("/battlenet/characters");
        if (res.status === 204 || !res.data) {
          const refreshRes = await api.post<AccountCharacter[]>("/battlenet/characters/refresh");
          const sorted = refreshRes.data
            .map((character) => normalizePortraitUrlField(character))
            .sort((a, b) => b.level - a.level);
          setCharacters(sorted);
        } else {
          const sorted = res.data
            .map((character) => normalizePortraitUrlField(character))
            .sort((a, b) => b.level - a.level);
          setCharacters(sorted);
        }
      } catch {
        // Fetch failed — show empty state
      } finally {
        setLoading(false);
      }
    };
    fetchCharacters();
  }, []);

  useEffect(() => {
    const missing = visibleChars.filter(c => {
      if (c.portraitUrl) return false;
      const id = `${c.region}-${c.realm}-${c.name.toLowerCase()}`;
      return !fetchedPortraitIds.current.has(id);
    });
    if (missing.length === 0) return;

    const ids = missing.map(c => `${c.region}-${c.realm}-${c.name.toLowerCase()}`);
    ids.forEach(id => fetchedPortraitIds.current.add(id));

    setLoadingPortraits(true);
    api.post<Record<string, string>>(
      "/battlenet/character-portraits",
      missing.map(c => ({ region: c.region, realm: c.realm, name: c.name }))
    ).then(res => {
      setPortraits(prev => ({ ...prev, ...normalizePortraitMap(res.data) }));
    }).catch(() => {
      ids.forEach(id => fetchedPortraitIds.current.delete(id));
    }).finally(() => {
      setLoadingPortraits(false);
    });
  }, [visibleChars]);

  return { characters, loading, portraits, loadingPortraits };
}
