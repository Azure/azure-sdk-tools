import convict from 'convict';

const AssertNullOrEmpty = (value: string) => {
    console.log(`get here`);
    if (!value || value.length === 0) {
        throw new Error(' must not be null or empty');
    }
};

const stringMustHaveLength = (value: string) => {
    console.log(`get here`);
    if (value.length === 0) {
        throw new Error('must not be empty');
    }
};

export class ResultPublisherBlobConfig {
    logsAndResultPath: string;
}

export const resultPublisherBlobConfig = convict<ResultPublisherBlobConfig>({
    logsAndResultPath: {
        default: null,
        format: AssertNullOrEmpty,
        env: 'LOGS_AND_RESULT_PATH',
        arg: 'logsAndResultPath',
    },
});
