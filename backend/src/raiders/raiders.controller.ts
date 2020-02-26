import { Controller, Get, Param, UseGuards } from "@nestjs/common";
import { JwtAuthGuard } from "src/auth/jwt-auth.guard";
import { RaidersService } from "./raiders.service";

@Controller("raiders")
export class RaidersController {
  constructor(private readonly raidersService: RaidersService) {}

  @UseGuards(JwtAuthGuard)
  @Get(":name")
  public async getRaider(@Param("name") name) {
    return { raider: await this.raidersService.findOneByName(name) };
  }
}
