import { default as ajvInit, ValidateFunction } from 'ajv';
import * as TriggerType from './TriggerType';
import * as InstallInstructionScriptInput from './InstallInstructionScriptInput';
import * as InstallInstructionScriptOutput from './InstallInstructionScriptOutput';

const ajv = ajvInit({
  coerceTypes: true,
  messages: true,
  verbose: true,
  useDefaults: true
});

let schemaAdded = false;
const addSchema = () => {
  if (!schemaAdded) {
    schemaAdded = true;
    ajv.addSchema(TriggerType.triggerTypeSchema);
    ajv.addSchema(InstallInstructionScriptInput.installInstructionScriptInputSchema);
    ajv.addSchema(InstallInstructionScriptOutput.installInstructionScriptOutputSchema);
  }
};

export const getTypeTransformer = <T>(schema: object, name: string) => {
  let validator: ValidateFunction | undefined;
  return (obj: unknown) => {
    addSchema();
    if (validator === undefined) {
      validator = ajv.compile(schema);
    }
    if (!validator(obj)) {
      const error = validator.errors![0];
      throw new Error(`ConfigError: Invalid ${name}: ${error.dataPath} ${error.message}. If the SDK artifacts haven't been successfully generated, please fix the errors to ensure they are generated correctly. Refer to the schema definitions at https://github.com/Azure/azure-rest-api-specs/tree/main/documentation/sdkautomation for guidance.`);
    }

    return obj as T;
  };
};
