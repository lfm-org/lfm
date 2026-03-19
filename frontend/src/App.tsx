import { Routes, Route } from "react-router";
import Layout from "./components/Layout";
import LandingPage from "./pages/LandingPage";
import LoginPage from "./pages/LoginPage";
import LoginFailedPage from "./pages/LoginFailedPage";
import LoginSuccessPage from "./pages/LoginSuccessPage";
import CharactersPage from "./pages/CharactersPage";
import RaidsPage from "./pages/RaidsPage";
import CreateRaidPage from "./pages/CreateRaidPage";
import AuthGuard from "./components/AuthGuard";

export default function App() {
  return (
    <Layout>
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/login/failed" element={<LoginFailedPage />} />
        <Route path="/login/success" element={<LoginSuccessPage />} />
        <Route path="/characters" element={<AuthGuard><CharactersPage /></AuthGuard>} />
        <Route path="/raids" element={<AuthGuard><RaidsPage /></AuthGuard>} />
        <Route path="/raids/new" element={<AuthGuard><CreateRaidPage /></AuthGuard>} />
      </Routes>
    </Layout>
  );
}
