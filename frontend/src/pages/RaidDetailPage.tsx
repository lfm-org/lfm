import { useEffect, useState } from "react";
import {
  Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Paper, Typography,
} from "@mui/material";
import {
  useReactTable, getCoreRowModel,
  createColumnHelper, flexRender,
} from "@tanstack/react-table";
import { useParams } from "react-router";
import api from "../lib/api";
import { DateUtils } from "../util/DateUtil";
import "./RaidDetailPage.css";

interface RaidCharacter {
  id: string;
  characterId: string;
  characterName: string;
  characterRealm: string;
  characterLevel: number;
  characterClassName: string;
  characterRaceName: string;
  raiderBattleNetId: string;
  desiredAttendance: string;
  reviewedAttendance: string;
}

interface Raid {
  id: string;
  startTime: string;
  description: string;
  mode: string;
  instanceName: string;
  raidCharacters: RaidCharacter[];
}

const columnHelper = createColumnHelper<RaidCharacter>();

const columns = [
  columnHelper.accessor("characterName", { header: "Name" }),
  columnHelper.accessor("characterLevel", { header: "Level" }),
  columnHelper.accessor("characterRaceName", { header: "Race" }),
  columnHelper.accessor("characterClassName", { header: "Class" }),
  columnHelper.accessor("characterRealm", { header: "Realm" }),
  columnHelper.accessor(
    (row) => row.reviewedAttendance
      .replace("_", " ")
      .split(" ")
      .map((word) => word[0].toUpperCase() + word.slice(1).toLowerCase())
      .join(" "),
    { id: "attendance", header: "Attendance" }
  ),
];

function InstanceInfo({ raid }: { raid: Raid | null }) {
  if (!raid) return null;
  return (
    <div>
      <h3>{raid.instanceName} ({raid.mode})</h3>
      <h4>&quot;{raid.description}&quot;</h4>
      <p>{DateUtils.FormatDateWithPassed(raid.startTime)}</p>
    </div>
  );
}

export default function RaidDetailPage() {
  const [raid, setRaid] = useState<Raid | null>(null);
  const { id } = useParams<{ id: string }>();

  useEffect(() => {
    if (!id) return;
    api.get<Raid>(`/raids/${id}`)
      .then(res => setRaid(res.data))
      .catch(() => {});
  }, [id]);

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
