import { Controller, Get, Param, ParseIntPipe } from "@nestjs/common";
import { RaidsService } from "./raids.service";

@Controller("raids")
export class RaidsController {
  constructor(private readonly raidsService: RaidsService) { }

  @Get()
  async getRaids() {
    return { raids: await this.raidsService.findAll() };
  }

  @Get(":id")
  async getRaid(@Param("id", ParseIntPipe) id: number) {
    return { raid: await this.raidsService.findOne(id) };
  }
}
