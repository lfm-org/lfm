import { Raid } from "src/raids/raid.entity";
import {
  Column,
  Entity,
  OneToMany,
  PrimaryColumn,
  UpdateDateColumn
} from "typeorm";

@Entity()
export class Instance {
  @PrimaryColumn()
  public id: number;

  @UpdateDateColumn({ name: "updated_time", type: "timestamp with time zone" })
  public updatedTime: Date;

  @Column({ type: "text", nullable: false })
  public name: string;

  @Column({ type: "text", array: true, nullable: false })
  public modes: string[];

  @Column({ type: "text", nullable: false })
  public type: string;

  @Column({ name: "minimum_level", nullable: false })
  public minLevel: number;

  @OneToMany(
    () => Raid,
    raid => raid.instance
  )
  public raids: Raid[];
}
