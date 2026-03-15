import { DateUtils } from "./DateUtil";

describe("DateUtils", () => {
  describe("FormatDate", () => {
    describe("given a valid ISO date string", () => {
      it("then formats it as dd.MM.yyyy HH.mm by default", () => {
        expect(DateUtils.FormatDate("2024-06-15T20:30:00.000Z")).toMatch(
          /15\.06\.2024 \d\d\.\d\d/
        );
      });

      it("then respects a custom format string", () => {
        expect(DateUtils.FormatDate("2024-01-07T00:00:00.000Z", "yyyy")).toBe(
          "2024"
        );
      });
    });
  });

  describe("FormatDateWithPassed", () => {
    describe("given a date in the past", () => {
      it("then returns 'Passed'", () => {
        expect(DateUtils.FormatDateWithPassed("2000-01-01T00:00:00.000Z")).toBe(
          "Passed"
        );
      });
    });

    describe("given a date far in the future", () => {
      it("then returns the formatted date, not 'Passed'", () => {
        // Use midday UTC to avoid date rolling across timezone boundaries.
        const result = DateUtils.FormatDateWithPassed(
          "2099-06-15T12:00:00.000Z"
        );
        expect(result).not.toBe("Passed");
        expect(result).toMatch(/15\.06\.2099/);
      });
    });
  });
});
