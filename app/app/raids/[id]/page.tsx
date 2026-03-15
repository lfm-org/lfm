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
import { useParams, useRouter } from "next/navigation";
import axios from "axios";
import { buildApiUrl } from "@/util/ApiUtil";
import { DateUtils } from "@/util/DateUtil";
import "./RaidPage.css";

interface WowClass {
  name: string;
}

interface WowRace {
  name: string;
}

interface Character {
  id: number;
  name: string;
  level: number | null;
  realm: string;
  class: WowClass;
  race: WowRace;
}

interface RaidCharacter {
  id: number;
  reviewedAttendance: string;
  character: Character;
}

interface RaidInstance {
  name: string;
}

interface Raid {
  id: number;
  startTime: string;
  description: string | null;
  mode: string | null;
  instance: RaidInstance;
  raidCharacters: RaidCharacter[];
}

const columnHelper = createColumnHelper<RaidCharacter>();

const columns = [
  columnHelper.accessor((row) => row.character.name, {
    id: "name",
    header: "Name",
    cell: (info) => (
      <MuiLink
        component={Link}
        href={`/character/${info.row.original.character.id}`}
        color="inherit"
      >
        {info.getValue()}
      </MuiLink>
    ),
  }),
  columnHelper.accessor((row) => row.character.level, {
    id: "level",
    header: "Level",
  }),
  columnHelper.accessor((row) => row.character.race.name, {
    id: "race",
    header: "Race",
  }),
  columnHelper.accessor((row) => row.character.class.name, {
    id: "class",
    header: "Class",
  }),
  columnHelper.accessor((row) => row.character.realm, {
    id: "realm",
    header: "Realm",
  }),
  columnHelper.accessor(
    (row) => {
      return row.reviewedAttendance
        .replace("_", " ")
        .split(" ")
        .map((word) => word[0].toUpperCase() + word.slice(1).toLowerCase())
        .join(" ");
    },
    { id: "attendance", header: "Attendance" }
  ),
];

function InstanceInfo({ raid }: { raid: Raid | null }) {
  if (!raid?.instance) return null;
  return (
    <div>
      <h3>
        {raid.instance.name} ({raid.mode})
      </h3>
      <h4>&quot;{raid.description}&quot;</h4>
      <p>{DateUtils.FormatDateWithPassed(raid.startTime)}</p>
    </div>
  );
}

export default function RaidPage() {
  const [raid, setRaid] = useState<Raid | null>(null);
  const params = useParams<{ id: string }>();
  const router = useRouter();

  useEffect(() => {
    if (!params.id) return;
    axios
      .get<{ raid: Raid }>(buildApiUrl(`/raids/${params.id}`), {
        withCredentials: true,
      })
      .then((response) => {
        setRaid(response.data.raid);
      })
      .catch((error) => {
        if (error?.response?.status === 401) {
          router.replace(`/login?redirect=/raids/${params.id}`);
        }
      });
  }, [params.id, router]);

  const table = useReactTable({
    data: raid?.raidCharacters ?? [],
    columns,
    getCoreRowModel: getCoreRowModel(),
  });

  return (
    <div className="RaidPage">
      <InstanceInfo raid={raid} />
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
