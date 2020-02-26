import { Module } from "@nestjs/common";
import { TypeOrmModule } from "@nestjs/typeorm";
import { RaidersService } from "./raiders.service";
import { Raider } from "./raider.entity";
import { RaidersController } from "./raiders.controller";

@Module({
  imports: [TypeOrmModule.forFeature([Raider])],
  controllers: [RaidersController],
  providers: [RaidersService],
  exports: [RaidersService]
})
export class RaidersModule {}
