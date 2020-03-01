import { AppBar, Toolbar, IconButton } from "@material-ui/core";
import { AccountCircle } from "@material-ui/icons";
import React from "react";
import "./App.css";
import { Logo } from "./components/Logo";
import { RaidsPage } from "./components/RaidsPage";

// tslint:disable-next-line:no-empty-interface
interface IAppProps {
}

// tslint:disable-next-line:no-empty-interface
interface IAppState {
}

class App extends React.Component<IAppProps, IAppState> {
  constructor(props: Readonly<IAppProps>) {
    super(props);
    this.state = { page: "raids", isLoggedIn: false };
  }

  public render() {
    return (
      <div className="App">
        <AppBar position="static" color="inherit">
          <Toolbar variant="dense" color="inherit">
            <Logo image="favicon.ico" title={document.title} />
            <IconButton color="inherit">
              <AccountCircle color="inherit" />
            </IconButton>
          </Toolbar>
        </AppBar>
        <RaidsPage />
      </div>
    );
  }
}

export default App;
