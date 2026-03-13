import { AppBar, Button, Toolbar } from "@material-ui/core";
import React from "react";
import "./App.css";
import { Logo } from "./components/Logo";
import { RaidPage } from "./components/RaidPage";
import { Routes, Route, Link, useLocation } from "react-router-dom";
import { RaidsPage } from "./components/RaidsPage";
import { LoginPage } from "./components/LoginPage";
import { LoginSuccessPage } from "./components/LoginSuccessPage";
import { LoginFailedPage } from "./components/LoginFailedPage";

function App() {
  const location = useLocation();
  const loginState = { from: { pathname: location.pathname } };

  return (
    <div className="App">
      <AppBar position="static" color="inherit">
        <Toolbar variant="dense" color="inherit">
          <Logo image="/favicon.ico" title={document.title} />
          <Button
            component={Link}
            to="/raids"
            color="inherit"
            size="small"
            style={{ marginLeft: "16px" }}
          >
            Raids
          </Button>
          <Button
            component={Link}
            to="/login"
            state={loginState}
            color="inherit"
            size="small"
            style={{ marginLeft: "8px" }}
          >
            Login
          </Button>
        </Toolbar>
      </AppBar>
      <Routes>
        <Route caseSensitive path="/" element={<RaidsPage />} />
        <Route caseSensitive path="/raids/:id" element={<RaidPage />} />
        <Route caseSensitive path="/raids" element={<RaidsPage />} />
        <Route caseSensitive path="/login" element={<LoginPage />} />
        <Route path="/login/success" element={<LoginSuccessPage />} />
        <Route path="/login/failed" element={<LoginFailedPage />} />
      </Routes>
    </div>
  );
}

export default App;
