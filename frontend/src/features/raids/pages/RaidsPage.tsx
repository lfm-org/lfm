import {
  Box,
  Button,
  Divider,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Select,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import EventBusyIcon from "@mui/icons-material/EventBusy";
import { useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import { useToast } from "../../../components/ToastContext";
import LoadingState from "../../../components/LoadingState";
import ErrorState from "../../../components/ErrorState";
import EmptyState from "../../../components/EmptyState";
import PageContainer from "../../../components/layout/PageContainer";
import useDocumentTitle from "../../../hooks/useDocumentTitle";
import { resolveInstanceModeLabel } from "../../../lib/wow/instances";
import { useAuth } from "../../auth";
import { useGuildHome } from "../../guild/lib/useGuildHome";
import RaidListCard from "../components/RaidListCard";
import RaidSummaryItem from "../components/RaidSummaryItem";
import { useRaids } from "../lib/useRaids";

export default function RaidsPage() {
  const navigate = useNavigate();
  const { t } = useTranslation();
  useDocumentTitle(`${t("raids.title")} — LFM`);
  const { user } = useAuth();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const isDesktop = useMediaQuery(theme.breakpoints.up("md"));
  const { data: guildHome } = useGuildHome();
  const battleNetId = user?.battleNetId ?? null;

  const {
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
    passedRaids,
    showPassed,
    handleTogglePassed,
    handleRaidUpdate,
    handleToggleRaid,
    handleSelectRaid,
    handlePageChange,
    handleRaidDelete,
    refresh,
    sortOrder,
    handleSortChange,
  } = useRaids(battleNetId, isDesktop, isMobile);

  const { showSuccess } = useToast();
  const handleRefresh = () => { refresh(); showSuccess(t("common.refreshed")); };

  const handleRaidEdit = (raidId: string) => {
    const params = new URLSearchParams();
    if (currentPage > 1) params.set("page", String(currentPage));
    const query = params.toString();
    navigate(`/raids/${encodeURIComponent(raidId)}/edit${query ? `?${query}` : ""}`);
  };

  const pagination = !loading && totalPages > 1 && (
    <Box
      component="nav"
      aria-label={t("raids.pagination")}
      sx={{ mt: 2, display: "flex", justifyContent: "center", gap: 1, flexWrap: "wrap" }}
    >
      <Button size="small" variant="outlined" disabled={currentPage === 1} onClick={() => handlePageChange(currentPage - 1)} aria-label={t("common.previous")} sx={{ minWidth: 36 }}>
        ‹
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
      <Button size="small" variant="outlined" disabled={currentPage === totalPages} onClick={() => handlePageChange(currentPage + 1)} aria-label={t("common.next")} sx={{ minWidth: 36 }}>
        ›
      </Button>
    </Box>
  );

  return (
    <PageContainer maxWidth={isDesktop ? 1280 : undefined}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2, gap: 2 }}>
        <Typography component="h1" variant="h5">{t("raids.title")}</Typography>
        <Box sx={{ display: "flex", gap: 1, alignItems: "center" }}>
          <IconButton onClick={handleRefresh} disabled={loading} aria-label={t("common.refresh")}>
            <RefreshIcon />
          </IconButton>
          <FormControl size="small" sx={{ minWidth: 140 }}>
            <InputLabel id="raids-sort-label">{t("raids.sort")}</InputLabel>
            <Select
              labelId="raids-sort-label"
              value={sortOrder}
              label={t("raids.sort")}
              onChange={(e) => handleSortChange(e.target.value as "asc" | "desc")}
            >
              <MenuItem value="desc">{t("raids.sortNewest")}</MenuItem>
              <MenuItem value="asc">{t("raids.sortOldest")}</MenuItem>
            </Select>
          </FormControl>
          <Button variant="contained" onClick={() => navigate("/raids/new")}>{t("raids.createButton")}</Button>
        </Box>
      </Box>

      {error && <ErrorState message={error} onRetry={refresh} />}

      {loading ? (
        <LoadingState />
      ) : raids.length === 0 ? (
        <EmptyState
          icon={<EventBusyIcon />}
          message={t("raids.empty")}
          action={{ label: t("raids.emptyCta"), onClick: () => navigate("/raids/new") }}
        />
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
            {passedRaids.length > 0 && (
              <>
                <Divider />
                <Box
                  component="button"
                  onClick={handleTogglePassed}
                  aria-expanded={showPassed}
                  aria-controls="passed-raids-section"
                  sx={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    width: "100%",
                    p: 1.5,
                    border: "none",
                    borderRadius: 0,
                    cursor: "pointer",
                    textAlign: "left",
                    bgcolor: "transparent",
                    color: "text.secondary",
                    "&:hover": { bgcolor: "action.hover" },
                    "&:focus-visible": { outline: "2px solid", outlineColor: "primary.main", outlineOffset: -2 },
                    transition: "background-color 0.15s",
                  }}
                >
                  <Typography component="span" variant="body2" fontWeight={600}>
                    {t("raids.passed", { count: passedRaids.length })}
                  </Typography>
                  <Typography component="span" variant="body2">{showPassed ? "▾" : "▸"}</Typography>
                </Box>
                {showPassed && (
                  <Box id="passed-raids-section" role="region" aria-label={t("raids.passedToggle")}>
                    {passedRaids.map((raid, index) => (
                      <Box key={raid.id}>
                        {index > 0 && <Divider />}
                        <RaidSummaryItem
                          raid={raid}
                          modeLabel={resolveInstanceModeLabel(instances, raid.instanceId, raid.modeKey)}
                          selected={raid.id === requestedRaidId}
                          onClick={() => handleSelectRaid(raid.id)}
                          guildTimezone={guildHome?.setup.timezone}
                          passed
                        />
                      </Box>
                    ))}
                  </Box>
                )}
              </>
            )}
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
                currentBattleNetId={battleNetId}
                canDeleteGuildRaids={guildHome?.memberPermissions.canDeleteGuildRaids ?? false}
                canCreateGuildRaids={guildHome?.memberPermissions.canCreateGuildRaids ?? false}
                onRaidDelete={handleRaidDelete}
                onRaidEdit={handleRaidEdit}
              />
            ) : (
              <Typography color="text.secondary">{t("raids.selectPrompt")}</Typography>
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
              currentBattleNetId={battleNetId}
              canDeleteGuildRaids={guildHome?.memberPermissions.canDeleteGuildRaids ?? false}
              canCreateGuildRaids={guildHome?.memberPermissions.canCreateGuildRaids ?? false}
              onRaidDelete={handleRaidDelete}
              onRaidEdit={handleRaidEdit}
            />
          ))}
          {pagination}
          {passedRaids.length > 0 && (
            <>
              <Button
                variant="text"
                fullWidth
                onClick={handleTogglePassed}
                aria-expanded={showPassed}
                aria-controls="passed-raids-mobile"
                sx={{ color: "text.secondary", justifyContent: "space-between" }}
              >
                <Typography variant="body2" fontWeight={600}>
                  {t("raids.passed", { count: passedRaids.length })}
                </Typography>
                <Typography component="span" variant="body2">{showPassed ? "▾" : "▸"}</Typography>
              </Button>
              {showPassed && (
                <Box id="passed-raids-mobile" role="region" aria-label={t("raids.passedToggle")} sx={{ display: "grid", gap: 3, opacity: 0.7 }}>
                  {passedRaids.map((raid) => (
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
                      currentBattleNetId={battleNetId}
                      canDeleteGuildRaids={guildHome?.memberPermissions.canDeleteGuildRaids ?? false}
                      canCreateGuildRaids={guildHome?.memberPermissions.canCreateGuildRaids ?? false}
                      onRaidDelete={handleRaidDelete}
                      onRaidEdit={handleRaidEdit}
                    />
                  ))}
                </Box>
              )}
            </>
          )}
        </Box>
      )}
    </PageContainer>
  );
}
