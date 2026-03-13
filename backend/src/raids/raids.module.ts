import { Module } from "@nestjs/common";
import { TypeOrmModule } from "@nestjs/typeorm";
import { RaidsService } from "./raids.service";
import { RaidsController } from "./raids.controller";
import { Raid } from "./raid.entity";
import { BattlenetModule } from "src/auth/battlenet/battlenet.module";

@Module({
  imports: [TypeOrmModule.forFeature([Raid]), BattlenetModule],
  controllers: [RaidsController],
  providers: [RaidsService]
})
export class RaidsModule {}
