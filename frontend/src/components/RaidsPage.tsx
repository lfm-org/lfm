import React, { Fragment } from "react";

import { Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Link as MaterialLink, Paper } from "@material-ui/core";
import moment from "moment";
import { useTable } from "react-table";
import { DateUtils } from "../util/DateUtil";
import "./RaidsPage.css";
import {
    Link, RouteComponentProps
} from "react-router-dom";
import Axios from "axios";

// tslint:disable-next-line:no-empty-interface
interface RouterProps {
}

interface IRaidsPageProps extends RouteComponentProps<RouterProps> {
}

interface IRaidsPageStates {
    isFetching: boolean;
    raids: any[];
}

export class RaidsPage extends React.Component<IRaidsPageProps, IRaidsPageStates> {

    constructor(props: Readonly<IRaidsPageProps>) {
        super(props);

        this.state = {
            isFetching: false,
            raids: [],
        };
    }

    public ByStartTime(a: any, b: any): number {
        const at = moment(a.startTime).local();
        const bt = moment(b.startTime).local();
        if (at.isAfter(bt)) {
            return 1;
        } else if (at.isBefore(bt)) {
            return -1;
        }
        return 0;
    }

    public componentDidMount() {
        this.setState({ isFetching: true });
        const endpoint = (process.env.REACT_APP_API_SCHEME || "http") + "://" +
            process.env.REACT_APP_API_HOST + ":" + (process.env.REACT_APP_API_PORT || "3000") +
            "/raids";
        console.log("Calling endpoint: " + endpoint);
        Axios.get(endpoint)
            .then((response) => {
                const sortedRaids = response.data.raids.sort(this.ByStartTime);
                this.setState({ isFetching: false, raids: sortedRaids });
            });
    }

    public render() {
        return (<this.RaidList raids={this.state.raids} />);
    }

    public RaidList({ raids }: { raids: any[] }) {
        const columns = React.useMemo(() => [
            {
                Header: "Instance",
                accessor: "instance.name",
                Cell: (props: any) =>
                    <Fragment>
                        <MaterialLink color="inherit" component={Link} to={`/raids/${props.row.original.id}`}>
                            {props.row.original.instance.name}
                        </MaterialLink>
                    </Fragment>
            },
            {
                Header: "Mode",
                accessor: "mode",
            },
            {
                Header: "Description",
                accessor: "description",
            },
            {
                Header: "Start Time",
                accessor: (raid: any) => {
                    return DateUtils.FormatDateWithPassed(raid.startTime);
                },
            },
            {
                Header: "Signup Closes",
                accessor: (raid: any) => {
                    return DateUtils.FormatDateWithPassed(raid.signupCloseTime);
                },
            },
        ],
            []);

        const data = React.useMemo(() => raids, [raids]);

        const {
            getTableProps,
            getTableBodyProps,
            headerGroups,
            rows,
            prepareRow,
        } = useTable({ columns, data });

        return (
            <div className="RaidsPage">
                <TableContainer component={Paper}>
                    <Table {...getTableProps()} size="small" color="inherit">
                        <TableHead>
                            {headerGroups.map((headerGroup) => (
                                <TableRow {...headerGroup.getHeaderGroupProps()}>
                                    {headerGroup.headers.map((column) => (
                                        <TableCell {...column.getHeaderProps()}>{column.render("Header")}</TableCell>
                                    ))}
                                </TableRow>
                            ))}
                        </TableHead>
                        <TableBody {...getTableBodyProps()}>
                            {rows.map((row, i) => {
                                prepareRow(row);
                                return (
                                    <TableRow {...row.getRowProps()} key={row.id}>
                                        {row.cells.map((cell) => {
                                            // tslint:disable-next-line:max-line-length
                                            return <TableCell {...cell.getCellProps()} component="th" scope="row">{cell.render("Cell")}</TableCell>;
                                        })}
                                    </TableRow>
                                );
                            })}
                        </TableBody>
                    </Table>
                </TableContainer>
            </div>
        );
    }
}
