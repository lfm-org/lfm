import { Column, Entity, PrimaryColumn, UpdateDateColumn } from "typeorm";

@Entity("wow_race")
export class WoWRace {
  @PrimaryColumn()
  public id: number;

  @UpdateDateColumn({ name: "updated_time", type: "timestamp with time zone" })
  public updatedTime: Date;

  @Column({ type: "text", nullable: false })
  public faction: string;

  @Column({ type: "text", nullable: false })
  public name: string;

  constructor(playableRace?: WoWPlayableRace) {
    if (playableRace) {
      this.id = playableRace.id;
      this.name = playableRace.name.en_GB || "";
      this.faction = playableRace.faction.type;
    }
  }
}
