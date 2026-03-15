import React from "react";
import { render, screen } from "@testing-library/react";
import NavBar from "./NavBar";

jest.mock("next/navigation", () => ({
  usePathname: () => "/",
}));

jest.mock("./Logo", () => ({
  Logo: () => <div data-testid="logo" />,
}));

jest.mock("next/image", () => ({
  __esModule: true,
  default: (props: React.ImgHTMLAttributes<HTMLImageElement>) => {
    // eslint-disable-next-line @next/next/no-img-element, jsx-a11y/alt-text
    return <img {...props} />;
  },
}));

describe("NavBar", () => {
  describe("given no character (logged out)", () => {
    it("then shows the Login link", () => {
      render(<NavBar character={null} />);
      expect(screen.getByRole("link", { name: /login/i })).toBeInTheDocument();
    });
  });

  describe("given a selected character (logged in)", () => {
    const character = { name: "TestChar", portraitUrl: "/test-portrait.jpg" };

    it("then shows the character name as a link to /characters", () => {
      render(<NavBar character={character} />);
      const link = screen.getByRole("link", { name: /TestChar/i });
      expect(link).toBeInTheDocument();
      expect(link).toHaveAttribute("href", "/characters");
    });

    it("then shows the Logout link", () => {
      render(<NavBar character={character} />);
      expect(screen.getByRole("link", { name: /logout/i })).toBeInTheDocument();
    });

    it("then does not show the Login link", () => {
      render(<NavBar character={character} />);
      expect(screen.queryByRole("link", { name: /login/i })).not.toBeInTheDocument();
    });
  });
});
