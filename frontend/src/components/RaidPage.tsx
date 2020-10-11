import React, { Fragment } from "react";

import { Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Link as MaterialLink, Paper } from "@material-ui/core";

import {
    Link,
    RouteComponentProps
} from "react-router-dom";
import "./RaidPage.css";
import { useTable } from "react-table";
import axios from 'axios';
import { DateUtils } from "../util/DateUtil";

interface RouterParams {
    id: string;
}

interface IRaidPageProps extends RouteComponentProps<RouterParams> {
}

// tslint:disable-next-line:no-empty-interface
interface IRaidPageState {
    isFetching: boolean;
    raid: any;
}

export class RaidPage extends React.Component<IRaidPageProps, IRaidPageState> {

    constructor(props: Readonly<IRaidPageProps>) {
        super(props);

        this.state = {
            isFetching: false,
            raid: { raidCharacters: [] }
        };
    }

    public componentDidMount() {
        this.setState({ isFetching: true });
        const endpoint = (process.env.REACT_APP_API_SCHEME || "http") + "://" +
            process.env.REACT_APP_API_HOST + ":" + (process.env.REACT_APP_API_PORT || "3000") +
            "/raids/" + this.props.match.params.id;
        console.log("Calling endpoint: " + endpoint);
        axios.get(endpoint)
            .then((response) => {
                this.setState({ isFetching: false, raid: response.data.raid });
            });
    }

    public render() {
        return (
            <div className="RaidPage">
                <this.InstanceInfo raid={this.state.raid} />
                <this.Attendance raid={this.state.raid} />
            </div>
        );
    }

    private InstanceInfo({ raid }: { raid: any }) {
        const instance = raid?.instance;
        if (instance) {
            return (
                <div>
                    <h3>{instance.name} ({raid.mode})</h3>
                    <h4>"{raid.description}"</h4>
                    <p>{DateUtils.FormatDateWithPassed(raid.startTime)}</p>
                </div>
            );
        }
        return (<div />);
    }

    private Attendance({ raid }: { raid: any }) {
        const columns = React.useMemo(() => [
            {
                Header: "Name",
                accessor: "character.name",
                Cell: (props: any) =>
                    <Fragment>
                        <MaterialLink color="inherit" component={Link} to={`/character/${props.row.original.character.id}`}>
                            {props.row.original.character.name}
                        </MaterialLink>
                    </Fragment>
            },
            {
                Header: "Level",
                accessor: "character.level",
            },
            {
                Header: "Race",
                accessor: "character.race.name",
            },
            {
                Header: "Class",
                accessor: "character.class.name",
            },
            {
                Header: "Realm",
                accessor: "character.realm",
            },
            {
                Header: "Attendance",
                accessor: (raid: any) => {
                    const attendanceWords = raid.reviewedAttendance.replace("_", " ").split(" ");
                    return attendanceWords.map((attendance: string) => attendance[0].toUpperCase() + attendance.substr(1).toLowerCase()).join(" ");
                },
            },
        ],
            []);

        const data = React.useMemo(() => raid.raidCharacters, [raid.raidCharacters]);

        const {
            getTableProps,
            getTableBodyProps,
            headerGroups,
            rows,
            prepareRow,
        } = useTable({ columns, data });

        return (
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
        );
    }
}
