import React from "react";
import { RaidPage } from "./RaidPage";
import { RaidsPage } from "./RaidsPage";

interface IPageContainerProps {
    currentPage: string;
}

// tslint:disable-next-line:no-empty-interface
interface IPageContainerState {
}

export class PageContainer extends React.Component<IPageContainerProps, IPageContainerState> {

    constructor(props: Readonly<IPageContainerProps>) {
        super(props);
        this.state = {};
    }

    public getElement(page: string) {
        if (page === "raid") {
            return <RaidPage />;
        }
        return <RaidsPage />;
    }

    public render() {
        return (this.getElement(this.props.currentPage));
    }
}
