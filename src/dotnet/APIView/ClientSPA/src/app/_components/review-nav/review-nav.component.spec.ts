import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewNavComponent } from './review-nav.component';
import { TreeNode } from 'primeng/api';

describe('ReviewNavComponent', () => {
  let component: ReviewNavComponent;
  let fixture: ComponentFixture<ReviewNavComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ReviewNavComponent]
    });
    fixture = TestBed.createComponent(ReviewNavComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('isClientType', () => {
    it('should return true for labels ending with "client"', () => {
      const testCases = [
        { label: 'BlobServiceClient' },
        { label: 'StorageClient' },
        { label: 'KeyVaultClient' },
        { label: 'MyClient' },
        { label: 'client' },
        { label: 'CLIENT' }
      ];

      testCases.forEach((testCase) => {
        const node: TreeNode = { label: testCase.label };
        expect(component.isClientType(node)).toBeTruthy();
      });
    });

    it('should return false for labels not ending with "client"', () => {
      const testCases = [
        { label: 'BlobService' },
        { label: 'Configuration' },
        { label: 'SomeOtherType' },
        { label: 'clientService' },
        { label: 'clientType' },
        { label: '' }
      ];

      testCases.forEach((testCase) => {
        const node: TreeNode = { label: testCase.label };
        expect(component.isClientType(node)).toBeFalsy();
      });
    });

    it('should return false for undefined label', () => {
      const node: TreeNode = {};
      expect(component.isClientType(node)).toBeFalsy();
    });

    it('should be case-insensitive', () => {
      const testCases = [
        { label: 'MyClient' },
        { label: 'MyCLIENT' },
        { label: 'Myclient' },
        { label: 'myClient' }
      ];

      testCases.forEach((testCase) => {
        const node: TreeNode = { label: testCase.label };
        expect(component.isClientType(node)).toBeTruthy();
      });
    });
  });
});
