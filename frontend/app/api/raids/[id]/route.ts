import { NextRequest, NextResponse } from "next/server";
import { requireAuth } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { Prisma, RaidVisibility } from "@prisma/client";

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  const identity = await requireAuth(request);
  if (!identity) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const { id } = await params;
  const raidId = parseInt(id, 10);
  if (isNaN(raidId)) {
    return NextResponse.json({ error: "Invalid id" }, { status: 400 });
  }

  const visibilityFilter: Prisma.RaidWhereInput[] = [
    { visibility: RaidVisibility.PUBLIC },
  ];

  if (identity.guildName) {
    visibilityFilter.push({
      visibility: RaidVisibility.GUILD,
      creatorGuild: identity.guildName,
    });
  }

  const raid = await prisma.raid.findFirst({
    where: {
      id: raidId,
      OR: visibilityFilter,
    },
    include: {
      instance: true,
      creator: true,
      raidCharacters: {
        include: {
          character: {
            include: {
              race: true,
              class: true,
            },
          },
        },
      },
    },
  });

  if (!raid) {
    return NextResponse.json({ error: "Not found" }, { status: 404 });
  }

  return NextResponse.json({ raid });
}
