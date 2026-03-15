import type { Metadata } from "next";
import "./globals.css";
import ThemeRegistry from "@/components/ThemeRegistry";
import NavBar from "@/components/NavBar";
import { cookies } from "next/headers";
import { battlenet } from "@/lib/battlenet";
import { prisma } from "@/lib/prisma";

export const metadata: Metadata = {
  title: "PUG ME!",
  description: "Communal World of Warcraft Pug Finder Service",
};

export default async function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const cookieStore = await cookies();
  const token = cookieStore.get("battlenet_token")?.value;
  const identity = token ? await battlenet.resolveIdentity(token) : null;

  let character: { name: string; portraitUrl: string | null } | null = null;
  if (identity) {
    const raider = await prisma.raider.findUnique({
      where: { battleNetId: identity.battleNetId },
      include: { selectedCharacter: true },
    });
    if (raider?.selectedCharacter) {
      character = {
        name: raider.selectedCharacter.name,
        portraitUrl: raider.selectedCharacter.portraitUrl,
      };
    }
  }

  return (
    <html lang="en">
      <body>
        <ThemeRegistry>
          <div className="App">
            <NavBar character={character} />
            {children}
          </div>
        </ThemeRegistry>
      </body>
    </html>
  );
}
