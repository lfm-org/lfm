import { Module } from "@nestjs/common";
import { HttpModule } from "@nestjs/axios";
import { RaidersModule } from "../../raiders/raiders.module";
import { BattlenetController } from "./battlenet.controller";
import { BattlenetService } from "./battlenet.service";
import { BattlenetAuthGuard } from "./battlenet.guard";

@Module({
  imports: [HttpModule, RaidersModule],
  controllers: [BattlenetController],
  providers: [BattlenetService, BattlenetAuthGuard],
  exports: [BattlenetService, BattlenetAuthGuard],
})
export class BattlenetModule {}
