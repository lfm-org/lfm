import React from "react";

// tslint:disable-next-line:no-empty-interface
interface IRaidPageProps {
}

// tslint:disable-next-line:no-empty-interface
interface IRaidPageState {
}

export class RaidPage extends React.Component<IRaidPageProps, IRaidPageState> {

    constructor(props: Readonly<{}>) {
        super(props);
        this.state = {};
    }

    public render() {
        return (<div>raid</div>);
    }
}
