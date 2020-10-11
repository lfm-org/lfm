import { Character } from "src/characters/character.entity";
import { Raid } from "src/raids/raid.entity";
import {
  Column,
  Entity,
  JoinColumn,
  ManyToOne,
  PrimaryGeneratedColumn
} from "typeorm";

export enum Attendance {
  NO = "NO",
  IF_ROOM = "IF_ROOM",
  YES = "YES"
}

@Entity()
export class RaidCharacter {
  @PrimaryGeneratedColumn()
  public id: number;

  @ManyToOne(
    () => Raid,
    raid => raid.raidCharacters,
    { cascade: true, nullable: false }
  )
  @JoinColumn({ name: "raid" })
  public raid: Raid;

  @ManyToOne(
    () => Character,
    character => character.raidCharacters,
    { cascade: true, nullable: false }
  )
  @JoinColumn({ name: "character" })
  public character: Character;

  @Column({
    type: "enum",
    enum: Attendance,
    default: Attendance.IF_ROOM,
    name: "desired_attendance",
    nullable: false
  })
  public desiredAttendance: Attendance;

  @Column({
    type: "enum",
    enum: Attendance,
    default: Attendance.IF_ROOM,
    name: "reviewed_attendance",
    nullable: false
  })
  public reviewedAttendance: Attendance;
}
