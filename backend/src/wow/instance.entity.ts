import { Raid } from "src/raids/raid.entity";
import {
  Column,
  Entity,
  OneToMany,
  PrimaryColumn,
  UpdateDateColumn,
} from "typeorm";

@Entity("wow_instance")
export class WoWInstance {
  @PrimaryColumn()
  public id: number;

  @UpdateDateColumn({ name: "updated_time", type: "timestamp with time zone" })
  public updatedTime: Date;

  @Column({ type: "text", nullable: false })
  public name: string;

  @Column({ type: "text", nullable: false })
  public type: string;

  @Column({ name: "minimum_level", nullable: false })
  public minLevel: number;

  @Column({ name: "expansion_id", nullable: false })
  public expansionId: number;

  @Column({ type: "text", array: true, nullable: false })
  public modes: string[];

  @OneToMany(
    () => Raid,
    (raid) => raid.instance
  )
  public raids: Raid[];

  constructor(journalInstance?: WoWJournalInstance) {
    if (journalInstance) {
      this.id = journalInstance.id;
      this.name = journalInstance.name.en_GB || "";
      this.type = journalInstance.category?.type || "";
      this.minLevel = journalInstance.minimum_level || 0;
      this.expansionId = journalInstance.expansion?.id || 0;
      this.modes =
        journalInstance.modes?.map(
          (modeEntry) => modeEntry.mode.name.en_GB || ""
        ) || [];
      this;
    }
  }
}
