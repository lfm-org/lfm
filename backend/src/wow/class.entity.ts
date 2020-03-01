import { Column, Entity, PrimaryColumn, UpdateDateColumn } from "typeorm";

@Entity()
export class Class {
  @PrimaryColumn()
  public id: number;

  @UpdateDateColumn({ name: "updated_time", type: "timestamp with time zone" })
  public updatedTime: Date;

  @Column({ type: "text", nullable: false })
  public name: string;
}
