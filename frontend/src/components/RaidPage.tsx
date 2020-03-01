import React from "react";

// tslint:disable-next-line:no-empty-interface
export interface IRaidPageProps {
}

// tslint:disable-next-line:no-empty-interface
export interface IRaidPageState {
}

export class RaidPage extends React.Component<IRaidPageProps, IRaidPageState> {

    constructor(props: Readonly<IRaidPageProps>) {
        super(props);

        this.state = {};
    }

    public render() {
        return (<div>raid</div>);
    }
}
