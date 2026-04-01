import { useEffect, useMemo, useState } from "react";
import { useNavigate, useSearchParams } from "react-router";
import { Avatar, Box, Button, CircularProgress, Stack, Typography, useMediaQuery, useTheme } from "@mui/material";
import { useTranslation } from "react-i18next";
import api from "../../../lib/api";
import { useAuth } from "../../auth";
import PageContainer from "../../../components/layout/PageContainer";
import { layout } from "../../../theme";
import { classColor } from "../../../lib/wow/classColors";
import { deleteAccount } from "../../../lib/auth";
import ForgetMeSection from "../components/ForgetMeSection";
import { useCharacters } from "../lib/useCharacters";

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

function parsePageParam(value: string | null): number {
  if (!value) return 1;
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 1;
}

function CharactersPageInner({
  visibleChars,
  totalPages,
  clampedPage,
  characters,
  loading,
  portraits,
  loadingPortraits,
  handlePageChange,
  selectCharacter,
  selectingId,
  deleteConfirmation,
  deleteConfirmationValid,
  deleteError,
  deleting,
  setDeleteConfirmation,
  handleDeleteAccount,
}: {
  visibleChars: AccountCharacter[];
  totalPages: number;
  clampedPage: number;
  characters: AccountCharacter[];
  loading: boolean;
  portraits: Record<string, string>;
  loadingPortraits: boolean;
  handlePageChange: (page: number) => void;
  selectCharacter: (char: AccountCharacter) => void;
  selectingId: string | null;
  deleteConfirmation: string;
  deleteConfirmationValid: boolean;
  deleteError: string | null;
  deleting: boolean;
  setDeleteConfirmation: (value: string) => void;
  handleDeleteAccount: () => void;
}) {
  const { t } = useTranslation();

  if (loading) {
    return (
      <PageContainer>
        <Typography>{t("characters.loading")}</Typography>
      </PageContainer>
    );
  }

  return (
    <PageContainer>
      <Stack spacing={layout.pageGap}>
        <Box>
          <Typography component="h1" variant="h5" gutterBottom>
            {t("characters.title")}
          </Typography>
          {characters.length === 0 && (
            <Typography color="text.secondary">
              {t("characters.empty")}
            </Typography>
          )}
        </Box>

        <Box
          sx={{
            display: "grid",
            gap: layout.componentGap,
            gridTemplateColumns: {
              xs: "1fr",
              sm: "repeat(3, 1fr)",
            },
          }}
        >
          {visibleChars.map((char) => {
            const color = char.classId ? classColor(char.classId) : undefined;
            const charId = `${char.region}-${char.realm}-${char.name.toLowerCase()}`;
            const portraitSrc = char.portraitUrl || portraits[charId];
            const awaitingPortrait = !portraitSrc && loadingPortraits;
            const isSelecting = selectingId === charId;
            return (
              <Button
                key={`${char.realm}-${char.name}`}
                variant="outlined"
                onClick={() => selectCharacter(char)}
                disabled={selectingId !== null}
                sx={{
                  p: 2,
                  minHeight: 120,
                  display: "flex",
                  flexDirection: "column",
                  alignItems: "flex-start",
                  justifyContent: "flex-start",
                  gap: 1,
                  textAlign: "left",
                  bgcolor: "background.paper",
                  borderLeft: color ? `4px solid ${color}` : undefined,
                }}
              >
                <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, width: "100%" }}>
                  <Avatar
                    src={portraitSrc}
                    alt={char.name}
                    sx={{ width: 40, height: 40, flexShrink: 0, bgcolor: color ?? "action.selected" }}
                  >
                    {(awaitingPortrait || isSelecting) && <CircularProgress size={20} color="inherit" />}
                  </Avatar>
                  <Box sx={{ minWidth: 0 }}>
                    <Typography variant="body1" component="span" display="block" noWrap>
                      {char.name}
                    </Typography>
                    <Typography variant="caption" color="text.secondary" display="block" noWrap>
                      {char.realmName}
                    </Typography>
                  </Box>
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary" display="block">
                    {t("characters.level", { level: char.level, className: char.className })}
                  </Typography>
                  {char.specName && (
                    <Typography variant="caption" color="text.secondary" display="block">
                      {char.specName}
                    </Typography>
                  )}
                </Box>
              </Button>
            );
          })}
        </Box>

        {totalPages > 1 && (
          <Box
            component="nav"
            aria-label={t("characters.pagination")}
            sx={{ display: "flex", justifyContent: "center", gap: 1 }}
          >
            <Button size="small" variant="outlined" disabled={clampedPage === 1} onClick={() => handlePageChange(clampedPage - 1)} aria-label={t("common.previous")} sx={{ minWidth: 36 }}>
              ‹
            </Button>
            {Array.from({ length: totalPages }, (_, index) => {
              const page = index + 1;
              return (
                <Button
                  key={page}
                  size="small"
                  variant={page === clampedPage ? "contained" : "outlined"}
                  aria-current={page === clampedPage ? "page" : undefined}
                  onClick={() => handlePageChange(page)}
                >
                  {page}
                </Button>
              );
            })}
            <Button size="small" variant="outlined" disabled={clampedPage === totalPages} onClick={() => handlePageChange(clampedPage + 1)} aria-label={t("common.next")} sx={{ minWidth: 36 }}>
              ›
            </Button>
          </Box>
        )}

        <ForgetMeSection
          deleteConfirmation={deleteConfirmation}
          deleteConfirmationValid={deleteConfirmationValid}
          deleteError={deleteError}
          deleting={deleting}
          onDeleteConfirmationChange={setDeleteConfirmation}
          onDeleteAccount={handleDeleteAccount}
        />
      </Stack>
    </PageContainer>
  );
}

export default function CharactersPage() {
  const { t } = useTranslation();
  const [deleteConfirmation, setDeleteConfirmation] = useState("");
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [selectingId, setSelectingId] = useState<string | null>(null);
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const { onAccountDeleted, onCharacterSelected } = useAuth();
  const theme = useTheme();
  const isDesktop = useMediaQuery(theme.breakpoints.up("sm"));
  const PAGE_SIZE = isDesktop ? 9 : 3;
  const deleteConfirmationValid = deleteConfirmation === "FORGET ME";

  const redirectPath = (() => {
    const requested = searchParams.get("redirect");
    return requested && requested.startsWith("/") ? requested : "/raids";
  })();

  const currentPage = parsePageParam(searchParams.get("page"));

  // visibleCharsForPortraits is derived from characters (from hook), then fed back in.
  // On initial render characters is [], so visibleCharsForPortraits is [].
  // After characters load, the hook's portrait effect sees the real visible slice.
  const [charactersForPagination, setCharactersForPagination] = useState<AccountCharacter[]>([]);
  const totalPagesForPortrait = Math.max(1, Math.ceil(charactersForPagination.length / PAGE_SIZE));
  const clampedPageForPortrait = Math.min(Math.max(currentPage, 1), totalPagesForPortrait);
  const visibleCharsForPortraits = useMemo(
    () => charactersForPagination.slice((clampedPageForPortrait - 1) * PAGE_SIZE, clampedPageForPortrait * PAGE_SIZE),
    [charactersForPagination, clampedPageForPortrait, PAGE_SIZE]
  );

  const { characters, loading, portraits, loadingPortraits } = useCharacters(visibleCharsForPortraits);

  useEffect(() => {
    setCharactersForPagination(characters);
  }, [characters]);

  const totalPages = Math.max(1, Math.ceil(characters.length / PAGE_SIZE));
  const clampedPage = Math.min(Math.max(currentPage, 1), totalPages);
  const visibleChars = useMemo(
    () => characters.slice((clampedPage - 1) * PAGE_SIZE, clampedPage * PAGE_SIZE),
    [characters, clampedPage, PAGE_SIZE]
  );

  const handlePageChange = (page: number) => {
    const next = new URLSearchParams(searchParams);
    if (page <= 1) {
      next.delete("page");
    } else {
      next.set("page", String(page));
    }
    setSearchParams(next);
  };

  const selectCharacter = async (char: AccountCharacter) => {
    const charId = `${char.region}-${char.realm}-${char.name.toLowerCase()}`;
    setSelectingId(charId);
    try {
      const res = await api.post<{ selectedCharacterId: string }>("/raider/character", {
        region: char.region,
        realm: char.realm,
        name: char.name,
      });
      onCharacterSelected(res.data.selectedCharacterId);
      navigate(redirectPath);
    } finally {
      setSelectingId(null);
    }
  };

  const handleDeleteAccount = async () => {
    if (!deleteConfirmationValid || deleting) return;

    setDeleting(true);
    setDeleteError(null);
    try {
      await deleteAccount();
      onAccountDeleted();
    } catch {
      setDeleteError(t("characters.deleteError"));
    } finally {
      setDeleting(false);
    }
  };

  return (
    <CharactersPageInner
      visibleChars={visibleChars}
      totalPages={totalPages}
      clampedPage={clampedPage}
      characters={characters}
      loading={loading}
      portraits={portraits}
      loadingPortraits={loadingPortraits}
      handlePageChange={handlePageChange}
      selectCharacter={selectCharacter}
      selectingId={selectingId}
      deleteConfirmation={deleteConfirmation}
      deleteConfirmationValid={deleteConfirmationValid}
      deleteError={deleteError}
      deleting={deleting}
      setDeleteConfirmation={setDeleteConfirmation}
      handleDeleteAccount={handleDeleteAccount}
    />
  );
}
