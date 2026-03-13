import { RaidCharacter } from "src/raid_characters/raid_character.entity";
import {
  Column,
  CreateDateColumn,
  Entity,
  JoinColumn,
  ManyToOne,
  OneToMany,
  PrimaryGeneratedColumn,
  UpdateDateColumn,
} from "typeorm";
import { WoWInstance } from "../wow/instance.entity";
import { Raider } from "../raiders/raider.entity";

export enum RaidVisibility {
  PUBLIC = "PUBLIC",
  GUILD = "GUILD",
}

@Entity()
export class Raid {
  @PrimaryGeneratedColumn()
  public id: number;

  @CreateDateColumn({ name: "start_time", type: "timestamp with time zone" })
  public startTime: Date;

  @UpdateDateColumn({ name: "updated_time", type: "timestamp with time zone" })
  public updatedTime: Date;

  @Column({ name: "signup_close_time", type: "timestamp with time zone" })
  public signupCloseTime: Date;

  @Column({ name: "description", type: "text", default: null })
  public description: string;

  @Column({ name: "mode", type: "text", default: null })
  public mode: string;

  @Column({
    type: "enum",
    enum: RaidVisibility,
    default: RaidVisibility.PUBLIC,
  })
  public visibility: RaidVisibility;

  @Column({ type: "text", name: "creator_guild", nullable: true })
  public creatorGuild?: string;

  @ManyToOne(
    () => WoWInstance,
    (instance) => instance.raids
  )
  @JoinColumn({ name: "instance" })
  public instance: WoWInstance;

  @ManyToOne(
    () => Raider,
    (raider) => raider.raids,
    { nullable: false, eager: true }
  )
  @JoinColumn({ name: "creator" })
  public creator: Raider;

  @OneToMany(
    () => RaidCharacter,
    (raidCharacter) => raidCharacter.raid
  )
  @JoinColumn({ name: "raid_characters" })
  public raidCharacters: RaidCharacter[];
}
