import * as crypto from "crypto";

export default class CryptoHelpers {
  static readonly CHSaltBytes: number = 16;
  static readonly CHIterations: number = 2048;
  static readonly CHKeyLength: number = 32;
  static readonly CHDigest: string = "sha512";

  static hash(password: string, salt: string) {
    return crypto
      .pbkdf2Sync(
        password,
        salt,
        this.CHIterations,
        this.CHKeyLength,
        this.CHDigest
      )
      .toString("hex");
  }

  static generate(password: string) {
    const salt = crypto.randomBytes(this.CHSaltBytes).toString("hex");
    const hash = this.hash(password, salt);
    return [salt, hash].join("$");
  }

  static verify(password: string, original: string) {
    const [salt, originalHash] = original.split("$");
    const hash = this.hash(password, salt);
    return hash === originalHash;
  }
}
