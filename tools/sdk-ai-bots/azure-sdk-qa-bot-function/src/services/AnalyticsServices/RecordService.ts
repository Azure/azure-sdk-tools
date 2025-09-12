import { TableEntity } from "@azure/data-tables";
import { AnalyticsServiceBase } from "./AnalyticsServiceBase";
import { TableService } from "../StorageService";

interface RecordData {
    submitTime: string;
    channelName: string;
    postId: string;
    link: string;
}

interface RecordTableEntity extends TableEntity, RecordData {}

export class RecordService extends AnalyticsServiceBase<
    RecordTableEntity,
    RecordData
> {
    private readonly tableName = "ConversationRecord";
    private tableService = new TableService(this.tableName);

    protected deserializeData(entities: RecordTableEntity[]): RecordData[] {
        return entities.map((entity) => ({
            submitTime: entity.submitTime,
            channelName: entity.channelName,
            postId: entity.postId,
            link: entity.link,
        }));
    }

    protected async getEntities(): Promise<RecordTableEntity[]> {
        return this.tableService.queryEntities<RecordTableEntity>({});
    }
}
