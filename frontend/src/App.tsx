import { Suspense, lazy, type ReactNode } from "react";
import { Routes, Route } from "react-router";
import { CircularProgress, Stack } from "@mui/material";
import AppLayout from "./components/layout/AppLayout";
import PageContainer from "./components/layout/PageContainer";
import AuthGuard from "./features/auth/components/AuthGuard";
import GuildSetupGuard from "./features/guild/components/GuildSetupGuard";
import LandingPage from "./features/auth/pages/LandingPage";
import LoginPage from "./features/auth/pages/LoginPage";
import LoginFailedPage from "./features/auth/pages/LoginFailedPage";
import LoginSuccessPage from "./features/auth/pages/LoginSuccessPage";
import GoodbyePage from "./features/auth/pages/GoodbyePage";

const CharactersPage = lazy(() => import("./features/characters/pages/CharactersPage"));
const GuildPage = lazy(() => import("./features/guild/pages/GuildPage"));
const GuildAdminPage = lazy(() => import("./features/guild/pages/GuildAdminPage"));
const RaidsPage = lazy(() => import("./features/raids/pages/RaidsPage"));
const CreateRaidPage = lazy(() => import("./features/raids/pages/CreateRaidPage"));

function RouteFallback() {
  return (
    <PageContainer>
      <Stack direction="row" spacing={1.5} alignItems="center">
        <CircularProgress size={20} />
      </Stack>
    </PageContainer>
  );
}

function withRouteFallback(node: ReactNode) {
  return <Suspense fallback={<RouteFallback />}>{node}</Suspense>;
}

export default function App() {
  return (
    <AppLayout>
      <Routes>
        <Route path="/" element={withRouteFallback(<LandingPage />)} />
        <Route path="/login" element={withRouteFallback(<LoginPage />)} />
        <Route path="/login/failed" element={withRouteFallback(<LoginFailedPage />)} />
        <Route path="/login/success" element={withRouteFallback(<LoginSuccessPage />)} />
        <Route path="/goodbye" element={withRouteFallback(<GoodbyePage />)} />
        <Route path="/characters" element={withRouteFallback(<AuthGuard><CharactersPage /></AuthGuard>)} />
        <Route path="/guild" element={withRouteFallback(<AuthGuard><GuildPage /></AuthGuard>)} />
        <Route path="/guild/admin" element={withRouteFallback(<AuthGuard><GuildAdminPage /></AuthGuard>)} />
        <Route path="/raids" element={withRouteFallback(<AuthGuard><GuildSetupGuard><RaidsPage /></GuildSetupGuard></AuthGuard>)} />
        <Route path="/raids/new" element={withRouteFallback(<AuthGuard><GuildSetupGuard><CreateRaidPage /></GuildSetupGuard></AuthGuard>)} />
      </Routes>
    </AppLayout>
  );
}
