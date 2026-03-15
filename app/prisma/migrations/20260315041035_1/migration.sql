-- CreateEnum
CREATE TYPE "RaidVisibility" AS ENUM ('PUBLIC', 'GUILD');

-- CreateEnum
CREATE TYPE "Attendance" AS ENUM ('NO', 'IF_ROOM', 'YES');

-- CreateTable
CREATE TABLE "raider" (
    "id" SERIAL NOT NULL,
    "created_time" TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_time" TIMESTAMPTZ(6) NOT NULL,
    "battle_net_id" TEXT NOT NULL,
    "guild_name" TEXT,
    "selected_character" INTEGER,

    CONSTRAINT "raider_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "character" (
    "id" SERIAL NOT NULL,
    "updated_time" TIMESTAMPTZ(6) NOT NULL,
    "region" TEXT NOT NULL,
    "realm" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "level" INTEGER,
    "class" INTEGER,
    "race" INTEGER,
    "portrait_url" TEXT,
    "raider" INTEGER NOT NULL,

    CONSTRAINT "character_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "raid" (
    "id" SERIAL NOT NULL,
    "start_time" TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_time" TIMESTAMPTZ(6) NOT NULL,
    "signup_close_time" TIMESTAMPTZ(6) NOT NULL,
    "description" TEXT,
    "mode" TEXT,
    "visibility" "RaidVisibility" NOT NULL DEFAULT 'PUBLIC',
    "creator_guild" TEXT,
    "instance" INTEGER NOT NULL,
    "creator" INTEGER NOT NULL,

    CONSTRAINT "raid_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "raid_character" (
    "id" SERIAL NOT NULL,
    "raid" INTEGER NOT NULL,
    "character" INTEGER NOT NULL,
    "desired_attendance" "Attendance" NOT NULL DEFAULT 'IF_ROOM',
    "reviewed_attendance" "Attendance" NOT NULL DEFAULT 'IF_ROOM',

    CONSTRAINT "raid_character_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "wow_class" (
    "id" INTEGER NOT NULL,
    "updated_time" TIMESTAMPTZ(6) NOT NULL,
    "name" TEXT NOT NULL,

    CONSTRAINT "wow_class_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "wow_race" (
    "id" INTEGER NOT NULL,
    "updated_time" TIMESTAMPTZ(6) NOT NULL,
    "faction" TEXT NOT NULL,
    "name" TEXT NOT NULL,

    CONSTRAINT "wow_race_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "wow_instance" (
    "id" INTEGER NOT NULL,
    "updated_time" TIMESTAMPTZ(6) NOT NULL,
    "name" TEXT NOT NULL,
    "type" TEXT NOT NULL,
    "minimum_level" INTEGER NOT NULL,
    "expansion_id" INTEGER NOT NULL,
    "modes" TEXT[],

    CONSTRAINT "wow_instance_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "wow_meta" (
    "id" SERIAL NOT NULL,
    "created_time" TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "success" BOOLEAN NOT NULL,

    CONSTRAINT "wow_meta_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "raider_battle_net_id_key" ON "raider"("battle_net_id");

-- CreateIndex
CREATE UNIQUE INDEX "character_region_realm_name_key" ON "character"("region", "realm", "name");

-- AddForeignKey
ALTER TABLE "raider" ADD CONSTRAINT "raider_selected_character_fkey" FOREIGN KEY ("selected_character") REFERENCES "character"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "character" ADD CONSTRAINT "character_class_fkey" FOREIGN KEY ("class") REFERENCES "wow_class"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "character" ADD CONSTRAINT "character_race_fkey" FOREIGN KEY ("race") REFERENCES "wow_race"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "character" ADD CONSTRAINT "character_raider_fkey" FOREIGN KEY ("raider") REFERENCES "raider"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "raid" ADD CONSTRAINT "raid_instance_fkey" FOREIGN KEY ("instance") REFERENCES "wow_instance"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "raid" ADD CONSTRAINT "raid_creator_fkey" FOREIGN KEY ("creator") REFERENCES "raider"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "raid_character" ADD CONSTRAINT "raid_character_raid_fkey" FOREIGN KEY ("raid") REFERENCES "raid"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "raid_character" ADD CONSTRAINT "raid_character_character_fkey" FOREIGN KEY ("character") REFERENCES "character"("id") ON DELETE RESTRICT ON UPDATE CASCADE;
