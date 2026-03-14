import type { Metadata } from "next";
import "./globals.css";
import ThemeRegistry from "@/components/ThemeRegistry";
import NavBar from "@/components/NavBar";

export const metadata: Metadata = {
  title: "PUG ME!",
  description: "Communal World of Warcraft Pug Finder Service",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body>
        <ThemeRegistry>
          <div className="App">
            <NavBar />
            {children}
          </div>
        </ThemeRegistry>
      </body>
    </html>
  );
}
