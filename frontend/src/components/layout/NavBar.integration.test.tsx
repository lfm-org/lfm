import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import NavBar from "./NavBar";
import { renderWithProviders } from "../../test/renderWithProviders";
import { setViewportWidth } from "../../test/setupDomTests";

const memberUser = {
  battleNetId: "bnet-member",
  guildName: "Ashen Concord",
  selectedCharacterId: "char-1",
  isSiteAdmin: false,
  locale: "en",
};

const siteAdminUser = {
  ...memberUser,
  battleNetId: "bnet-site-admin",
  isSiteAdmin: true,
};

describe("NavBar integration", () => {
  it("keeps desktop routes inline and exposes Characters plus Logout in the account menu", async () => {
    setViewportWidth(1280);
    const user = userEvent.setup();

    renderWithProviders(
      <NavBar
        character={{
          name: "Aelrin",
          portraitUrl: "https://example.com/aelrin.png",
        }}
      />,
      {
        route: "/raids",
        authValue: { user: memberUser, loading: false },
      }
    );

    expect(
      screen.getByRole("link", { name: "Raids" }).getAttribute("href")
    ).toBe("/raids");
    expect(
      screen.getByRole("link", { name: "Guild" }).getAttribute("href")
    ).toBe("/guild");

    const trigger = screen.getByRole("button", {
      name: "Open navigation menu for Aelrin",
    });

    expect(trigger.getAttribute("aria-haspopup")).toBe("menu");
    expect(trigger.getAttribute("aria-expanded")).toBe("false");

    await user.click(trigger);

    expect(trigger.getAttribute("aria-expanded")).toBe("true");

    const menu = screen.getByRole("menu");
    expect(
      within(menu).getByRole("menuitem", { name: "Characters" })
    ).toBeTruthy();
    expect(
      within(menu).getByRole("menuitem", { name: "Logout" })
    ).toBeTruthy();
    expect(
      within(menu).queryByRole("menuitem", { name: "Raids" })
    ).toBeNull();
  });

  it("collapses signed-in mobile routes into the character menu", async () => {
    setViewportWidth(390);
    const user = userEvent.setup();

    renderWithProviders(
      <NavBar character={{ name: "Aelrin", portraitUrl: null }} />,
      {
        route: "/guild/admin",
        authValue: { user: siteAdminUser, loading: false },
      }
    );

    expect(screen.queryByRole("link", { name: "Raids" })).toBeNull();
    expect(screen.queryByRole("link", { name: "Guild" })).toBeNull();

    const trigger = screen.getByRole("button", {
      name: "Open navigation menu for Aelrin",
    });

    await user.click(trigger);

    const menu = screen.getByRole("menu");
    expect(
      within(menu).getByRole("menuitem", { name: "Characters" })
    ).toBeTruthy();
    expect(
      within(menu).getByRole("menuitem", { name: "Raids" })
    ).toBeTruthy();
    expect(
      within(menu).getByRole("menuitem", { name: "Guild" })
    ).toBeTruthy();
    expect(
      within(menu).getByRole("menuitem", { name: "Guild Admin" })
    ).toBeTruthy();
    expect(
      within(menu).getByRole("menuitem", { name: "Logout" })
    ).toBeTruthy();
  });

  it("shows only Login on mobile when signed out", () => {
    setViewportWidth(390);

    renderWithProviders(<NavBar />, { route: "/" });

    expect(screen.getByRole("link", { name: "Login" })).toBeTruthy();
    expect(screen.queryByRole("link", { name: "Raids" })).toBeNull();
    expect(screen.queryByRole("link", { name: "Guild" })).toBeNull();
    expect(
      screen.queryByRole("button", { name: /Open navigation menu/i })
    ).toBeNull();
  });
});
