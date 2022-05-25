import { Connection, MongoRepository } from 'typeorm';

import { CodeGeneration } from '../../types/codeGeneration';
import { CodeGenerationDao } from './codeGenerationDao';

export class CodeGenerationDaoImpl implements CodeGenerationDao {
    private repo: MongoRepository<CodeGeneration>;

    constructor(connection: Connection) {
        this.repo = connection.getMongoRepository(CodeGeneration);
    }

    public async getCodeGenerationByName(name: string): Promise<CodeGeneration> {
        const codegen = await this.repo.findOne({ name: name });
        return codegen;
    }

    public async submitCodeGeneration(codegen: CodeGeneration): Promise<void> {
        // findOneAndReplace will not trigger BeforeInsert and BeforeUpdate event, so validate here
        await codegen.validate();
        try {
            await this.repo.findOneAndReplace({ name: codegen.name }, codegen, { upsert: true });
        } catch (e) {
            console.error(`${e.message}
            ${e.stack}`);
        }
    }

    /* update code-gen information. */
    public async updateCodeGenerationValuesByName(name: string, values: any): Promise<any> {
        const codegen = await this.repo.findOne({ name: name });
        for (const key of Object.keys(values)) {
            codegen[key] = values[key];
        }
        await this.repo.save(codegen);
    }

    public async deleteCodeGenerationByName(name: string): Promise<any> {
        const codegen = await this.repo.findOne({ name: name });
        await this.repo.delete(codegen);
    }

    /* Get all code generations of an special onboard type. */
    public async listCodeGenerations(filters: any = undefined, filterCompleted = false): Promise<CodeGeneration[]> {
        let finalFilters: any;
        if (!filters) {
            filters = {};
        }
        if (filterCompleted) {
            finalFilters = {
                where: { $and: [{ status: { $ne: 'completed' } }, { status: { $ne: 'pipelineCompleted' } }, filters] }
            };
        } else {
            finalFilters = filters;
        }
        const codegens = await this.repo.find(finalFilters);
        return codegens;
    }

    /* update code-gen information. */
    public async updateCodeGenerationValueByName(name: string, key: string, value: string): Promise<any> {
        const codegen = await this.repo.findOne({ name: name });
        codegen[key] = value;
        await this.repo.save(codegen);
    }

    public async listCodeGenerationsByStatus(status: string): Promise<CodeGeneration[]> {
        const codegens = await this.repo.find({ status: status });
        return codegens;
    }
}
