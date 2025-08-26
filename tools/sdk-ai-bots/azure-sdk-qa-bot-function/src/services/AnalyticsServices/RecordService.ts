import { TableEntity } from "@azure/data-tables";
import { AnalyticsServiceBase } from "./AnalyticsServiceBase";

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
    constructor(tableName: string = "feedback") {
        super(tableName);
    }

    protected deserializeData(entity: RecordTableEntity): RecordData {
        return {
            submitTime: entity.submitTime,
            channelName: entity.channelName,
            postId: entity.postId,
            link: entity.link,
        };
    }
}
