import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import {
  Box, Typography, Button,
  Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Link as MuiLink, Paper,
} from "@mui/material";
import {
  useReactTable, getCoreRowModel,
  createColumnHelper, flexRender,
} from "@tanstack/react-table";
import { Link as RouterLink } from "react-router";
import api from "../lib/api";
import type { Raid } from "../lib/raidTypes";
import { resolveInstanceModeLabel, type WowInstance } from "../lib/wowInstances";
import { DateUtils } from "../util/DateUtil";
import "./RaidsPage.css";

const columnHelper = createColumnHelper<Raid>();

export default function RaidsPage() {
  const navigate = useNavigate();
  const [raids, setRaids] = useState<Raid[]>([]);
  const [instances, setInstances] = useState<WowInstance[]>([]);

  useEffect(() => {
    let active = true;

    Promise.allSettled([
      api.get<Raid[]>("/raids"),
      api.get<WowInstance[]>("/instances"),
    ])
      .then(([raidResult, instanceResult]) => {
        if (!active) return;

        if (raidResult.status === "fulfilled") {
          setRaids(raidResult.value.data);
        }

        if (instanceResult.status === "fulfilled") {
          setInstances(instanceResult.value.data);
        }
      });

    return () => {
      active = false;
    };
  }, []);

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
    columnHelper.accessor(
      (row) => resolveInstanceModeLabel(instances, row.instanceId, row.modeKey),
      {
        id: "modeKey",
        header: "Mode",
      }
    ),
    columnHelper.accessor("description", { header: "Description" }),
    columnHelper.accessor((row) => DateUtils.FormatDateWithPassed(row.startTime), {
      id: "startTime",
      header: "Start Time",
    }),
    columnHelper.accessor(
      (row) => row.signupCloseTime ? DateUtils.FormatDateWithPassed(row.signupCloseTime) : "—",
      { id: "signupCloseTime", header: "Signup Closes" }
    ),
  ];

  const table = useReactTable({
    data: raids,
    columns,
    getCoreRowModel: getCoreRowModel(),
  });

  return (
    <div className="RaidsPage">
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
        <Typography component="h1" variant="h5">Raids</Typography>
        <Button variant="contained" onClick={() => navigate("/raids/new")}>Create Raid</Button>
      </Box>
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
