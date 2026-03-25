import { useEffect, useRef, useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Divider,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import { useNavigate, useSearchParams } from "react-router";
import PageContainer from "../../../components/layout/PageContainer";
import api from "../../../lib/api";
import { normalizeRaid, type Raid } from "../lib/raidTypes";
import { normalizeWowInstances, resolveInstanceModeLabel, type WowInstance } from "../../../lib/wow/instances";
import { useAuth } from "../../auth";
import { useGuildHome } from "../../guild/lib/useGuildHome";
import RaidListCard from "../components/RaidListCard";
import RaidSummaryItem from "../components/RaidSummaryItem";
import {
  normalizeRaidSignupCharacter,
  type RaidSignupCharacter,
} from "../lib/raidSignupCharacters";

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

export default function RaidsPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { user } = useAuth();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const isDesktop = useMediaQuery(theme.breakpoints.up("md"));
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
  const { data: guildHome } = useGuildHome();

  useEffect(() => {
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
    if (!user) {
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
  }, [user?.battleNetId]);

  const requestedRaidId = searchParams.get("raid");
  const requestedPage = parsePageParam(searchParams.get("page"));
  const targetIndex = requestedRaidId ? raids.findIndex((raid) => raid.id === requestedRaidId) : -1;
  const totalPages = Math.max(1, Math.ceil(raids.length / PAGE_SIZE));
  const currentPage = clampPage(
    targetIndex >= 0 ? Math.floor(targetIndex / PAGE_SIZE) + 1 : requestedPage,
    totalPages
  );
  const visibleRaids = raids.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

  // Auto-select first raid on desktop when no raid is selected
  useEffect(() => {
    if (!isDesktop || loading || visibleRaids.length === 0) return;
    const isSelected = requestedRaidId && visibleRaids.some(r => r.id === requestedRaidId);
    if (!isSelected) {
      const next = new URLSearchParams(searchParams);
      next.set("raid", visibleRaids[0].id);
      setSearchParams(next, { replace: true });
    }
  }, [isDesktop, loading, visibleRaids, requestedRaidId]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (!isMobile || !requestedRaidId || targetIndex < 0) return;

    setExpandedRaids((current) => (
      current[requestedRaidId] ? current : { ...current, [requestedRaidId]: true }
    ));
  }, [isMobile, requestedRaidId, targetIndex]);

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

  const handlePageChange = (page: number) => {
    const next = new URLSearchParams(searchParams);
    if (page <= 1) {
      next.delete("page");
    } else {
      next.set("page", String(page));
    }
    next.delete("raid");
    setSearchParams(next);
  };

  const selectedRaid = requestedRaidId
    ? visibleRaids.find(r => r.id === requestedRaidId) ?? null
    : null;

  const pagination = !loading && totalPages > 1 && (
    <Box
      component="nav"
      aria-label="Raid pages"
      sx={{ mt: 2, display: "flex", justifyContent: "center", gap: 1, flexWrap: "wrap" }}
    >
      <Button size="small" variant="outlined" disabled={currentPage === 1} onClick={() => handlePageChange(currentPage - 1)}>
        Previous
      </Button>
      {Array.from({ length: totalPages }, (_, index) => {
        const page = index + 1;
        return (
          <Button
            key={page}
            size="small"
            variant={page === currentPage ? "contained" : "outlined"}
            aria-current={page === currentPage ? "page" : undefined}
            onClick={() => handlePageChange(page)}
          >
            {page}
          </Button>
        );
      })}
      <Button size="small" variant="outlined" disabled={currentPage === totalPages} onClick={() => handlePageChange(currentPage + 1)}>
        Next
      </Button>
    </Box>
  );

  return (
    <PageContainer maxWidth={isDesktop ? 1280 : undefined}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2, gap: 2 }}>
        <Typography component="h1" variant="h5">Raids</Typography>
        <Button variant="contained" onClick={() => navigate("/raids/new")}>Create Raid</Button>
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}>
          <CircularProgress />
        </Box>
      ) : raids.length === 0 ? (
        <Typography color="text.secondary">No raids found.</Typography>
      ) : isDesktop ? (
        /* Desktop: two-column layout */
        <Box sx={{ display: "flex", gap: 3, alignItems: "flex-start" }}>
          {/* Left panel: raid list */}
          <Box
            sx={{
              width: 320,
              flexShrink: 0,
              border: "1px solid",
              borderColor: "divider",
              borderRadius: 2,
              overflow: "hidden",
            }}
          >
            {visibleRaids.map((raid, index) => (
              <Box key={raid.id}>
                {index > 0 && <Divider />}
                <RaidSummaryItem
                  raid={raid}
                  modeLabel={resolveInstanceModeLabel(instances, raid.instanceId, raid.modeKey)}
                  selected={raid.id === requestedRaidId}
                  onClick={() => handleSelectRaid(raid.id)}
                  guildTimezone={guildHome?.setup.timezone}
                />
              </Box>
            ))}
            {pagination && <Box sx={{ borderTop: "1px solid", borderColor: "divider", p: 1 }}>{pagination}</Box>}
          </Box>

          {/* Right panel: selected raid details */}
          <Box sx={{ flex: 1, minWidth: 0 }}>
            {selectedRaid ? (
              <RaidListCard
                key={selectedRaid.id}
                raid={selectedRaid}
                modeLabel={resolveInstanceModeLabel(instances, selectedRaid.instanceId, selectedRaid.modeKey)}
                isMobile={false}
                isExpanded={true}
                onToggle={() => {}}
                onRaidUpdate={handleRaidUpdate}
                characters={characters}
                selectedCharacterId={selectedCharacterId}
                loadingChars={loadingChars}
                charactersError={charactersError}
                guildTimezone={guildHome?.setup.timezone}
                canSignupToGuildRaids={guildHome?.memberPermissions.canSignupGuildRaids ?? false}
              />
            ) : (
              <Typography color="text.secondary">Select a raid to view details.</Typography>
            )}
          </Box>
        </Box>
      ) : (
        /* Mobile: stacked cards */
        <Box sx={{ display: "grid", gap: 3 }}>
          {visibleRaids.map((raid) => (
            <RaidListCard
              key={raid.id}
              raid={raid}
              modeLabel={resolveInstanceModeLabel(instances, raid.instanceId, raid.modeKey)}
              isMobile={isMobile}
              isExpanded={Boolean(expandedRaids[raid.id])}
              onToggle={() => handleToggleRaid(raid.id)}
              onRaidUpdate={handleRaidUpdate}
              characters={characters}
              selectedCharacterId={selectedCharacterId}
              loadingChars={loadingChars}
              charactersError={charactersError}
              guildTimezone={guildHome?.setup.timezone}
              canSignupToGuildRaids={guildHome?.memberPermissions.canSignupGuildRaids ?? false}
            />
          ))}
          {pagination}
        </Box>
      )}
    </PageContainer>
  );
}
