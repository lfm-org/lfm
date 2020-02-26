import { ForbiddenException, Injectable } from "@nestjs/common";
import { JwtService } from "@nestjs/jwt";
import * as bcrypt from "bcrypt";
import { Raider } from "src/raiders/raider.entity";
import { RaidersService } from "src/raiders/raiders.service";

@Injectable()
export class AuthService {
  private readonly saltRounds = 10;

  constructor(
    private readonly raidersService: RaidersService,
    private readonly jwtService: JwtService
  ) {}

  public async validateUser(username: string, password: string): Promise<any> {
    const raider = await this.raidersService.findOneByNameAuth(username);
    if (raider && (await bcrypt.compare(password, raider.passwordHash))) {
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
      await bcrypt.hash(body.password, this.saltRounds)
    );
    const payload = { username: newRaider.name, sub: newRaider.id };
    return {
      access_token: this.jwtService.sign(payload)
    };
  }
}
