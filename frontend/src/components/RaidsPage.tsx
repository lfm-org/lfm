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
import { buildApiUrl } from "../util/ApiUtil";
import "./RaidsPage.css";
import { Link, useLocation, useNavigate } from "react-router-dom";
import Axios from "axios";
import { getAccessToken, clearAccessToken } from "../util/AuthUtil";

export function RaidsPage() {
  const [, setFetching] = useState(false);
  const [raids, setRaids] = useState([]);
  const navigate = useNavigate();
  const location = useLocation();

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
    const token = getAccessToken();
    if (!token) {
      navigate("/login", {
        state: { from: { pathname: location.pathname } },
      });
      return;
    }
    setFetching(true);
    const endpoint = buildApiUrl("/raids");
    console.log("Calling endpoint: " + endpoint);
    Axios.get(endpoint, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    })
      .then((response) => {
        const sortedRaids = response.data.raids.sort(ByStartTime);
        setFetching(false);
        setRaids(sortedRaids);
      })
      .catch((error) => {
        setFetching(false);
        if (error?.response?.status === 401) {
          clearAccessToken();
          navigate("/login", {
            state: { from: { pathname: location.pathname } },
          });
        }
      });

    return () => {
      setFetching(false);
    };
  }, [navigate, location.pathname]);

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
