import React from "react";

// tslint:disable-next-line:no-empty-interface
export interface ILoginPageProps {
}

// tslint:disable-next-line:no-empty-interface
export interface ILoginPageState {
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
