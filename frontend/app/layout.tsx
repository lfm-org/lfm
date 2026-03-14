import type { Metadata } from "next";
import "./globals.css";
import ThemeRegistry from "@/components/ThemeRegistry";
import NavBar from "@/components/NavBar";
import { cookies } from "next/headers";
import { battlenet } from "@/lib/battlenet";

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

  return (
    <html lang="en">
      <body>
        <ThemeRegistry>
          <div className="App">
            <NavBar battleTag={identity?.battleTag ?? null} />
            {children}
          </div>
        </ThemeRegistry>
      </body>
    </html>
  );
}
