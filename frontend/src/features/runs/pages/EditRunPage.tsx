import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { Alert, Typography } from "@mui/material";
import LoadingState from "../../../components/LoadingState";
import { useToast } from "../../../components/useToast";
import DOMPurify from "dompurify";
import { DateTime } from "luxon";
import api from "../../../lib/api";
import { normalizeWowInstances, toModeKey, type WowInstance } from "../../../lib/wow/instances";
import PageContainer from "../../../components/layout/PageContainer";
import useDocumentTitle from "../../../hooks/useDocumentTitle";
import { useGuildHome } from "../../guild/lib/useGuildHome";
import { getLockedFields } from "../lib/runEditability";
import RunForm, { type EditRunFormValues, type RunFormInitialValues } from "../components/RunForm";
import type { Run } from "../lib/runTypes";

export default function EditRunPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { showSuccess } = useToast();
  useDocumentTitle(`${t("editRun.title")} — LFM`);
  const { data: guildHome } = useGuildHome();

  const [run, setRun] = useState<Run | null>(null);
  const [instances, setInstances] = useState<WowInstance[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    Promise.all([
      api.get<Run>(`/runs/${encodeURIComponent(id)}`),
      api.get<WowInstance[]>("/instances"),
    ])
      .then(([runRes, instancesRes]) => {
        setRun(runRes.data);
        setInstances(normalizeWowInstances(instancesRes.data));
      })
      .catch(() => setError("editRun.loadFailed"))
      .finally(() => setLoading(false));
  }, [id]);

  const handleSubmit = async (values: EditRunFormValues) => {
    if (!id) return;
    const sanitizedDescription = DOMPurify.sanitize(values.description, {
      ALLOWED_TAGS: [],
      ALLOWED_ATTR: [],
    });
    setSubmitting(true);
    setError(null);
    try {
      await api.put(`/runs/${encodeURIComponent(id)}`, {
        ...values,
        description: sanitizedDescription,
      });
      showSuccess(t("editRun.saveSuccess"));
      const params = new URLSearchParams(window.location.search);
      const returnPage = params.get("page");
      const returnParams = new URLSearchParams();
      returnParams.set("run", id);
      if (returnPage) returnParams.set("page", returnPage);
      navigate(`/runs?${returnParams.toString()}`);
    } catch {
      setError("editRun.saveFailed");
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <PageContainer maxWidth={600}>
        <LoadingState />
      </PageContainer>
    );
  }

  if (!run) {
    return (
      <PageContainer maxWidth={600}>
        <Alert severity="error">{t("editRun.notFound")}</Alert>
      </PageContainer>
    );
  }

  const timezone = guildHome?.setup.timezone ?? "UTC";
  const lockedFields = getLockedFields(run.runCharacters.length);
  const lockReason = lockedFields.size > 0 ? t("editRun.lockedField") : undefined;

  const selectedInstance = instances.find((i) => i.id === run.instanceId);
  const modeKey = selectedInstance?.modes
    ? (selectedInstance.modes.map(toModeKey).find((k) => k === run.modeKey) ?? run.modeKey)
    : run.modeKey;

  const initialValues: RunFormInitialValues = {
    instanceId: run.instanceId,
    startTime: run.startTime ? DateTime.fromISO(run.startTime, { zone: "UTC" }).setZone(timezone) : null,
    signupCloseTime: run.signupCloseTime ? DateTime.fromISO(run.signupCloseTime, { zone: "UTC" }).setZone(timezone) : null,
    description: run.description,
    selectedModeKey: modeKey,
    visibility: run.visibility,
  };

  return (
    <PageContainer maxWidth={600}>
      <Typography variant="h5" component="h1" gutterBottom>{t("editRun.title")}</Typography>
      <RunForm
        initialValues={initialValues}
        instances={instances}
        locale={guildHome?.setup.locale}
        timezone={timezone}
        canCreateGuildRuns={guildHome?.memberPermissions.canCreateGuildRuns ?? false}
        onSubmit={handleSubmit}
        submitting={submitting}
        error={error}
        onCancel={() => navigate(`/runs?run=${encodeURIComponent(id!)}`)}
        submitLabel={t("editRun.submit")}
        mode="edit"
        lockedFields={lockedFields}
        lockReason={lockReason}
      />
    </PageContainer>
  );
}
