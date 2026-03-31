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
import { useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import PageContainer from "../../../components/layout/PageContainer";
import { resolveInstanceModeLabel } from "../../../lib/wow/instances";
import { useAuth } from "../../auth";
import { useGuildHome } from "../../guild/lib/useGuildHome";
import RaidListCard from "../components/RaidListCard";
import RaidSummaryItem from "../components/RaidSummaryItem";
import { useRaids } from "../lib/useRaids";

export default function RaidsPage() {
  const navigate = useNavigate();
  const { t } = useTranslation();
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
    handleRaidUpdate,
    handleToggleRaid,
    handleSelectRaid,
    handlePageChange,
  } = useRaids(battleNetId, isDesktop, isMobile);

  const pagination = !loading && totalPages > 1 && (
    <Box
      component="nav"
      aria-label={t("raids.pagination")}
      sx={{ mt: 2, display: "flex", justifyContent: "center", gap: 1, flexWrap: "wrap" }}
    >
      <Button size="small" variant="outlined" disabled={currentPage === 1} onClick={() => handlePageChange(currentPage - 1)}>
        {t("common.previous")}
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
        {t("common.next")}
      </Button>
    </Box>
  );

  return (
    <PageContainer maxWidth={isDesktop ? 1280 : undefined}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2, gap: 2 }}>
        <Typography component="h1" variant="h5">{t("raids.title")}</Typography>
        <Button variant="contained" onClick={() => navigate("/raids/new")}>{t("raids.createButton")}</Button>
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}>
          <CircularProgress />
        </Box>
      ) : raids.length === 0 ? (
        <Typography color="text.secondary">{t("raids.empty")}</Typography>
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
            />
          ))}
          {pagination}
        </Box>
      )}
    </PageContainer>
  );
}
