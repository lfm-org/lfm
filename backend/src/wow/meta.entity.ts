import {
  Column,
  CreateDateColumn,
  Entity,
  PrimaryGeneratedColumn,
} from "typeorm";

@Entity("wow_meta")
export class WoWMeta {
  @PrimaryGeneratedColumn()
  public id: number;

  @CreateDateColumn({ name: "created_time", type: "timestamp with time zone" })
  public createdTime: Date;

  @Column()
  public success: boolean;
}
