import React from "react";
import { LoginPage } from "./LoginPage";
import { RaidPage } from "./RaidPage";
import { RaidsPage } from "./RaidsPage";

export interface IPageContainerProps {
    currentPage: string;
}

// tslint:disable-next-line:no-empty-interface
export interface IPageContainerState {
}

export class PageContainer extends React.Component<IPageContainerProps, IPageContainerState> {

    constructor(props: Readonly<IPageContainerProps>) {
        super(props);

        this.state = {};
    }

    public element(page: string) {
        let current = <RaidsPage />;

        switch (page) {
            case "raid": current = <RaidPage />; break;
            case "login": current = <LoginPage />; break;
        }

        return current;
    }

    public render() {
        return (this.element(this.props.currentPage));
    }
}
