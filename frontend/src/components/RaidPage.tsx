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

import { Link, useParams } from "react-router-dom";
import "./RaidPage.css";
import { useTable } from "react-table";
import Axios from "axios";
import { DateUtils } from "../util/DateUtil";

export function RaidPage() {
  const [, setFetching] = useState(false);
  const [raid, setRaid] = useState({ raidCharacters: [] });
  const params = useParams();

  useEffect(() => {
    setFetching(true);
    const endpoint =
      (process.env.REACT_APP_API_SCHEME || "http") +
      "://" +
      process.env.REACT_APP_API_HOST +
      ":" +
      (process.env.REACT_APP_API_PORT || "3000") +
      "/raids/" +
      params.id;
    console.log("Calling endpoint: " + endpoint);
    Axios.get(endpoint).then((response) => {
      setFetching(false);
      setRaid(response.data.raid);
    });

    return () => {
      setFetching(false);
    };
  }, [params]);

  function InstanceInfo({ raid }: { raid: any }) {
    const instance = raid?.instance;
    if (instance) {
      return (
        <div>
          <h3>
            {instance.name} ({raid.mode})
          </h3>
          <h4>"{raid.description}"</h4>
          <p>{DateUtils.FormatDateWithPassed(raid.startTime)}</p>
        </div>
      );
    }
    return <div />;
  }

  function Attendance({ raid }: { raid: any }) {
    const columns = React.useMemo(
      () => [
        {
          Header: "Name",
          accessor: "character.name",
          Cell: (props: any) => (
            <Fragment>
              <MaterialLink
                color="inherit"
                component={Link}
                to={`/character/${props.row.original.character.id}`}
              >
                {props.row.original.character.name}
              </MaterialLink>
            </Fragment>
          ),
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
            const attendanceWords = raid.reviewedAttendance
              .replace("_", " ")
              .split(" ");
            return attendanceWords
              .map(
                (attendance: string) =>
                  attendance[0].toUpperCase() +
                  attendance.substr(1).toLowerCase()
              )
              .join(" ");
          },
        },
      ],
      []
    );

    const data = React.useMemo(
      () => raid.raidCharacters,
      [raid.raidCharacters]
    );

    const { getTableProps, getTableBodyProps, headerGroups, rows, prepareRow } =
      useTable({ columns, data });

    return (
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
    );
  }
  return (
    <div className="RaidPage">
      <InstanceInfo raid={raid} />
      <Attendance raid={raid} />
    </div>
  );
}
