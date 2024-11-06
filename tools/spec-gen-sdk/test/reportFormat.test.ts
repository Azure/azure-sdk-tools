import { formatSuppressionLine } from '../src/utils/reportFormat';

// To invoke these tests, run `npm run test-reportFormat` from the "private/openapi-sdk-automation" directory.
describe('formatSuppressionLine', () => {
  it('easy case', () => {
    const suppressionLines = ['abc'];
    const formatResult = formatSuppressionLine(suppressionLines);
    expect(formatResult).toContain('- abc');
  })

  it('case with /t', () => {
    const suppressionLines = ['+\tFunction `*LinkerClient.BeginCreateOrUpdate` has been removed'];
    const formatResult = formatSuppressionLine(suppressionLines);
    expect(formatResult).toContain('- Function `*LinkerClient.BeginCreateOrUpdate` has been removed');
  })

  it('case by using object', () => {
    const suppressionLines = ["+\tType of parameter headers of interface WebHookActivity is changed from {\n        [propertyName: string]: string;\n    } to {\n        [propertyName: string]: any;\n    }"];
    const formatResult = formatSuppressionLine(suppressionLines);
    expect(formatResult).toContain('- "Type of parameter headers of interface WebHookActivity is changed from {\\n        [propertyName: string]: string;\\n    } to {\\n        [propertyName: string]: any;\\n    }"');
  })

});
