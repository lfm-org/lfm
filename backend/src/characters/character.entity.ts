import { RaidCharacter } from "src/raid_characters/raid_character.entity";
import {
  Column,
  Entity,
  JoinColumn,
  ManyToOne,
  OneToMany,
  PrimaryGeneratedColumn,
  Unique,
  UpdateDateColumn
} from "typeorm";
import { Raider } from "../raiders/raider.entity";
import { Class } from "../wow/class.entity";
import { Race } from "../wow/race.entity";

@Entity()
@Unique("unique_region_realm_name", ["region", "realm", "name"])
export class Character {
  @PrimaryGeneratedColumn()
  public id: number;

  @UpdateDateColumn({ name: "updated_time", type: "timestamp with time zone" })
  public updatedTime: Date;

  @Column({ type: "text", nullable: false })
  public region: string;

  @Column({ type: "text", nullable: false })
  public realm: string;

  @Column({ type: "text", nullable: false })
  public name: string;

  @ManyToOne(() => Class, { nullable: false, eager: true })
  @JoinColumn({ name: "class" })
  public class: Class;

  @ManyToOne(() => Race, { nullable: false, eager: true })
  @JoinColumn({ name: "race" })
  public race: Race;

  @Column({ nullable: true })
  public level?: number;

  @ManyToOne(
    () => Raider,
    raider => raider.characters,
    { cascade: true }
  )
  @JoinColumn({ name: "raider" })
  public raider: Raider;

  @OneToMany(
    () => RaidCharacter,
    raidCharacter => raidCharacter.raid
  )
  @JoinColumn({ name: "raid_characters" })
  public raidCharacters: RaidCharacter[];
}
