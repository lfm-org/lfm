import { Routes, Route, Navigate } from "react-router";
import LoginPage from "./pages/LoginPage";
import LoginFailedPage from "./pages/LoginFailedPage";
import LoginSuccessPage from "./pages/LoginSuccessPage";
import CharactersPage from "./pages/CharactersPage";
import RaidsPage from "./pages/RaidsPage";
import RaidDetailPage from "./pages/RaidDetailPage";
import AuthGuard from "./components/AuthGuard";

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/raids" replace />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/login/failed" element={<LoginFailedPage />} />
      <Route path="/login/success" element={<LoginSuccessPage />} />
      <Route path="/characters" element={<AuthGuard><CharactersPage /></AuthGuard>} />
      <Route path="/raids" element={<AuthGuard><RaidsPage /></AuthGuard>} />
      <Route path="/raids/:id" element={<AuthGuard><RaidDetailPage /></AuthGuard>} />
    </Routes>
  );
}
