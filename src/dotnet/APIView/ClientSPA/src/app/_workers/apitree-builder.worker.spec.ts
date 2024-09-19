import { HttpClientTestingModule, HttpTestingController } from "@angular/common/http/testing";
import { ApiTreeBuilderData} from "../_models/revision";
import { TestBed } from "@angular/core/testing";
import { CodePanelRowData } from "../_models/codePanelModels";
import { ReviewPageWorkerMessageDirective } from "../_models/insertCodePanelRowDataMessage";

import contentWithDiffNodes from "./test-data/content-with-diff-nodes.json";
import contentWithDiffInOnlyDocs from "./test-data/content-with-diff-in-only-docs.json";
import contentWithActiveOnly from "./test-data/content-with-active-revision-only.json";
import contentWithFullDiff from "./test-data/content-with-diff-full-style.json";
import contentWithAddedOnly from "./test-data/content-with-only-added-diff.json";
import contentWithRemovedOnly from "./test-data/content-with-only-removed-diff.json";
import contentWithAttributeDiff from "./test-data/content-with-attribute-diff-only.json";
 
describe('API Tree Builder', () => {
    let httpMock: HttpTestingController;
    let apiTreeBuilder: Worker;
  
    beforeEach(() => {
      TestBed.configureTestingModule({
        imports: [HttpClientTestingModule],
      });

      httpMock = TestBed.inject(HttpTestingController);
      apiTreeBuilder = new Worker(new URL('./apitree-builder.worker', import.meta.url));
    });
  
    afterEach(() => {
      apiTreeBuilder.terminate();
      httpMock.verify();
    });

    it('should only show trees with diff', (done) => {
      apiTreeBuilder.onmessage = ({ data }) => {
        if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodePanelRowData) {
          const codePanelRowData = data.payload as CodePanelRowData[];
          expect(codePanelRowData.length).toBe(29);
          const linesWithDiff = codePanelRowData.filter(row => row.diffKind === 'removed' || row.diffKind === 'added');
          expect(linesWithDiff.length).toBe(6);
        }
        done();
      };

      apiTreeBuilder.onerror = (error) => {
        done.fail(error.message);
      };
  
      const apiTreeBuilderData : ApiTreeBuilderData = {
        diffStyle: 'trees',
        showDocumentation: false,
        showComments: true,
        showSystemComments: true,
        showHiddenApis: false
      };

      const jsonString = JSON.stringify(contentWithDiffNodes);
      const encoder = new TextEncoder();
      const arrayBuffer = encoder.encode(jsonString).buffer;
  
      apiTreeBuilder.postMessage(apiTreeBuilderData);
      apiTreeBuilder.postMessage(arrayBuffer);
    });

    it('should only show nodes with diff', (done) => {
      apiTreeBuilder.onmessage = ({ data }) => {
        if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodePanelRowData) {
          const codePanelRowData = data.payload as CodePanelRowData[];
          expect(codePanelRowData.length).toBe(24);
          const linesWithDiff = codePanelRowData.filter(row => row.diffKind === 'removed' || row.diffKind === 'added');
          expect(linesWithDiff.length).toBe(6);
        }
        done();
      };
  
      apiTreeBuilder.onerror = (error) => {
        done.fail(error.message);
      };
  
      const apiTreeBuilderData : ApiTreeBuilderData = {
        diffStyle: 'nodes',
        showDocumentation: false,
        showComments: true,
        showSystemComments: true,
        showHiddenApis: false
      };
  
      const jsonString = JSON.stringify(contentWithDiffNodes);
      const encoder = new TextEncoder();
      const arrayBuffer = encoder.encode(jsonString).buffer;
  
      apiTreeBuilder.postMessage(apiTreeBuilderData);
      apiTreeBuilder.postMessage(arrayBuffer);
    });

    it ('should show doc diff when doc is enabled', (done) => {
      apiTreeBuilder.onmessage = ({ data }) => {
        if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodePanelRowData) {
          const codePanelRowData = data.payload as CodePanelRowData[];
          expect(codePanelRowData.length).toBe(174);
          const linesWithDiff = codePanelRowData.filter(row => row.diffKind === 'removed' || row.diffKind === 'added');
          expect(linesWithDiff.length).toBe(152);
        }
        done();
      };
  
      apiTreeBuilder.onerror = (error) => {
        done.fail(error.message);
      };
  
      const apiTreeBuilderData : ApiTreeBuilderData = {
        diffStyle: 'nodes',
        showDocumentation: true,
        showComments: true,
        showSystemComments: true,
        showHiddenApis: false
      };
  
      const jsonString = JSON.stringify(contentWithDiffInOnlyDocs);
      const encoder = new TextEncoder();
      const arrayBuffer = encoder.encode(jsonString).buffer;
  
      apiTreeBuilder.postMessage(apiTreeBuilderData);
      apiTreeBuilder.postMessage(arrayBuffer);
    });

    it ('should not show node with diff in only docs when doc is not enabled', (done) =>{
      apiTreeBuilder.onmessage = ({ data }) => {
        if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodePanelRowData) {
          const codePanelRowData = data.payload as CodePanelRowData[];
          expect(codePanelRowData.length).toBe(5);
          const linesWithDiff = codePanelRowData.filter(row => row.diffKind === 'removed' || row.diffKind === 'added');
          expect(linesWithDiff.length).toBe(2);
        }
        done();
      };
  
      apiTreeBuilder.onerror = (error) => {
        done.fail(error.message);
      };
  
      const apiTreeBuilderData : ApiTreeBuilderData = {
        diffStyle: 'nodes',
        showDocumentation: false,
        showComments: true,
        showSystemComments: true,
        showHiddenApis: false
      };
  
      const jsonString = JSON.stringify(contentWithDiffInOnlyDocs);
      const encoder = new TextEncoder();
      const arrayBuffer = encoder.encode(jsonString).buffer;
  
      apiTreeBuilder.postMessage(apiTreeBuilderData);
      apiTreeBuilder.postMessage(arrayBuffer);
    });

    it('test number lines in no diff without docs', (done) => {
      apiTreeBuilder.onmessage = ({ data }) => {
        if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodePanelRowData) {
          const codePanelRowData = data.payload as CodePanelRowData[];
          expect(codePanelRowData.length).toBe(41);
          const linesWithDiff = codePanelRowData.filter(row => row.diffKind === 'removed' || row.diffKind === 'added');
          expect(linesWithDiff.length).toBe(0);
        }
        done();
      };

      apiTreeBuilder.onerror = (error) => {
        done.fail(error.message);
      };
  
      const apiTreeBuilderData : ApiTreeBuilderData = {
        diffStyle: 'full',
        showDocumentation: false,
        showComments: false,
        showSystemComments: false,
        showHiddenApis: false
      };

      const jsonString = JSON.stringify(contentWithActiveOnly);
      const encoder = new TextEncoder();
      const arrayBuffer = encoder.encode(jsonString).buffer;
  
      apiTreeBuilder.postMessage(apiTreeBuilderData);
      apiTreeBuilder.postMessage(arrayBuffer);
    });

    it('test number lines in no diff with docs', (done) => {
      apiTreeBuilder.onmessage = ({ data }) => {
        if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodePanelRowData) {
          const codePanelRowData = data.payload as CodePanelRowData[];
          expect(codePanelRowData.length).toBe(162);
          const linesWithDiff = codePanelRowData.filter(row => row.diffKind === 'removed' || row.diffKind === 'added');
          expect(linesWithDiff.length).toBe(0);
        }
        done();
      };

      apiTreeBuilder.onerror = (error) => {
        done.fail(error.message);
      };
  
      const apiTreeBuilderData : ApiTreeBuilderData = {
        diffStyle: 'full',
        showDocumentation: true,
        showComments: false,
        showSystemComments: false,
        showHiddenApis: false
      };

      const jsonString = JSON.stringify(contentWithActiveOnly);
      const encoder = new TextEncoder();
      const arrayBuffer = encoder.encode(jsonString).buffer;
  
      apiTreeBuilder.postMessage(apiTreeBuilderData);
      apiTreeBuilder.postMessage(arrayBuffer);
    });


    it('test diff lines in full diff without docs', (done) => {
      apiTreeBuilder.onmessage = ({ data }) => {
        if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodePanelRowData) {
          const codePanelRowData = data.payload as CodePanelRowData[];
          expect(codePanelRowData.length).toBe(43);
          const linesWithDiff = codePanelRowData.filter(row => row.diffKind === 'removed' || row.diffKind === 'added');
          expect(linesWithDiff.length).toBe(9);
        }
        done();
      };

      apiTreeBuilder.onerror = (error) => {
        done.fail(error.message);
      };
  
      const apiTreeBuilderData : ApiTreeBuilderData = {
        diffStyle: 'full',
        showDocumentation: false,
        showComments: false,
        showSystemComments: false,
        showHiddenApis: false
      };

      const jsonString = JSON.stringify(contentWithFullDiff);
      const encoder = new TextEncoder();
      const arrayBuffer = encoder.encode(jsonString).buffer;
  
      apiTreeBuilder.postMessage(apiTreeBuilderData);
      apiTreeBuilder.postMessage(arrayBuffer);
    });

    it('test diff lines in full diff with docs', (done) => {
      apiTreeBuilder.onmessage = ({ data }) => {
        if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodePanelRowData) {
          const codePanelRowData = data.payload as CodePanelRowData[];
          expect(codePanelRowData.length).toBe(202);
          const linesWithDiff = codePanelRowData.filter(row => row.diffKind === 'removed' || row.diffKind === 'added');
          expect(linesWithDiff.length).toBe(97);
        }
        done();
      };

      apiTreeBuilder.onerror = (error) => {
        done.fail(error.message);
      };
  
      const apiTreeBuilderData : ApiTreeBuilderData = {
        diffStyle: 'full',
        showDocumentation: true,
        showComments: false,
        showSystemComments: false,
        showHiddenApis: false
      };

      const jsonString = JSON.stringify(contentWithFullDiff);
      const encoder = new TextEncoder();
      const arrayBuffer = encoder.encode(jsonString).buffer;
  
      apiTreeBuilder.postMessage(apiTreeBuilderData);
      apiTreeBuilder.postMessage(arrayBuffer);
    });

    it('test diff lines in tree style diff without docs', (done) => {
      apiTreeBuilder.onmessage = ({ data }) => {
        if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodePanelRowData) {
          const codePanelRowData = data.payload as CodePanelRowData[];
          expect(codePanelRowData.length).toBe(27);
          const linesWithDiff = codePanelRowData.filter(row => row.diffKind === 'removed' || row.diffKind === 'added');
          expect(linesWithDiff.length).toBe(9);
        }
        done();
      };

      apiTreeBuilder.onerror = (error) => {
        done.fail(error.message);
      };
  
      const apiTreeBuilderData : ApiTreeBuilderData = {
        diffStyle: 'trees',
        showDocumentation: false,
        showComments: false,
        showSystemComments: false,
        showHiddenApis: false
      };

      const jsonString = JSON.stringify(contentWithFullDiff);
      const encoder = new TextEncoder();
      const arrayBuffer = encoder.encode(jsonString).buffer;
  
      apiTreeBuilder.postMessage(apiTreeBuilderData);
      apiTreeBuilder.postMessage(arrayBuffer);
    });
    it('test diff lines in node style diff without docs', (done) => {
      apiTreeBuilder.onmessage = ({ data }) => {
        if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodePanelRowData) {
          const codePanelRowData = data.payload as CodePanelRowData[];
          expect(codePanelRowData.length).toBe(21);
          const linesWithDiff = codePanelRowData.filter(row => row.diffKind === 'removed' || row.diffKind === 'added');
          expect(linesWithDiff.length).toBe(9);
        }
        done();
      };

      apiTreeBuilder.onerror = (error) => {
        done.fail(error.message);
      };
  
      const apiTreeBuilderData : ApiTreeBuilderData = {
        diffStyle: 'nodes',
        showDocumentation: false,
        showComments: false,
        showSystemComments: false,
        showHiddenApis: false
      };

      const jsonString = JSON.stringify(contentWithFullDiff);
      const encoder = new TextEncoder();
      const arrayBuffer = encoder.encode(jsonString).buffer;
  
      apiTreeBuilder.postMessage(apiTreeBuilderData);
      apiTreeBuilder.postMessage(arrayBuffer);
    });

    it('test diff lines with added diff only', (done) => {
      apiTreeBuilder.onmessage = ({ data }) => {
        if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodePanelRowData) {
          const codePanelRowData = data.payload as CodePanelRowData[];
          expect(codePanelRowData.length).toBe(460);
          const linesWithDiff = codePanelRowData.filter(row => row.diffKind === 'removed' || row.diffKind === 'added');
          expect(linesWithDiff.length).toBe(1);
        }
        done();
      };

      apiTreeBuilder.onerror = (error) => {
        done.fail(error.message);
      };
  
      const apiTreeBuilderData : ApiTreeBuilderData = {
        diffStyle: 'full',
        showDocumentation: false,
        showComments: false,
        showSystemComments: false,
        showHiddenApis: false
      };

      const jsonString = JSON.stringify(contentWithAddedOnly);
      const encoder = new TextEncoder();
      const arrayBuffer = encoder.encode(jsonString).buffer;
  
      apiTreeBuilder.postMessage(apiTreeBuilderData);
      apiTreeBuilder.postMessage(arrayBuffer);
    });

    it('test diff lines with removed diff only', (done) => {
      apiTreeBuilder.onmessage = ({ data }) => {
        if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodePanelRowData) {
          const codePanelRowData = data.payload as CodePanelRowData[];
          expect(codePanelRowData.length).toBe(460);
          const linesWithDiff = codePanelRowData.filter(row => row.diffKind === 'removed' || row.diffKind === 'added');
          expect(linesWithDiff.length).toBe(1);
        }
        done();
      };

      apiTreeBuilder.onerror = (error) => {
        done.fail(error.message);
      };
  
      const apiTreeBuilderData : ApiTreeBuilderData = {
        diffStyle: 'full',
        showDocumentation: false,
        showComments: false,
        showSystemComments: false,
        showHiddenApis: false
      };

      const jsonString = JSON.stringify(contentWithRemovedOnly);
      const encoder = new TextEncoder();
      const arrayBuffer = encoder.encode(jsonString).buffer;
  
      apiTreeBuilder.postMessage(apiTreeBuilderData);
      apiTreeBuilder.postMessage(arrayBuffer);
    });

    it('Test Only Attribute line diff', (done) => {
      apiTreeBuilder.onmessage = ({ data }) => {
        if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodePanelRowData) {
          const codePanelRowData = data.payload as CodePanelRowData[];
          expect(codePanelRowData.length).toBe(467);
          const linesWithDiff = codePanelRowData.filter(row => row.diffKind === 'removed' || row.diffKind === 'added');
          linesWithDiff.forEach(element => {
            console.log(element);
          });
          
          expect(linesWithDiff.length).toBe(3);
        }
        done();
      };
  
      apiTreeBuilder.onerror = (error) => {
        done.fail(error.message);
      };
  
      const apiTreeBuilderData : ApiTreeBuilderData = {
        diffStyle: 'full',
        showDocumentation: false,
        showComments: false,
        showSystemComments: false,
        showHiddenApis: false
      };
      const jsonString = JSON.stringify(contentWithAttributeDiff);
      const encoder = new TextEncoder();
      const arrayBuffer = encoder.encode(jsonString).buffer;
  
      apiTreeBuilder.postMessage(apiTreeBuilderData);
      apiTreeBuilder.postMessage(arrayBuffer);
    });
  });