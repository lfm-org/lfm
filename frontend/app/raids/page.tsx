"use client";

import React, { useEffect, useState } from "react";
import {
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Link as MuiLink,
  Paper,
} from "@mui/material";
import {
  useReactTable,
  getCoreRowModel,
  createColumnHelper,
  flexRender,
} from "@tanstack/react-table";
import Link from "next/link";
import { useRouter } from "next/navigation";
import axios from "axios";
import { buildApiUrl } from "@/util/ApiUtil";
import { DateUtils } from "@/util/DateUtil";
import "./RaidsPage.css";

interface RaidInstance {
  id: number;
  name: string;
}

interface Raid {
  id: number;
  startTime: string;
  signupCloseTime: string;
  description: string | null;
  mode: string | null;
  instance: RaidInstance;
}

const columnHelper = createColumnHelper<Raid>();

const columns = [
  columnHelper.accessor((row) => row.instance.name, {
    id: "instance",
    header: "Instance",
    cell: (info) => (
      <MuiLink
        component={Link}
        href={`/raids/${info.row.original.id}`}
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
  const router = useRouter();

  useEffect(() => {
    axios
      .get<{ raids: Raid[] }>(buildApiUrl("/raids"), {
        withCredentials: true,
      })
      .then((response) => {
        setRaids(response.data.raids);
      })
      .catch((error) => {
        if (error?.response?.status === 401) {
          router.replace("/login?redirect=/raids");
        }
      });
  }, [router]);

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
                    {flexRender(
                      header.column.columnDef.header,
                      header.getContext()
                    )}
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
