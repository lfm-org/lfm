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
import LoadingState from "../../../components/LoadingState";
import ErrorState from "../../../components/ErrorState";
import EmptyState from "../../../components/EmptyState";
import PageContainer from "../../../components/layout/PageContainer";
import useDocumentTitle from "../../../hooks/useDocumentTitle";
import { resolveInstanceModeLabel } from "../../../lib/wow/instances";
import { useAuth } from "../../auth";
import { useGuildHome } from "../../guild/lib/useGuildHome";
import RunListCard from "../components/RunListCard";
import RunSummaryItem from "../components/RunSummaryItem";
import { useRuns } from "../lib/useRuns";

export default function RunsPage() {
  const navigate = useNavigate();
  const { t } = useTranslation();
  useDocumentTitle(`${t("runs.title")} — LFM`);
  const { user } = useAuth();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const isDesktop = useMediaQuery(theme.breakpoints.up("md"));
  const { data: guildHome } = useGuildHome();
  const battleNetId = user?.battleNetId ?? null;

  const {
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
    passedRuns,
    showPassed,
    handleTogglePassed,
    handleRunUpdate,
    handleToggleRun,
    handleSelectRun,
    handlePageChange,
    handleRunDelete,
    refresh,
    sortOrder,
    handleSortChange,
  } = useRuns(battleNetId, isDesktop, isMobile);

  const handleRefresh = () => { refresh(); };

  const handleRunEdit = (runId: string) => {
    const params = new URLSearchParams();
    if (currentPage > 1) params.set("page", String(currentPage));
    const query = params.toString();
    navigate(`/runs/${encodeURIComponent(runId)}/edit${query ? `?${query}` : ""}`);
  };

  const pagination = !loading && totalPages > 1 && (
    <Box
      component="nav"
      aria-label={t("runs.pagination")}
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
        <Typography component="h1" variant="h5">{t("runs.title")}</Typography>
        <Box sx={{ display: "flex", gap: 1, alignItems: "center" }}>
          <IconButton onClick={handleRefresh} disabled={loading} aria-label={t("common.refresh")}>
            <RefreshIcon />
          </IconButton>
          <FormControl size="small" sx={{ minWidth: 140 }}>
            <InputLabel id="runs-sort-label">{t("runs.sort")}</InputLabel>
            <Select
              labelId="runs-sort-label"
              value={sortOrder}
              label={t("runs.sort")}
              onChange={(e) => handleSortChange(e.target.value as "asc" | "desc")}
            >
              <MenuItem value="desc">{t("runs.sortNewest")}</MenuItem>
              <MenuItem value="asc">{t("runs.sortOldest")}</MenuItem>
            </Select>
          </FormControl>
          <Button variant="contained" onClick={() => navigate("/runs/new")}>{t("runs.createButton")}</Button>
        </Box>
      </Box>

      {error && <ErrorState message={t(error)} onRetry={refresh} />}

      {loading ? (
        <LoadingState />
      ) : runs.length === 0 ? (
        <EmptyState
          icon={<EventBusyIcon />}
          message={t("runs.empty")}
          action={{ label: t("runs.emptyCta"), onClick: () => navigate("/runs/new") }}
        />
      ) : isDesktop ? (
        /* Desktop: two-column layout */
        <Box sx={{ display: "flex", gap: 3, alignItems: "flex-start" }}>
          {/* Left panel: run list */}
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
            {visibleRuns.map((run, index) => (
              <Box key={run.id}>
                {index > 0 && <Divider />}
                <RunSummaryItem
                  run={run}
                  modeLabel={resolveInstanceModeLabel(instances, run.instanceId, run.modeKey)}
                  selected={run.id === requestedRunId}
                  onClick={() => handleSelectRun(run.id)}
                  guildTimezone={guildHome?.setup.timezone}
                />
              </Box>
            ))}
            {pagination && <Box sx={{ borderTop: "1px solid", borderColor: "divider", p: 1 }}>{pagination}</Box>}
            {passedRuns.length > 0 && (
              <>
                <Divider />
                <Box
                  component="button"
                  onClick={handleTogglePassed}
                  aria-expanded={showPassed}
                  aria-controls="passed-runs-section"
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
                    {t("runs.passed", { count: passedRuns.length })}
                  </Typography>
                  <Typography component="span" variant="body2">{showPassed ? "▾" : "▸"}</Typography>
                </Box>
                {showPassed && (
                  <Box id="passed-runs-section" role="region" aria-label={t("runs.passedToggle")}>
                    {passedRuns.map((run, index) => (
                      <Box key={run.id}>
                        {index > 0 && <Divider />}
                        <RunSummaryItem
                          run={run}
                          modeLabel={resolveInstanceModeLabel(instances, run.instanceId, run.modeKey)}
                          selected={run.id === requestedRunId}
                          onClick={() => handleSelectRun(run.id)}
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

          {/* Right panel: selected run details */}
          <Box sx={{ flex: 1, minWidth: 0 }}>
            {selectedRun ? (
              <RunListCard
                key={selectedRun.id}
                run={selectedRun}
                modeLabel={resolveInstanceModeLabel(instances, selectedRun.instanceId, selectedRun.modeKey)}
                isMobile={false}
                isExpanded={true}
                onToggle={() => {}}
                onRunUpdate={handleRunUpdate}
                characters={characters}
                selectedCharacterId={selectedCharacterId}
                loadingChars={loadingChars}
                charactersError={charactersError}
                guildTimezone={guildHome?.setup.timezone}
                canSignupToGuildRuns={guildHome?.memberPermissions.canSignupGuildRuns ?? false}
                currentBattleNetId={battleNetId}
                canDeleteGuildRuns={guildHome?.memberPermissions.canDeleteGuildRuns ?? false}
                canCreateGuildRuns={guildHome?.memberPermissions.canCreateGuildRuns ?? false}
                onRunDelete={handleRunDelete}
                onRunEdit={handleRunEdit}
              />
            ) : (
              <Typography color="text.secondary">{t("runs.selectPrompt")}</Typography>
            )}
          </Box>
        </Box>
      ) : (
        /* Mobile: stacked cards */
        <Box sx={{ display: "grid", gap: 3 }}>
          {visibleRuns.map((run) => (
            <RunListCard
              key={run.id}
              run={run}
              modeLabel={resolveInstanceModeLabel(instances, run.instanceId, run.modeKey)}
              isMobile={isMobile}
              isExpanded={Boolean(expandedRuns[run.id])}
              onToggle={() => handleToggleRun(run.id)}
              onRunUpdate={handleRunUpdate}
              characters={characters}
              selectedCharacterId={selectedCharacterId}
              loadingChars={loadingChars}
              charactersError={charactersError}
              guildTimezone={guildHome?.setup.timezone}
              canSignupToGuildRuns={guildHome?.memberPermissions.canSignupGuildRuns ?? false}
              currentBattleNetId={battleNetId}
              canDeleteGuildRuns={guildHome?.memberPermissions.canDeleteGuildRuns ?? false}
              canCreateGuildRuns={guildHome?.memberPermissions.canCreateGuildRuns ?? false}
              onRunDelete={handleRunDelete}
              onRunEdit={handleRunEdit}
            />
          ))}
          {pagination}
          {passedRuns.length > 0 && (
            <>
              <Button
                variant="text"
                fullWidth
                onClick={handleTogglePassed}
                aria-expanded={showPassed}
                aria-controls="passed-runs-mobile"
                sx={{ color: "text.secondary", justifyContent: "space-between" }}
              >
                <Typography variant="body2" fontWeight={600}>
                  {t("runs.passed", { count: passedRuns.length })}
                </Typography>
                <Typography component="span" variant="body2">{showPassed ? "▾" : "▸"}</Typography>
              </Button>
              {showPassed && (
                <Box id="passed-runs-mobile" role="region" aria-label={t("runs.passedToggle")} sx={{ display: "grid", gap: 3, opacity: 0.7 }}>
                  {passedRuns.map((run) => (
                    <RunListCard
                      key={run.id}
                      run={run}
                      modeLabel={resolveInstanceModeLabel(instances, run.instanceId, run.modeKey)}
                      isMobile={isMobile}
                      isExpanded={Boolean(expandedRuns[run.id])}
                      onToggle={() => handleToggleRun(run.id)}
                      onRunUpdate={handleRunUpdate}
                      characters={characters}
                      selectedCharacterId={selectedCharacterId}
                      loadingChars={loadingChars}
                      charactersError={charactersError}
                      guildTimezone={guildHome?.setup.timezone}
                      canSignupToGuildRuns={guildHome?.memberPermissions.canSignupGuildRuns ?? false}
                      currentBattleNetId={battleNetId}
                      canDeleteGuildRuns={guildHome?.memberPermissions.canDeleteGuildRuns ?? false}
                      canCreateGuildRuns={guildHome?.memberPermissions.canCreateGuildRuns ?? false}
                      onRunDelete={handleRunDelete}
                      onRunEdit={handleRunEdit}
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
