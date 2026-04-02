import { describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "../../../test/renderWithProviders";
import RunInfoCard from "./RunInfoCard";
import type { Run } from "../lib/runTypes";

function buildRun(overrides: Partial<Run> = {}): Run {
  return {
    id: "run-1",
    startTime: new Date(Date.now() + 48 * 3600 * 1000).toISOString(),
    signupCloseTime: new Date(Date.now() + 42 * 3600 * 1000).toISOString(),
    description: "Test run",
    modeKey: "NORMAL:10",
    visibility: "PUBLIC",
    instanceId: 631,
    instanceName: "Icecrown Citadel",
    creatorBattleNetId: "creator-1",
    creatorGuild: "",
    createdAt: "2026-04-01T00:00:00Z",
    runCharacters: [],
    ...overrides,
  };
}

describe("RunInfoCard edit button", () => {
  it("shows edit button for creator", () => {
    renderWithProviders(
      <RunInfoCard
        run={buildRun()}
        modeLabel="Normal (10)"
        currentBattleNetId="creator-1"
        onRunEdit={vi.fn()}
      />
    );
    const btn = screen.getByRole("button", { name: "Edit" });
    expect(btn).toBeTruthy();
    expect((btn as HTMLButtonElement).disabled).toBe(false);
  });

  it("hides edit button for non-creator without guild permission", () => {
    renderWithProviders(
      <RunInfoCard
        run={buildRun()}
        modeLabel="Normal (10)"
        currentBattleNetId="other-user"
        onRunEdit={vi.fn()}
      />
    );
    expect(screen.queryByRole("button", { name: "Edit" })).toBeNull();
  });

  it("shows edit button for guild officer on guild run", () => {
    renderWithProviders(
      <RunInfoCard
        run={buildRun({ visibility: "GUILD", creatorBattleNetId: "someone-else" })}
        modeLabel="Normal (10)"
        currentBattleNetId="officer-1"
        canCreateGuildRuns
        onRunEdit={vi.fn()}
      />
    );
    const btn = screen.getByRole("button", { name: "Edit" });
    expect(btn).toBeTruthy();
    expect((btn as HTMLButtonElement).disabled).toBe(false);
  });

  it("disables edit button when signups are closed", () => {
    renderWithProviders(
      <RunInfoCard
        run={buildRun({ signupCloseTime: new Date(Date.now() - 3600 * 1000).toISOString() })}
        modeLabel="Normal (10)"
        currentBattleNetId="creator-1"
        onRunEdit={vi.fn()}
      />
    );
    expect((screen.getByRole("button", { name: "Edit" }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("calls onRunEdit when clicked", async () => {
    const onRunEdit = vi.fn();
    renderWithProviders(
      <RunInfoCard
        run={buildRun()}
        modeLabel="Normal (10)"
        currentBattleNetId="creator-1"
        onRunEdit={onRunEdit}
      />
    );
    await userEvent.click(screen.getByRole("button", { name: "Edit" }));
    expect(onRunEdit).toHaveBeenCalledWith("run-1");
  });
});
