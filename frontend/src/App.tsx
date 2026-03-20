import { Routes, Route } from "react-router";
import AppLayout from "./components/layout/AppLayout";
import { AuthGuard, LandingPage, LoginPage, LoginFailedPage, LoginSuccessPage } from "./features/auth";
import { CharactersPage } from "./features/characters";
import { RaidsPage, CreateRaidPage } from "./features/raids";

export default function App() {
  return (
    <AppLayout>
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/login/failed" element={<LoginFailedPage />} />
        <Route path="/login/success" element={<LoginSuccessPage />} />
        <Route path="/characters" element={<AuthGuard><CharactersPage /></AuthGuard>} />
        <Route path="/raids" element={<AuthGuard><RaidsPage /></AuthGuard>} />
        <Route path="/raids/new" element={<AuthGuard><CreateRaidPage /></AuthGuard>} />
      </Routes>
    </AppLayout>
  );
}
