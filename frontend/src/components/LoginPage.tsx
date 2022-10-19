import { Button, TextField } from "@material-ui/core";
import React, { FormEvent } from "react";
import { RouteComponentProps } from "react-router-dom";
import "./LoginPage.css";

// tslint:disable-next-line:no-empty-interface
interface RouterProps {
}

interface ILoginPageProps extends RouteComponentProps<RouterProps> {
}

// tslint:disable-next-line:no-empty-interface
interface ILoginPageState {
}

export class LoginPage extends React.Component<ILoginPageProps, ILoginPageState> {

    constructor(props: Readonly<ILoginPageProps>) {
        super(props);

        this.state = {};
    }

    private endpoint = (process.env.REACT_APP_API_SCHEME || "http") + "://" +
        process.env.REACT_APP_API_HOST + ":" + (process.env.REACT_APP_API_PORT || "3000") +
        "/login";

    public render() {
        return (<div className="LoginPage">
            <form noValidate autoComplete="off" action={this.endpoint} method="post">
                <div>
                    <TextField id="username" label="Username" variant="outlined" required />
                </div>
                <div>
                    <TextField id="password" label="Password" variant="outlined" type="password" required />
                </div>
                <div>
                    <Button variant="contained" type="submit" value="Submit">Login</Button>
                </div>
            </form>
        </div>);
    }
}
