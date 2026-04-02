import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import { Typography } from "@mui/material";
import LoadingState from "../../../components/LoadingState";
import { useToast } from "../../../components/useToast";
import DOMPurify from "dompurify";
import api from "../../../lib/api";
import { normalizeWowInstances, type WowInstance } from "../../../lib/wow/instances";
import PageContainer from "../../../components/layout/PageContainer";
import useDocumentTitle from "../../../hooks/useDocumentTitle";
import { useGuildHome } from "../../guild/lib/useGuildHome";
import RunForm, { type CreateRunFormValues, type RunFormInitialValues } from "../components/RunForm";

const EMPTY_INITIAL: RunFormInitialValues = {
  instanceId: "",
  startTime: null,
  signupCloseTime: null,
  description: "",
  selectedModeKey: "",
  visibility: "PUBLIC",
};

export default function CreateRunPage() {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { showSuccess } = useToast();
  useDocumentTitle(`${t("createRun.title")} — LFM`);
  const { data: guildHome } = useGuildHome();
  const [instances, setInstances] = useState<WowInstance[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.get<WowInstance[]>("/instances")
      .then((res) => setInstances(normalizeWowInstances(res.data)))
      .catch(() => setError("createRun.loadInstancesFailed"))
      .finally(() => setLoading(false));
  }, []);

  const handleSubmit = async (values: CreateRunFormValues) => {
    const sanitizedDescription = DOMPurify.sanitize(values.description, {
      ALLOWED_TAGS: [],
      ALLOWED_ATTR: [],
    });
    setSubmitting(true);
    setError(null);
    try {
      const res = await api.post<{ id: string }>("/runs", {
        ...values,
        description: sanitizedDescription,
      });
      showSuccess(t("createRun.createSuccess"));
      navigate(`/runs?run=${encodeURIComponent(res.data.id)}`);
    } catch {
      setError("createRun.createFailed");
      setSubmitting(false);
    }
  };

  if (loading) return <PageContainer maxWidth={600}><LoadingState /></PageContainer>;

  return (
    <PageContainer maxWidth={600}>
      <Typography variant="h5" component="h1" gutterBottom>{t("createRun.title")}</Typography>
      <RunForm
        initialValues={EMPTY_INITIAL}
        instances={instances}
        locale={guildHome?.setup.locale}
        timezone={guildHome?.setup.timezone}
        canCreateGuildRuns={guildHome?.memberPermissions.canCreateGuildRuns ?? false}
        onSubmit={handleSubmit}
        submitting={submitting}
        error={error}
        onCancel={() => navigate("/runs")}
        submitLabel={t("createRun.submit")}
        mode="create"
      />
    </PageContainer>
  );
}
