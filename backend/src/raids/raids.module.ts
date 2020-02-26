import { Module } from "@nestjs/common";
import { TypeOrmModule } from "@nestjs/typeorm";
import { RaidsService } from "./raids.service";
import { RaidsController } from "./raids.controller";
import { Raid } from "./raid.entity";

@Module({
  imports: [TypeOrmModule.forFeature([Raid])],
  controllers: [RaidsController],
  providers: [RaidsService]
})
export class RaidsModule {}
