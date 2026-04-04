import { describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import { renderWithProviders } from "../../../test/renderWithProviders";
import CharacterCard from "./CharacterCard";

describe("CharacterCard with spec icon", () => {
  it("renders spec icon image when specIconUrl is provided", () => {
    renderWithProviders(
      <CharacterCard
        characterName="Grognak"
        characterClassId={1}
        characterClassName="Warrior"
        specName="Protection"
        specIconUrl="https://example.test/icon.jpg"
        desiredAttendance="IN"
      />
    );
    const img = screen.getByRole("img", { name: "Protection Warrior" });
    expect(img).toBeTruthy();
    expect((img as HTMLImageElement).src).toBe("https://example.test/icon.jpg");
  });

  it("renders fallback when specIconUrl is null", () => {
    renderWithProviders(
      <CharacterCard
        characterName="Grognak"
        characterClassId={1}
        characterClassName="Warrior"
        specName="Protection"
        specIconUrl={null}
        desiredAttendance="IN"
      />
    );
    const fallback = screen.getByRole("img", { name: "Protection Warrior" });
    expect(fallback).toBeTruthy();
    expect(fallback.textContent).toBe("P");
  });

  it("does not render class/spec text caption", () => {
    renderWithProviders(
      <CharacterCard
        characterName="Grognak"
        characterClassId={1}
        characterClassName="Warrior"
        specName="Protection"
        specIconUrl="https://example.test/icon.jpg"
        desiredAttendance="IN"
      />
    );
    expect(screen.queryByText("Warrior · Protection")).toBeNull();
    expect(screen.queryByText("Warrior")).toBeNull();
  });

  it("still renders character name and attendance chip", () => {
    renderWithProviders(
      <CharacterCard
        characterName="Grognak"
        characterClassId={1}
        characterClassName="Warrior"
        specName="Protection"
        specIconUrl="https://example.test/icon.jpg"
        desiredAttendance="IN"
      />
    );
    expect(screen.getByText("Grognak")).toBeTruthy();
    expect(screen.getByText("In")).toBeTruthy();
  });
});
