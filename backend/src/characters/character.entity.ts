import {
  Column,
  Entity,
  JoinColumn,
  ManyToOne,
  OneToOne,
  PrimaryGeneratedColumn,
  UpdateDateColumn
} from "typeorm";
import { Raider } from "../raiders/raider.entity";
import { Class } from "../wow/class.entity";
import { Race } from "../wow/race.entity";

@Entity()
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

  @OneToOne(() => Class)
  @JoinColumn()
  public class: Class;

  @OneToOne(() => Race)
  @JoinColumn()
  public race: Race;

  @Column({ nullable: true })
  public level?: number;

  @ManyToOne(
    () => Raider,
    raider => raider.characters
  )
  public raider: Raider;
}
