import { AppBar, Toolbar, IconButton } from "@material-ui/core";
import { AccountCircle } from "@material-ui/icons";
import React, { useEffect, useState } from "react";
import "./App.css";
import { Logo } from "./components/Logo";
import { RaidPage } from "./components/RaidPage";
import { BrowserRouter as Router, Routes, Route } from "react-router-dom";
import { LoginPage } from "./components/LoginPage";
import { RaidsPage } from "./components/RaidsPage";
import { LoginSuccessPage } from "./components/LoginSuccessPage";
import { store } from "./store";
import { UserLoginState } from "./models/user.model";

function App() {
  const [loginState, setLoginState] = useState(
    store.getState().user.loginState
  );

  useEffect(() => {
    setLoginState(store.getState().user.loginState);
  }, []);

  return (
    <div className="App">
      <AppBar position="static" color="inherit">
        <Toolbar variant="dense" color="inherit">
          <Logo image="/favicon.ico" title={document.title} />
          {loginState === UserLoginState.LoggedIn && (
            <div>{store.getState().user.name}</div>
          )}
          <IconButton color="inherit" href="/login">
            <AccountCircle color="inherit" />
          </IconButton>
        </Toolbar>
      </AppBar>
      <Router>
        <Routes>
          <Route caseSensitive path="/" element={<RaidsPage />} />
          <Route
            caseSensitive
            path="/login/success"
            element={<LoginSuccessPage />}
          />
          <Route caseSensitive path="/login/failed" element={<LoginPage />} />
          <Route caseSensitive path="/login" element={<LoginPage />} />
          <Route caseSensitive path="/raids/:id" element={<RaidPage />} />
          <Route caseSensitive path="/raids" element={<RaidsPage />} />
        </Routes>
      </Router>
    </div>
  );
}

export default App;
