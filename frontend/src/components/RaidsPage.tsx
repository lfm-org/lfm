import React, { Fragment, useEffect, useState } from "react";

import {
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Link as MaterialLink,
  Paper,
} from "@material-ui/core";
import moment from "moment";
import { useTable } from "react-table";
import { DateUtils } from "../util/DateUtil";
import "./RaidsPage.css";
import { Link } from "react-router-dom";
import Axios from "axios";

export function RaidsPage() {
  const [, setFetching] = useState(false);
  const [raids, setRaids] = useState([]);

  function ByStartTime(a: any, b: any): number {
    const at = moment(a.startTime).local();
    const bt = moment(b.startTime).local();
    if (at.isAfter(bt)) {
      return 1;
    } else if (at.isBefore(bt)) {
      return -1;
    }
    return 0;
  }

  useEffect(() => {
    setFetching(true);
    const endpoint =
      (process.env.REACT_APP_API_SCHEME || "http") +
      "://" +
      process.env.REACT_APP_API_HOST +
      ":" +
      (process.env.REACT_APP_API_PORT || "3000") +
      "/raids";
    console.log("Calling endpoint: " + endpoint);
    Axios.get(endpoint).then((response) => {
      const sortedRaids = response.data.raids.sort(ByStartTime);
      setFetching(false);
      setRaids(sortedRaids);
    });

    return () => {
      setFetching(false);
    };
  }, []);

  function RaidList({ raids }: { raids: any[] }) {
    const columns = React.useMemo(
      () => [
        {
          Header: "Instance",
          accessor: "instance.name",
          Cell: (props: any) => (
            <Fragment>
              <MaterialLink
                color="inherit"
                component={Link}
                to={`/raids/${props.row.original.id}`}
              >
                {props.row.original.instance.name}
              </MaterialLink>
            </Fragment>
          ),
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
      []
    );

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
                    <TableCell {...column.getHeaderProps()}>
                      {column.render("Header")}
                    </TableCell>
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
                      // eslint:disable-next-line:max-line-length
                      return (
                        <TableCell
                          {...cell.getCellProps()}
                          component="th"
                          scope="row"
                        >
                          {cell.render("Cell")}
                        </TableCell>
                      );
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

  return <RaidList raids={raids} />;
}
