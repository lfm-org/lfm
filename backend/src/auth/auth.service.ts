import { ForbiddenException, Injectable } from "@nestjs/common";
import { JwtService } from "@nestjs/jwt";
import { Raider } from "src/raiders/raider.entity";
import { RaidersService } from "src/raiders/raiders.service";
import CryptoHelpers from "./utility.crypto";

@Injectable()
export class AuthService {
  constructor(
    private readonly raidersService: RaidersService,
    private readonly jwtService: JwtService
  ) {}

  public async validateUser(username: string, password: string): Promise<any> {
    const raider = await this.raidersService.findOneByNameAuth(username);
    if (raider && CryptoHelpers.verify(password, raider.passwordHash)) {
      return raider;
    }
    return null;
  }

  public async login(user: Partial<Raider>) {
    const payload = { username: user.name, sub: user.id };
    return {
      access_token: this.jwtService.sign(payload)
    };
  }

  public async register(body: any) {
    const existingRaider = await this.raidersService.findOneByName(body.name);
    if (existingRaider) {
      throw new ForbiddenException();
    }
    const newRaider = await this.raidersService.create(
      body.name,
      CryptoHelpers.generate(body.password)
    );
    const payload = { username: newRaider.name, sub: newRaider.id };
    return {
      access_token: this.jwtService.sign(payload)
    };
  }
}
