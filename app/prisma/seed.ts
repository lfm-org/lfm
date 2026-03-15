import { PrismaClient } from "@prisma/client";
import { PrismaPg } from "@prisma/adapter-pg";

const url = process.env.DATABASE_URL;
if (!url) throw new Error("DATABASE_URL is not set");

const client = new PrismaClient({
  adapter: new PrismaPg({ connectionString: url }),
});

async function main(): Promise<void> {
  // Upsert the stub WowInstance required as a non-nullable FK on Raid
  await client.wowInstance.upsert({
    where: { id: 1 },
    update: {},
    create: {
      id: 1,
      name: "Test Instance",
      type: "RAID",
      minLevel: 1,
      expansionId: 1,
      modes: ["Normal"],
    },
  });

  // Upsert the test raider (also created by the callback stub at runtime,
  // but seeded here so cookie-seeded tests have a valid identity)
  const raider = await client.raider.upsert({
    where: { battleNetId: "test-bnet-id-hashed" },
    update: {},
    create: {
      battleNetId: "test-bnet-id-hashed",
      guildName: null,
    },
  });

  // Delete in FK-safe order: raidCharacters → raids
  await client.raidCharacter.deleteMany({});
  await client.raid.deleteMany({});

  const sevenDays = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000);
  const sixDays = new Date(Date.now() + 6 * 24 * 60 * 60 * 1000);

  await client.raid.createMany({
    data: [
      {
        description: "Alpha Raid",
        mode: "Normal",
        visibility: "PUBLIC",
        startTime: sevenDays,
        signupCloseTime: sixDays,
        instanceId: 1,
        creatorId: raider.id,
        creatorGuild: null,
      },
      {
        description: "Beta Raid",
        mode: "Heroic",
        visibility: "PUBLIC",
        startTime: sevenDays,
        signupCloseTime: sixDays,
        instanceId: 1,
        creatorId: raider.id,
        creatorGuild: null,
      },
      {
        description: "Gamma Raid",
        mode: "Mythic",
        visibility: "GUILD",
        startTime: sevenDays,
        signupCloseTime: sixDays,
        instanceId: 1,
        creatorId: raider.id,
        creatorGuild: "TestUser#1234",
      },
    ],
  });

  console.log("Seed complete: 1 WowInstance, 1 Raider, 3 Raids");
}

main()
  .catch((e) => {
    console.error(e);
    process.exit(1);
  })
  .finally(() => client.$disconnect());
