import { Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from "@material-ui/core";
import Paper from "@material-ui/core/Paper";
import { makeStyles } from "@material-ui/core/styles";
import moment from "moment";
import React from "react";
import { useTable } from "react-table";
import "./RaidsPage.css";

const useStyles = makeStyles({
    table: {
        minWidth: "650px",
    },
});

// tslint:disable-next-line:no-empty-interface
interface IRaidsPageProps {
}

interface IRaidsPageStates {
    isFetching: boolean;
    raids: any[];
}

export class RaidsPage extends React.Component<IRaidsPageProps, IRaidsPageStates> {

    private static FormatDate(date: string) {
        return moment(date).local().format("DD.MM.YYYY hh.mm");
    }

    constructor(props: Readonly<IRaidsPageProps>) {
        super(props);
        this.state = {
            isFetching: false,
            raids: [],
        };
    }

    public componentDidMount() {
        this.setState({ isFetching: true });
        const endpoint = process.env.REACT_APP_API_URL + "raids";
        fetch(endpoint)
            .then((response) => {
                const data = response.json();
                return data;
            })
            .then((data) => {
                this.setState({ isFetching: false, raids: data.raids });
            });
    }

    public render() {
        return (<this.Table raids={this.state.raids} />);
    }

    private Table({ raids }: { raids: any[] }) {
        const columns = React.useMemo(() => [
            {
                Header: "Instance",
                accessor: "instance.name",
            },
            {
                Header: "Description",
                accessor: "description",
            },
            {
                Header: "Start Time",
                accessor: (raid: any) => {
                    return RaidsPage.FormatDate(raid.startTime);
                },
            },
            {
                Header: "Signup Closes",
                accessor: (raid: any) => {
                    return RaidsPage.FormatDate(raid.signupCloseTime);
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

        const classes = useStyles();

        return (
            <div className="RaidsPage">
                <TableContainer component={Paper}>
                    <Table {...getTableProps()} className={classes.table} size="small">
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
