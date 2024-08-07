import { HttpClientTestingModule, HttpTestingController } from "@angular/common/http/testing";
import { ApiTreeBuilderData} from "../_models/revision";
import { TestBed } from "@angular/core/testing";
import { CodePanelRowData } from "../_models/codePanelModels";
import { ReviewPageWorkerMessageDirective } from "../_models/insertCodePanelRowDataMessage";

import contentWithDiffNodes from "./test-data/content-with-diff-nodes.json";
import contentWithDiffInOnlyDocs from "./test-data/content-with-diff-in-only-docs.json";
 
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
          expect(codePanelRowData.length).toBe(21);
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
          expect(codePanelRowData.length).toBe(171);
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
  });