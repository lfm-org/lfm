/**
 * @jest-environment node
 */

describe("given HMAC_SECRET is set", () => {
  beforeEach(() => {
    process.env.HMAC_SECRET = "test-secret-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
  });

  afterEach(() => {
    delete process.env.HMAC_SECRET;
  });

  describe("when hashBattleNetId is called with the same value twice", () => {
    it("then returns the same hash", () => {
      const { hashBattleNetId } = require("./crypto");
      expect(hashBattleNetId(463557)).toBe(hashBattleNetId(463557));
    });
  });

  describe("when hashBattleNetId is called with different values", () => {
    it("then returns different hashes", () => {
      const { hashBattleNetId } = require("./crypto");
      expect(hashBattleNetId(463557)).not.toBe(hashBattleNetId(999999));
    });
  });

  describe("when hashBattleNetId is called with integer and string of the same value", () => {
    it("then returns the same hash", () => {
      const { hashBattleNetId } = require("./crypto");
      expect(hashBattleNetId(463557)).toBe(hashBattleNetId("463557"));
    });
  });
});

describe("given HMAC_SECRET is not set", () => {
  beforeEach(() => {
    delete process.env.HMAC_SECRET;
    jest.resetModules();
  });

  describe("when hashBattleNetId is called", () => {
    it("then throws referencing HMAC_SECRET", () => {
      const { hashBattleNetId } = require("./crypto");
      expect(() => hashBattleNetId(463557)).toThrow("HMAC_SECRET");
    });
  });
});
