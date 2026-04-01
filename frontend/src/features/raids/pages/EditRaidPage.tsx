import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { Alert, CircularProgress, Typography } from "@mui/material";
import DOMPurify from "dompurify";
import { DateTime } from "luxon";
import api from "../../../lib/api";
import { normalizeWowInstances, toModeKey, type WowInstance } from "../../../lib/wow/instances";
import PageContainer from "../../../components/layout/PageContainer";
import useDocumentTitle from "../../../hooks/useDocumentTitle";
import { useGuildHome } from "../../guild/lib/useGuildHome";
import { getLockedFields } from "../lib/raidEditability";
import RaidForm, { type EditRaidFormValues, type RaidFormInitialValues } from "../components/RaidForm";
import type { Raid } from "../lib/raidTypes";

export default function EditRaidPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t } = useTranslation();
  useDocumentTitle(`${t("editRaid.title")} — LFM`);
  const { data: guildHome } = useGuildHome();

  const [raid, setRaid] = useState<Raid | null>(null);
  const [instances, setInstances] = useState<WowInstance[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    Promise.all([
      api.get<Raid>(`/raids/${encodeURIComponent(id)}`),
      api.get<WowInstance[]>("/instances"),
    ])
      .then(([raidRes, instancesRes]) => {
        setRaid(raidRes.data);
        setInstances(normalizeWowInstances(instancesRes.data));
      })
      .catch(() => setError("editRaid.loadFailed"))
      .finally(() => setLoading(false));
  }, [id]);

  const handleSubmit = async (values: EditRaidFormValues) => {
    if (!id) return;
    const sanitizedDescription = DOMPurify.sanitize(values.description, {
      ALLOWED_TAGS: [],
      ALLOWED_ATTR: [],
    });
    setSubmitting(true);
    setError(null);
    try {
      await api.put(`/raids/${encodeURIComponent(id)}`, {
        ...values,
        description: sanitizedDescription,
      });
      navigate(`/raids?raid=${encodeURIComponent(id)}`);
    } catch {
      setError("editRaid.saveFailed");
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <PageContainer maxWidth={600}>
        <CircularProgress aria-label={t("common.loading")} />
      </PageContainer>
    );
  }

  if (!raid) {
    return (
      <PageContainer maxWidth={600}>
        <Alert severity="error">{t("editRaid.notFound")}</Alert>
      </PageContainer>
    );
  }

  const timezone = guildHome?.setup.timezone ?? "UTC";
  const lockedFields = getLockedFields(raid.raidCharacters.length);
  const lockReason = lockedFields.size > 0 ? t("editRaid.lockedField") : undefined;

  const selectedInstance = instances.find((i) => i.id === raid.instanceId);
  const modeKey = selectedInstance?.modes
    ? (selectedInstance.modes.map(toModeKey).find((k) => k === raid.modeKey) ?? raid.modeKey)
    : raid.modeKey;

  const initialValues: RaidFormInitialValues = {
    instanceId: raid.instanceId,
    startTime: raid.startTime ? DateTime.fromISO(raid.startTime, { zone: "UTC" }).setZone(timezone) : null,
    signupCloseTime: raid.signupCloseTime ? DateTime.fromISO(raid.signupCloseTime, { zone: "UTC" }).setZone(timezone) : null,
    description: raid.description,
    selectedModeKey: modeKey,
    visibility: raid.visibility,
  };

  return (
    <PageContainer maxWidth={600}>
      <Typography variant="h5" component="h1" gutterBottom>{t("editRaid.title")}</Typography>
      <RaidForm
        initialValues={initialValues}
        instances={instances}
        locale={guildHome?.setup.locale}
        timezone={timezone}
        canCreateGuildRaids={guildHome?.memberPermissions.canCreateGuildRaids ?? false}
        onSubmit={handleSubmit}
        submitting={submitting}
        error={error}
        onCancel={() => navigate(`/raids?raid=${encodeURIComponent(id!)}`)}
        submitLabel={t("editRaid.submit")}
        mode="edit"
        lockedFields={lockedFields}
        lockReason={lockReason}
      />
    </PageContainer>
  );
}
