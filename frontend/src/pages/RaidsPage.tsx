import { useEffect, useState } from "react";
import {
  Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Link as MuiLink, Paper,
} from "@mui/material";
import {
  useReactTable, getCoreRowModel,
  createColumnHelper, flexRender,
} from "@tanstack/react-table";
import { Link as RouterLink } from "react-router";
import api from "../lib/api";
import { DateUtils } from "../util/DateUtil";
import "./RaidsPage.css";

interface Raid {
  id: string;
  startTime: string;
  signupCloseTime: string;
  description: string;
  mode: string;
  instanceName: string;
}

const columnHelper = createColumnHelper<Raid>();

const columns = [
  columnHelper.accessor("instanceName", {
    header: "Instance",
    cell: (info) => (
      <MuiLink
        component={RouterLink}
        to={`/raids/${info.row.original.id}`}
        color="inherit"
      >
        {info.getValue()}
      </MuiLink>
    ),
  }),
  columnHelper.accessor("mode", { header: "Mode" }),
  columnHelper.accessor("description", { header: "Description" }),
  columnHelper.accessor((row) => DateUtils.FormatDateWithPassed(row.startTime), {
    id: "startTime",
    header: "Start Time",
  }),
  columnHelper.accessor(
    (row) => DateUtils.FormatDateWithPassed(row.signupCloseTime),
    { id: "signupCloseTime", header: "Signup Closes" }
  ),
];

export default function RaidsPage() {
  const [raids, setRaids] = useState<Raid[]>([]);

  useEffect(() => {
    api.get<Raid[]>("/raids")
      .then(res => setRaids(res.data))
      .catch(() => {});
  }, []);

  const table = useReactTable({
    data: raids,
    columns,
    getCoreRowModel: getCoreRowModel(),
  });

  return (
    <div className="RaidsPage">
      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id}>
                {headerGroup.headers.map((header) => (
                  <TableCell key={header.id}>
                    {flexRender(header.column.columnDef.header, header.getContext())}
                  </TableCell>
                ))}
              </TableRow>
            ))}
          </TableHead>
          <TableBody>
            {table.getRowModel().rows.map((row) => (
              <TableRow key={row.id}>
                {row.getVisibleCells().map((cell) => (
                  <TableCell key={cell.id} component="th" scope="row">
                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                  </TableCell>
                ))}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </div>
  );
}
