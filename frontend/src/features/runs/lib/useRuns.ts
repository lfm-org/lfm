import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router";
import api from "../../../lib/api";
import { normalizeRun, type Run } from "./runTypes";
import { normalizeWowInstances, type WowInstance } from "../../../lib/wow/instances";
import { normalizeRunSignupCharacter, type RunSignupCharacter } from "./runSignupCharacters";
import { groupRunsByTime } from "./runGrouping";

const PAGE_SIZE = 5;

interface CharactersResponse {
  characters: RunSignupCharacter[];
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

export interface UseRunsResult {
  runs: Run[];
  instances: WowInstance[];
  loading: boolean;
  error: string | null;
  characters: RunSignupCharacter[];
  selectedCharacterId: string | null;
  loadingChars: boolean;
  charactersError: string | null;
  expandedRuns: Record<string, boolean>;
  requestedRunId: string | null;
  totalPages: number;
  currentPage: number;
  visibleRuns: Run[];
  selectedRun: Run | null;
  passedRuns: Run[];
  showPassed: boolean;
  handleTogglePassed: () => void;
  handleRunUpdate: (updatedRun: Run) => void;
  handleToggleRun: (runId: string) => void;
  handleSelectRun: (runId: string) => void;
  handlePageChange: (page: number) => void;
  handleRunDelete: (runId: string) => void;
  refresh: () => void;
  sortOrder: "asc" | "desc";
  handleSortChange: (order: "asc" | "desc") => void;
}

export function useRuns(battleNetId: string | null, isDesktop: boolean, isMobile: boolean): UseRunsResult {
  const [searchParams, setSearchParams] = useSearchParams();
  const [runs, setRuns] = useState<Run[]>([]);
  const [instances, setInstances] = useState<WowInstance[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [characters, setCharacters] = useState<RunSignupCharacter[]>([]);
  const [selectedCharacterId, setSelectedCharacterId] = useState<string | null>(null);
  const [loadingChars, setLoadingChars] = useState(true);
  const [charactersError, setCharactersError] = useState<string | null>(null);
  const [expandedRuns, setExpandedRuns] = useState<Record<string, boolean>>({});
  const lastFocusedRunId = useRef<string | null>(null);

  const loadRuns = useCallback(() => {
    let active = true;
    setLoading(true);
    setError(null);

    Promise.allSettled([
      api.get<Run[]>("/runs"),
      api.get<WowInstance[]>("/instances"),
    ])
      .then(([runResult, instanceResult]) => {
        if (!active) return;

        if (runResult.status === "fulfilled") {
          setRuns(runResult.value.data.map(normalizeRun));
        } else {
          setRuns([]);
          setError("runs.loadFailed");
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
    return loadRuns();
  }, [loadRuns]);

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
        setCharacters(response.data.characters.map(normalizeRunSignupCharacter));
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

  const requestedRunId = searchParams.get("run");
  const requestedPage = parsePageParam(searchParams.get("page"));
  const showPassed = searchParams.get("passed") === "1";
  const sortOrder = (searchParams.get("sort") === "asc" ? "asc" : "desc") as "asc" | "desc";

  const { upcoming, passed } = useMemo(() => groupRunsByTime(runs), [runs]);

  const sortedUpcoming = useMemo(() => {
    const sorted = [...upcoming];
    if (sortOrder === "asc") sorted.reverse();
    return sorted;
  }, [upcoming, sortOrder]);

  const targetIndex = requestedRunId ? sortedUpcoming.findIndex((run) => run.id === requestedRunId) : -1;
  const targetInPassed = requestedRunId && targetIndex < 0 ? passed.some((r) => r.id === requestedRunId) : false;
  const totalPages = Math.max(1, Math.ceil(sortedUpcoming.length / PAGE_SIZE));
  const currentPage = clampPage(
    targetIndex >= 0 ? Math.floor(targetIndex / PAGE_SIZE) + 1 : requestedPage,
    totalPages
  );
  const visibleRuns = sortedUpcoming.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

  // Auto-select first run on desktop when no run is selected
  useEffect(() => {
    if (!isDesktop || loading || (visibleRuns.length === 0 && passed.length === 0)) return;
    const isSelected = requestedRunId && (visibleRuns.some(r => r.id === requestedRunId) || passed.some(r => r.id === requestedRunId));
    if (!isSelected) {
      const fallback = visibleRuns[0] ?? passed[0];
      if (fallback) {
        const next = new URLSearchParams(searchParams);
        if (!visibleRuns[0] && passed[0]) next.set("passed", "1");
        next.set("run", fallback.id);
        setSearchParams(next, { replace: true });
      }
    }
  }, [isDesktop, loading, visibleRuns, passed, requestedRunId]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (!isMobile || !requestedRunId || targetIndex < 0) return;

    setExpandedRuns((current) => (
      current[requestedRunId] ? current : { ...current, [requestedRunId]: true }
    ));
  }, [isMobile, requestedRunId, targetIndex]);

  useEffect(() => {
    if (targetInPassed && !showPassed) {
      const next = new URLSearchParams(searchParams);
      next.set("passed", "1");
      setSearchParams(next, { replace: true });
    }
  }, [targetInPassed]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (isDesktop || !requestedRunId || targetIndex < 0) return;

    const isVisible = visibleRuns.some((run) => run.id === requestedRunId);
    if (!isVisible || lastFocusedRunId.current === requestedRunId) return;

    const frame = requestAnimationFrame(() => {
      document.getElementById(`run-card-${requestedRunId}`)?.scrollIntoView({ block: "start" });
      lastFocusedRunId.current = requestedRunId;
    });

    return () => cancelAnimationFrame(frame);
  }, [isDesktop, requestedRunId, targetIndex, visibleRuns]);

  const handleRunUpdate = (updatedRun: Run) => {
    setRuns((current) => current.map((run) => (
      run.id === updatedRun.id ? normalizeRun(updatedRun) : run
    )));
  };

  const handleToggleRun = (runId: string) => {
    setExpandedRuns((current) => ({
      ...current,
      [runId]: !current[runId],
    }));
  };

  const handleSelectRun = (runId: string) => {
    const next = new URLSearchParams(searchParams);
    next.set("run", runId);
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

  const handleRunDelete = (runId: string) => {
    setRuns((current) => current.filter((run) => run.id !== runId));
  };

  const handleSortChange = (order: "asc" | "desc") => {
    const next = new URLSearchParams(searchParams);
    if (order === "desc") {
      next.delete("sort");
    } else {
      next.set("sort", order);
    }
    next.delete("page");
    setSearchParams(next);
  };

  const selectedRun = requestedRunId
    ? (visibleRuns.find(r => r.id === requestedRunId)
      ?? passed.find(r => r.id === requestedRunId)
      ?? null)
    : null;

  return {
    runs,
    instances,
    loading,
    error,
    characters,
    selectedCharacterId,
    loadingChars,
    charactersError,
    expandedRuns,
    requestedRunId,
    totalPages,
    currentPage,
    visibleRuns,
    selectedRun,
    passedRuns: passed,
    showPassed,
    handleTogglePassed,
    handleRunUpdate,
    handleToggleRun,
    handleSelectRun,
    handlePageChange,
    handleRunDelete,
    refresh: loadRuns,
    sortOrder,
    handleSortChange,
  };
}
