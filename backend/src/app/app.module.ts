import { Module } from "@nestjs/common";
import { ConfigModule, ConfigService } from "@nestjs/config";
import { TypeOrmModule, TypeOrmModuleOptions } from "@nestjs/typeorm";
import { CharactersModule } from "src/characters/characters.module";
import { AuthModule } from "src/auth/auth.module";
import { WoWModule } from "src/wow/wow.module";
import { RaidsModule } from "src/raids/raids.module";
import { RaidersModule } from "src/raiders/raiders.module";

@Module({
  imports: [
    ConfigModule.forRoot(),
    TypeOrmModule.forRootAsync({
      useFactory: (config: ConfigService): TypeOrmModuleOptions => ({
        type: "postgres",
        host: config.get("TYPEORM_HOST", "localhost"),
        port: config.get<number>("TYPEORM_PORT", 5432),
        username: config.get("TYPEORM_USERNAME", "test"),
        password: config.get("TYPEORM_PASSWORD", "test"),
        database: config.get("TYPEORM_DATABASE", "test"),
        entities: JSON.parse(config.get("TYPEORM_ENTITIES", "[]")),
        migrations: JSON.parse(config.get("TYPEORM_MIGRATIONS", "[]")),
        migrationsRun: config.get("TYPEORM_MIGRATIONS_RUN", false),
        logging: config.get("TYPEORM_LOGGING", false),
        dropSchema: config.get("TYPEORM_DROP_SCHEMA", false),
        synchronize: config.get("TYPEORM_SYNCHRONIZE", false)
      }),
      imports: [ConfigModule],
      inject: [ConfigService]
    }),
    CharactersModule,
    AuthModule,
    RaidersModule,
    RaidsModule,
    WoWModule
  ]
})
export class AppModule {}
