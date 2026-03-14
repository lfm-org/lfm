import { NextRequest, NextResponse } from "next/server";
import { requireAuth } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { Prisma, RaidVisibility } from "@prisma/client";

export async function GET(request: NextRequest) {
  const identity = await requireAuth(request);
  if (!identity) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
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

  const raids = await prisma.raid.findMany({
    where: { OR: visibilityFilter },
    include: {
      instance: true,
      creator: true,
    },
    orderBy: { startTime: "asc" },
  });

  return NextResponse.json({ raids });
}
