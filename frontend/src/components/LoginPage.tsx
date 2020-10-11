import React from "react";
import { RouteComponentProps } from "react-router-dom";

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

    public render() {
        return (<div>login</div>);
    }
}
