import { Routes, Route } from "react-router";
import AppLayout from "./components/layout/AppLayout";
import { AuthGuard, GoodbyePage, LandingPage, LoginPage, LoginFailedPage, LoginSuccessPage } from "./features/auth";
import { CharactersPage } from "./features/characters";
import { GuildAdminPage, GuildPage, GuildSetupGuard } from "./features/guild";
import { RaidsPage, CreateRaidPage } from "./features/raids";

export default function App() {
  return (
    <AppLayout>
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/login/failed" element={<LoginFailedPage />} />
        <Route path="/login/success" element={<LoginSuccessPage />} />
        <Route path="/goodbye" element={<GoodbyePage />} />
        <Route path="/characters" element={<AuthGuard><CharactersPage /></AuthGuard>} />
        <Route path="/guild" element={<AuthGuard><GuildPage /></AuthGuard>} />
        <Route path="/guild/admin" element={<AuthGuard><GuildAdminPage /></AuthGuard>} />
        <Route path="/raids" element={<AuthGuard><GuildSetupGuard><RaidsPage /></GuildSetupGuard></AuthGuard>} />
        <Route path="/raids/new" element={<AuthGuard><GuildSetupGuard><CreateRaidPage /></GuildSetupGuard></AuthGuard>} />
      </Routes>
    </AppLayout>
  );
}
