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

  @ManyToOne(
    () => WoWInstance,
    (instance) => instance.raids
  )
  @JoinColumn({ name: "instance" })
  public instance: WoWInstance;

  @OneToMany(
    () => RaidCharacter,
    (raidCharacter) => raidCharacter.raid
  )
  @JoinColumn({ name: "raid_characters" })
  public raidCharacters: RaidCharacter[];
}
