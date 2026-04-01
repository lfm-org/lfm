import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import { Typography } from "@mui/material";
import DOMPurify from "dompurify";
import api from "../../../lib/api";
import { normalizeWowInstances, type WowInstance } from "../../../lib/wow/instances";
import PageContainer from "../../../components/layout/PageContainer";
import { useGuildHome } from "../../guild/lib/useGuildHome";
import RaidForm, { type RaidFormValues, type RaidFormInitialValues } from "../components/RaidForm";

const EMPTY_INITIAL: RaidFormInitialValues = {
  instanceId: "",
  startTime: null,
  signupCloseTime: null,
  description: "",
  selectedModeKey: "",
  visibility: "PUBLIC",
};

export default function CreateRaidPage() {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { data: guildHome } = useGuildHome();
  const [instances, setInstances] = useState<WowInstance[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.get<WowInstance[]>("/instances")
      .then((res) => setInstances(normalizeWowInstances(res.data)))
      .catch(() => setError("createRaid.loadInstancesFailed"))
      .finally(() => setLoading(false));
  }, []);

  const handleSubmit = async (values: RaidFormValues) => {
    const sanitizedDescription = DOMPurify.sanitize(values.description, {
      ALLOWED_TAGS: [],
      ALLOWED_ATTR: [],
    });
    setSubmitting(true);
    setError(null);
    try {
      const res = await api.post<{ id: string }>("/raids", {
        ...values,
        description: sanitizedDescription,
      });
      navigate(`/raids?raid=${encodeURIComponent(res.data.id)}`);
    } catch {
      setError("createRaid.createFailed");
      setSubmitting(false);
    }
  };

  if (loading) return <Typography sx={{ p: 4 }}>{t("createRaid.loading")}</Typography>;

  return (
    <PageContainer maxWidth={600}>
      <Typography variant="h5" component="h1" gutterBottom>{t("createRaid.title")}</Typography>
      <RaidForm
        initialValues={EMPTY_INITIAL}
        instances={instances}
        locale={guildHome?.setup.locale}
        timezone={guildHome?.setup.timezone}
        canCreateGuildRaids={guildHome?.memberPermissions.canCreateGuildRaids ?? false}
        onSubmit={handleSubmit}
        submitting={submitting}
        error={error}
        onCancel={() => navigate("/raids")}
        submitLabel={t("createRaid.submit")}
        mode="create"
      />
    </PageContainer>
  );
}
