import { IsNotEmpty, validateOrReject } from 'class-validator';
import { BeforeInsert, BeforeUpdate, Column, Entity, Index, ObjectIdColumn } from 'typeorm';

@Entity('sdkGenerations')
export class CodeGeneration {
    @BeforeInsert()
    @BeforeUpdate()
    async validate() {
        try {
            await validateOrReject(this);
        } catch (e) {
            throw new Error(JSON.stringify(e, null, 2));
        }
    }
    @ObjectIdColumn()
        id: number;
    @Index({ unique: true })
    @Column()
    @IsNotEmpty()
        name: string;
    @Column()
    @IsNotEmpty()
        service: string;
    @Column()
    @IsNotEmpty()
        serviceType: string;
    @Column()
        resourcesToGenerate: string;
    @Column()
        tag: string;
    @Column()
    @IsNotEmpty()
        sdk: string;
    @Column()
    @IsNotEmpty()
        swaggerRepo: string;
    @Column()
    @IsNotEmpty()
        sdkRepo: string;
    @Column()
    @IsNotEmpty()
        codegenRepo: string;
    @Column()
    @IsNotEmpty()
        type: string;
    @Column()
        ignoreFailure: string;
    @Column()
        stages: string;
    @Column({ default: '' })
        lastPipelineBuildID: string;
    @Column()
        swaggerPR: string;
    @Column()
        codePR: string;
    @Column()
    @IsNotEmpty()
        status: string;
    @Column({ default: '' })
        owner: string;
}
