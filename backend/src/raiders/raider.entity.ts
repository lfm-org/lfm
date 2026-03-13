import {
  Column,
  CreateDateColumn,
  Entity,
  Index,
  OneToMany,
  PrimaryGeneratedColumn,
  UpdateDateColumn,
} from "typeorm";
import { Character } from "../characters/character.entity";
import { Raid } from "../raids/raid.entity";

@Entity()
export class Raider {
  @PrimaryGeneratedColumn()
  public id: number;

  @CreateDateColumn({ name: "created_time", type: "timestamp with time zone" })
  public createdTime: Date;

  @UpdateDateColumn({ name: "updated_time", type: "timestamp with time zone" })
  public updatedTime: Date;

  @OneToMany(
    () => Character,
    (character) => character.raider
  )
  public characters: Character[];

  @Column({ type: "text", nullable: true })
  public name?: string;

  @Column({ type: "text", name: "battle_tag", nullable: true })
  public battleTag?: string;

  @Column({
    type: "text",
    name: "battle_net_id",
    nullable: true,
    select: false,
  })
  @Index({ nullFiltered: true, unique: true })
  public battleNetId?: string;

  @Column({ type: "text", name: "guild_name", nullable: true })
  public guildName?: string;

  @OneToMany(
    () => Raid,
    (raid) => raid.creator
  )
  public raids: Raid[];
}
