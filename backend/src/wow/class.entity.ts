import { Column, Entity, PrimaryColumn, UpdateDateColumn } from "typeorm";

@Entity("wow_class")
export class WoWClass {
  @PrimaryColumn()
  public id: number;

  @UpdateDateColumn({ name: "updated_time", type: "timestamp with time zone" })
  public updatedTime: Date;

  @Column({ type: "text", nullable: false })
  public name: string;

  constructor(playableClass?: WoWPlayableClass) {
    if (playableClass) {
      this.id = playableClass.id;
      this.name = playableClass.name.en_GB || "";
    }
  }
}
