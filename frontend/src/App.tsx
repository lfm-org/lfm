import { AppBar, Toolbar, IconButton } from "@material-ui/core";
import { AccountCircle } from "@material-ui/icons";
import React from "react";
import "./App.css";
import { Logo } from "./components/Logo";
import { RaidPage } from "./components/RaidPage";
import {
  BrowserRouter as Router,
  Switch,
  Route
} from "react-router-dom";
import { LoginPage } from "./components/LoginPage";
import { RaidsPage } from "./components/RaidsPage";

// tslint:disable-next-line:no-empty-interface
interface IAppProps {
}

interface IAppState {
  isLoggedIn: boolean;
}

class App extends React.Component<IAppProps, IAppState> {
  constructor(props: Readonly<IAppProps>) {
    super(props);

    this.state = { isLoggedIn: false };
  }

  public render() {
    return (
      <div className="App">
        <AppBar position="static" color="inherit">
          <Toolbar variant="dense" color="inherit">
            <Logo image="/favicon.ico" title={document.title} />
            <IconButton color="inherit" href="/login">
              <AccountCircle color="inherit" />
            </IconButton>
          </Toolbar>
        </AppBar>
        <Router>
          <Switch>
            <Route exact path="/login" component={LoginPage} />
            <Route exact path="/raids/:id" component={RaidPage} />
            <Route exact path={["/", "/raids"]} component={RaidsPage} />
          </Switch>
        </Router>
      </div>
    );
  }
}

export default App;
