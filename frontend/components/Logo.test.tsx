import React from "react";
import { render, screen } from "@testing-library/react";
import { Logo, DEFAULT_SUBTITLES } from "./Logo";

describe("given the Logo component", () => {
  describe("when rendered", () => {
    it("then displays a subtitle from the configured list", () => {
      render(<Logo image="/favicon.ico" title="PUG ME!" />);
      const el = screen.getByTestId("logo-subtitle");
      expect(DEFAULT_SUBTITLES).toContain(el.textContent);
    });
  });

  describe("when Math.random returns 0", () => {
    it("then the subtitle is the first entry", () => {
      jest.spyOn(Math, "random").mockReturnValue(0);
      render(<Logo image="/favicon.ico" title="PUG ME!" />);
      expect(screen.getByTestId("logo-subtitle").textContent).toBe(
        DEFAULT_SUBTITLES[0]
      );
      jest.restoreAllMocks();
    });
  });
});
