import {
  Controller,
  Get,
  Param,
  ParseIntPipe,
  Req,
  NotFoundException,
  UseGuards,
} from "@nestjs/common";
import { RaidsService } from "./raids.service";
import { BattlenetAuthGuard } from "../auth/battlenet/battlenet.guard";
import { BattleNetRequest } from "../auth/battlenet/battle-net-request.interface";

@Controller("raids")
export class RaidsController {
  constructor(private readonly raidsService: RaidsService) {}

  @UseGuards(BattlenetAuthGuard)
  @Get()
  async getRaids(@Req() req: BattleNetRequest) {
    return {
      raids: await this.raidsService.findAll(
        req.battleNetIdentity?.guildName
      ),
    };
  }

  @UseGuards(BattlenetAuthGuard)
  @Get(":id")
  async getRaid(@Req() req: BattleNetRequest, @Param("id", ParseIntPipe) id: number) {
    const raid = await this.raidsService.findOne(
      id,
      req.battleNetIdentity?.guildName
    );
    if (!raid) {
      throw new NotFoundException(`Raid ${id} not found`);
    }
    return { raid };
  }
}
