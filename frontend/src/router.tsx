import { Suspense, lazy, type ReactNode } from "react";
import { createBrowserRouter, createRoutesFromElements, Route, Outlet } from "react-router";
import { CircularProgress, Stack } from "@mui/material";
import { useTranslation } from "react-i18next";
import AppLayout from "./components/layout/AppLayout";
import PageContainer from "./components/layout/PageContainer";
import { AuthProvider } from "./features/auth";
import AuthGuard from "./features/auth/components/AuthGuard";
import GuildSetupGuard from "./features/guild/components/GuildSetupGuard";

const LandingPage = lazy(() => import("./features/auth/pages/LandingPage"));
const LoginPage = lazy(() => import("./features/auth/pages/LoginPage"));
const LoginFailedPage = lazy(() => import("./features/auth/pages/LoginFailedPage"));
const LoginSuccessPage = lazy(() => import("./features/auth/pages/LoginSuccessPage"));
const GoodbyePage = lazy(() => import("./features/auth/pages/GoodbyePage"));
const PrivacyPolicyPage = lazy(() => import("./features/auth/pages/PrivacyPolicyPage"));
const CharactersPage = lazy(() => import("./features/characters/pages/CharactersPage"));
const GuildPage = lazy(() => import("./features/guild/pages/GuildPage"));
const GuildAdminPage = lazy(() => import("./features/guild/pages/GuildAdminPage"));
const RaidsPage = lazy(() => import("./features/raids/pages/RaidsPage"));
const CreateRaidPage = lazy(() => import("./features/raids/pages/CreateRaidPage"));
const EditRaidPage = lazy(() => import("./features/raids/pages/EditRaidPage"));

function RouteFallback() {
  const { t } = useTranslation();
  return (
    <PageContainer>
      <Stack direction="row" spacing={1.5} alignItems="center">
        <CircularProgress size={20} aria-label={t("common.loading")} />
      </Stack>
    </PageContainer>
  );
}

function withRouteFallback(node: ReactNode) {
  return <Suspense fallback={<RouteFallback />}>{node}</Suspense>;
}

function RootLayout() {
  return (
    <AuthProvider>
      <AppLayout>
        <Outlet />
      </AppLayout>
    </AuthProvider>
  );
}

export const router = createBrowserRouter(
  createRoutesFromElements(
    <Route element={<RootLayout />}>
      <Route path="/" element={withRouteFallback(<LandingPage />)} />
      <Route path="/login" element={withRouteFallback(<LoginPage />)} />
      <Route path="/login/failed" element={withRouteFallback(<LoginFailedPage />)} />
      <Route path="/login/success" element={withRouteFallback(<LoginSuccessPage />)} />
      <Route path="/goodbye" element={withRouteFallback(<GoodbyePage />)} />
      <Route path="/privacy" element={withRouteFallback(<PrivacyPolicyPage />)} />
      <Route path="/characters" element={withRouteFallback(<AuthGuard><CharactersPage /></AuthGuard>)} />
      <Route path="/guild" element={withRouteFallback(<AuthGuard><GuildPage /></AuthGuard>)} />
      <Route path="/guild/admin" element={withRouteFallback(<AuthGuard><GuildAdminPage /></AuthGuard>)} />
      <Route path="/raids" element={withRouteFallback(<AuthGuard><GuildSetupGuard><RaidsPage /></GuildSetupGuard></AuthGuard>)} />
      <Route path="/raids/new" element={withRouteFallback(<AuthGuard><GuildSetupGuard><CreateRaidPage /></GuildSetupGuard></AuthGuard>)} />
      <Route path="/raids/:id/edit" element={withRouteFallback(<AuthGuard><GuildSetupGuard><EditRaidPage /></GuildSetupGuard></AuthGuard>)} />
    </Route>
  )
);
