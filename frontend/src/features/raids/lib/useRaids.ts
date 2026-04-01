import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router";
import api from "../../../lib/api";
import { normalizeRaid, type Raid } from "./raidTypes";
import { normalizeWowInstances, type WowInstance } from "../../../lib/wow/instances";
import { normalizeRaidSignupCharacter, type RaidSignupCharacter } from "./raidSignupCharacters";
import { groupRaidsByTime } from "./raidGrouping";

const PAGE_SIZE = 5;

interface CharactersResponse {
  characters: RaidSignupCharacter[];
  selectedCharacterId: string | null;
}

function parsePageParam(value: string | null): number {
  if (!value) return 1;
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 1;
}

function clampPage(page: number, totalPages: number): number {
  return Math.min(Math.max(page, 1), totalPages);
}

export interface UseRaidsResult {
  raids: Raid[];
  instances: WowInstance[];
  loading: boolean;
  error: string | null;
  characters: RaidSignupCharacter[];
  selectedCharacterId: string | null;
  loadingChars: boolean;
  charactersError: string | null;
  expandedRaids: Record<string, boolean>;
  requestedRaidId: string | null;
  totalPages: number;
  currentPage: number;
  visibleRaids: Raid[];
  selectedRaid: Raid | null;
  passedRaids: Raid[];
  showPassed: boolean;
  handleTogglePassed: () => void;
  handleRaidUpdate: (updatedRaid: Raid) => void;
  handleToggleRaid: (raidId: string) => void;
  handleSelectRaid: (raidId: string) => void;
  handlePageChange: (page: number) => void;
  handleRaidDelete: (raidId: string) => void;
  refresh: () => void;
}

export function useRaids(battleNetId: string | null, isDesktop: boolean, isMobile: boolean): UseRaidsResult {
  const [searchParams, setSearchParams] = useSearchParams();
  const [raids, setRaids] = useState<Raid[]>([]);
  const [instances, setInstances] = useState<WowInstance[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [characters, setCharacters] = useState<RaidSignupCharacter[]>([]);
  const [selectedCharacterId, setSelectedCharacterId] = useState<string | null>(null);
  const [loadingChars, setLoadingChars] = useState(true);
  const [charactersError, setCharactersError] = useState<string | null>(null);
  const [expandedRaids, setExpandedRaids] = useState<Record<string, boolean>>({});
  const lastFocusedRaidId = useRef<string | null>(null);

  const loadRaids = useCallback(() => {
    let active = true;
    setLoading(true);
    setError(null);

    Promise.allSettled([
      api.get<Raid[]>("/raids"),
      api.get<WowInstance[]>("/instances"),
    ])
      .then(([raidResult, instanceResult]) => {
        if (!active) return;

        if (raidResult.status === "fulfilled") {
          setRaids(raidResult.value.data.map(normalizeRaid));
        } else {
          setRaids([]);
          setError("Failed to load raids");
        }

        if (instanceResult.status === "fulfilled") {
          setInstances(normalizeWowInstances(instanceResult.value.data));
        } else {
          setInstances([]);
        }
      })
      .finally(() => {
        if (active) {
          setLoading(false);
        }
      });

    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    return loadRaids();
  }, [loadRaids]);

  useEffect(() => {
    if (!battleNetId) {
      setCharacters([]);
      setSelectedCharacterId(null);
      setLoadingChars(false);
      setCharactersError(null);
      return;
    }

    let active = true;
    setLoadingChars(true);
    setCharactersError(null);

    api.get<CharactersResponse>("/raider/characters")
      .then((response) => {
        if (!active) return;
        setCharacters(response.data.characters.map(normalizeRaidSignupCharacter));
        setSelectedCharacterId(response.data.selectedCharacterId);
      })
      .catch(() => {
        if (!active) return;
        setCharacters([]);
        setSelectedCharacterId(null);
        setCharactersError("Failed to load characters");
      })
      .finally(() => {
        if (active) {
          setLoadingChars(false);
        }
      });

    return () => {
      active = false;
    };
  }, [battleNetId]);

  const requestedRaidId = searchParams.get("raid");
  const requestedPage = parsePageParam(searchParams.get("page"));
  const showPassed = searchParams.get("passed") === "1";

  const { upcoming, passed } = useMemo(() => groupRaidsByTime(raids), [raids]);

  const targetIndex = requestedRaidId ? upcoming.findIndex((raid) => raid.id === requestedRaidId) : -1;
  const targetInPassed = requestedRaidId && targetIndex < 0 ? passed.some((r) => r.id === requestedRaidId) : false;
  const totalPages = Math.max(1, Math.ceil(upcoming.length / PAGE_SIZE));
  const currentPage = clampPage(
    targetIndex >= 0 ? Math.floor(targetIndex / PAGE_SIZE) + 1 : requestedPage,
    totalPages
  );
  const visibleRaids = upcoming.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

  // Auto-select first raid on desktop when no raid is selected
  useEffect(() => {
    if (!isDesktop || loading || (visibleRaids.length === 0 && passed.length === 0)) return;
    const isSelected = requestedRaidId && (visibleRaids.some(r => r.id === requestedRaidId) || passed.some(r => r.id === requestedRaidId));
    if (!isSelected) {
      const fallback = visibleRaids[0] ?? passed[0];
      if (fallback) {
        const next = new URLSearchParams(searchParams);
        if (!visibleRaids[0] && passed[0]) next.set("passed", "1");
        next.set("raid", fallback.id);
        setSearchParams(next, { replace: true });
      }
    }
  }, [isDesktop, loading, visibleRaids, passed, requestedRaidId]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (!isMobile || !requestedRaidId || targetIndex < 0) return;

    setExpandedRaids((current) => (
      current[requestedRaidId] ? current : { ...current, [requestedRaidId]: true }
    ));
  }, [isMobile, requestedRaidId, targetIndex]);

  useEffect(() => {
    if (targetInPassed && !showPassed) {
      const next = new URLSearchParams(searchParams);
      next.set("passed", "1");
      setSearchParams(next, { replace: true });
    }
  }, [targetInPassed]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (isDesktop || !requestedRaidId || targetIndex < 0) return;

    const isVisible = visibleRaids.some((raid) => raid.id === requestedRaidId);
    if (!isVisible || lastFocusedRaidId.current === requestedRaidId) return;

    const frame = requestAnimationFrame(() => {
      document.getElementById(`raid-card-${requestedRaidId}`)?.scrollIntoView({ block: "start" });
      lastFocusedRaidId.current = requestedRaidId;
    });

    return () => cancelAnimationFrame(frame);
  }, [isDesktop, requestedRaidId, targetIndex, visibleRaids]);

  const handleRaidUpdate = (updatedRaid: Raid) => {
    setRaids((current) => current.map((raid) => (
      raid.id === updatedRaid.id ? normalizeRaid(updatedRaid) : raid
    )));
  };

  const handleToggleRaid = (raidId: string) => {
    setExpandedRaids((current) => ({
      ...current,
      [raidId]: !current[raidId],
    }));
  };

  const handleSelectRaid = (raidId: string) => {
    const next = new URLSearchParams(searchParams);
    next.set("raid", raidId);
    setSearchParams(next);
  };

  const handleTogglePassed = () => {
    const next = new URLSearchParams(searchParams);
    if (showPassed) {
      next.delete("passed");
    } else {
      next.set("passed", "1");
    }
    setSearchParams(next);
  };

  const handlePageChange = (page: number) => {
    const next = new URLSearchParams(searchParams);
    if (page <= 1) {
      next.delete("page");
    } else {
      next.set("page", String(page));
    }
    setSearchParams(next);
  };

  const handleRaidDelete = (raidId: string) => {
    setRaids((current) => current.filter((raid) => raid.id !== raidId));
  };

  const selectedRaid = requestedRaidId
    ? (visibleRaids.find(r => r.id === requestedRaidId)
      ?? passed.find(r => r.id === requestedRaidId)
      ?? null)
    : null;

  return {
    raids,
    instances,
    loading,
    error,
    characters,
    selectedCharacterId,
    loadingChars,
    charactersError,
    expandedRaids,
    requestedRaidId,
    totalPages,
    currentPage,
    visibleRaids,
    selectedRaid,
    passedRaids: passed,
    showPassed,
    handleTogglePassed,
    handleRaidUpdate,
    handleToggleRaid,
    handleSelectRaid,
    handlePageChange,
    handleRaidDelete,
    refresh: loadRaids,
  };
}
