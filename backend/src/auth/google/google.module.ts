import { Module } from "@nestjs/common";
import { RaidersModule } from "../../raiders/raiders.module";
import { GoogleController } from "./google.controller";
import { GoogleService } from "./google.service";

@Module({
  imports: [RaidersModule],
  controllers: [GoogleController],
  providers: [GoogleService],
})
export class GoogleModule {}
