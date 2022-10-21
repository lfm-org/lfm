import { NestFactory } from "@nestjs/core";
import { AppModule } from "./app/app.module";
import { NestExpressApplication } from "@nestjs/platform-express";
import * as cookieParser from "cookie-parser";

async function bootstrap() {
  const app = await NestFactory.create<NestExpressApplication>(AppModule, {
    cors: true,
  });
  app.use(cookieParser());
  await app.listen(3000);
}
bootstrap();
