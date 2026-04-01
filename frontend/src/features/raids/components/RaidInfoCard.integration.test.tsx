import { describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "../../../test/renderWithProviders";
import RaidInfoCard from "./RaidInfoCard";
import type { Raid } from "../lib/raidTypes";

function buildRaid(overrides: Partial<Raid> = {}): Raid {
  return {
    id: "raid-1",
    startTime: new Date(Date.now() + 48 * 3600 * 1000).toISOString(),
    signupCloseTime: new Date(Date.now() + 42 * 3600 * 1000).toISOString(),
    description: "Test raid",
    modeKey: "NORMAL:10",
    visibility: "PUBLIC",
    instanceId: 631,
    instanceName: "Icecrown Citadel",
    creatorBattleNetId: "creator-1",
    creatorGuild: "",
    createdAt: "2026-04-01T00:00:00Z",
    raidCharacters: [],
    ...overrides,
  };
}

describe("RaidInfoCard edit button", () => {
  it("shows edit button for creator", () => {
    renderWithProviders(
      <RaidInfoCard
        raid={buildRaid()}
        modeLabel="Normal (10)"
        currentBattleNetId="creator-1"
        onRaidEdit={vi.fn()}
      />
    );
    const btn = screen.getByRole("button", { name: "Edit" });
    expect(btn).toBeTruthy();
    expect((btn as HTMLButtonElement).disabled).toBe(false);
  });

  it("hides edit button for non-creator without guild permission", () => {
    renderWithProviders(
      <RaidInfoCard
        raid={buildRaid()}
        modeLabel="Normal (10)"
        currentBattleNetId="other-user"
        onRaidEdit={vi.fn()}
      />
    );
    expect(screen.queryByRole("button", { name: "Edit" })).toBeNull();
  });

  it("shows edit button for guild officer on guild raid", () => {
    renderWithProviders(
      <RaidInfoCard
        raid={buildRaid({ visibility: "GUILD", creatorBattleNetId: "someone-else" })}
        modeLabel="Normal (10)"
        currentBattleNetId="officer-1"
        canCreateGuildRaids
        onRaidEdit={vi.fn()}
      />
    );
    const btn = screen.getByRole("button", { name: "Edit" });
    expect(btn).toBeTruthy();
    expect((btn as HTMLButtonElement).disabled).toBe(false);
  });

  it("disables edit button when signups are closed", () => {
    renderWithProviders(
      <RaidInfoCard
        raid={buildRaid({ signupCloseTime: new Date(Date.now() - 3600 * 1000).toISOString() })}
        modeLabel="Normal (10)"
        currentBattleNetId="creator-1"
        onRaidEdit={vi.fn()}
      />
    );
    expect((screen.getByRole("button", { name: "Edit" }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("calls onRaidEdit when clicked", async () => {
    const onRaidEdit = vi.fn();
    renderWithProviders(
      <RaidInfoCard
        raid={buildRaid()}
        modeLabel="Normal (10)"
        currentBattleNetId="creator-1"
        onRaidEdit={onRaidEdit}
      />
    );
    await userEvent.click(screen.getByRole("button", { name: "Edit" }));
    expect(onRaidEdit).toHaveBeenCalledWith("raid-1");
  });
});
