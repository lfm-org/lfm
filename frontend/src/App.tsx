import React from 'react';
import './App.css';
import { PageContainer } from './components/PageContainer';
import { AppBar, Toolbar, Button } from '@material-ui/core';

interface IAppProps {
}

interface IAppState {
  clickTarget: string;
  isLoggedIn: boolean;
}

class App extends React.Component<IAppProps, IAppState> {
  constructor(props: Readonly<IAppProps>) {
    super(props);
    this.handleLogin = this.handleLogin.bind(this);
    this.handleLogout = this.handleLogout.bind(this);
    this.state = { clickTarget: "raids", isLoggedIn: false };
  }

  public handleLogin(e: React.MouseEvent) {
    this.setState({ clickTarget: "raid", isLoggedIn: true });
  }

  public handleLogout(e: React.MouseEvent) {
    this.setState({ clickTarget: "raids", isLoggedIn: false });
  }

  public render() {
    const isLoggedIn = this.state.isLoggedIn;
    return (
      <div className="App">
        <AppBar position="static">
          <Toolbar variant="dense">
            {isLoggedIn ? (
              <Button color="inherit" onClick={this.handleLogout}>Logout</Button>
            ) : (
                <Button color="inherit" onClick={this.handleLogin}>Login</Button>
              )}
          </Toolbar>
        </AppBar>
        <PageContainer currentPage={this.state.clickTarget} />
      </div>
    );
  }
}

export default App;
