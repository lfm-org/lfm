import { Controller, Get } from "@nestjs/common";
import { RaidsService } from "./raids.service";

@Controller("raids")
export class RaidsController {
  constructor(private readonly raidsService: RaidsService) {}

  @Get()
  async getRaids() {
    return { raids: await this.raidsService.findAll() };
  }
}
