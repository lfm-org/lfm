import { NextRequest, NextResponse } from "next/server";
import { battlenet, normalizeRedirectPath } from "@/lib/battlenet";
import { prisma } from "@/lib/prisma";

export async function POST(request: NextRequest) {
  const token = request.cookies.get("battlenet_token")?.value;
  const identity = token ? await battlenet.resolveIdentity(token) : null;
  if (!identity) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const contentType = request.headers.get("content-type") ?? "";
  let region: string, realm: string, name: string;
  if (contentType.includes("application/json")) {
    const body = await request.json() as { region: string; realm: string; name: string };
    ({ region, realm, name } = body);
  } else {
    const formData = await request.formData();
    region = formData.get("region") as string;
    realm = formData.get("realm") as string;
    name = formData.get("name") as string;
  }

  if (!region || !realm || !name) {
    return NextResponse.json({ error: "region, realm, and name are required" }, { status: 400 });
  }

  const raider = await prisma.raider.findUnique({
    where: { battleNetId: identity.battleNetId },
  });
  if (!raider) {
    return NextResponse.json({ error: "Raider not found" }, { status: 404 });
  }

  let portraitUrl: string | null = null;

  if (process.env.TEST_MODE === "true") {
    portraitUrl = "/test-portrait.jpg";
  } else {
    try {
      const namespace = `profile-${region}`;
      const mediaUrl = new URL(
        `https://${region}.api.blizzard.com/profile/wow/character/${realm}/${name.toLowerCase()}/character-media`
      );
      mediaUrl.searchParams.set("namespace", namespace);
      const mediaRes = await fetch(mediaUrl.toString(), {
        headers: { Authorization: `Bearer ${token}` },
      });
      if (mediaRes.ok) {
        const data = await mediaRes.json() as { assets?: Array<{ key: string; value: string }> };
        portraitUrl = data.assets?.find((a) => a.key === "avatar")?.value ?? null;
      }
    } catch {
      // proceed with null portrait
    }
  }

  const character = await prisma.character.upsert({
    where: { unique_region_realm_name: { region, realm, name } },
    update: { portraitUrl, raiderId: raider.id },
    create: { region, realm, name, portraitUrl, raiderId: raider.id },
  });

  await prisma.raider.update({
    where: { id: raider.id },
    data: { selectedCharacterId: character.id },
  });

  const redirectParam = request.nextUrl.searchParams.get("redirect");
  const normalized = normalizeRedirectPath(redirectParam ?? undefined);
  const destination = (!normalized || normalized === "/" || normalized === "/characters") ? "/raids" : normalized;
  return NextResponse.redirect(new URL(destination, process.env.APP_BASE_URL ?? request.url));
}
