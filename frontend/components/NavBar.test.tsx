import React from "react";
import { render, screen } from "@testing-library/react";
import NavBar from "./NavBar";

jest.mock("next/navigation", () => ({
  usePathname: () => "/",
}));

jest.mock("./Logo", () => ({
  Logo: () => <div data-testid="logo" />,
}));

describe("NavBar", () => {
  describe("given no battleTag (logged out)", () => {
    it("then shows the Login link", () => {
      render(<NavBar battleTag={null} />);
      expect(screen.getByRole("link", { name: /login/i })).toBeInTheDocument();
    });
  });

  describe("given a battleTag (logged in)", () => {
    it("then shows the battle tag", () => {
      render(<NavBar battleTag="User#1234" />);
      expect(screen.getByText("User#1234")).toBeInTheDocument();
    });

    it("then does not show the Login link", () => {
      render(<NavBar battleTag="User#1234" />);
      expect(
        screen.queryByRole("link", { name: /login/i })
      ).not.toBeInTheDocument();
    });
  });
});
