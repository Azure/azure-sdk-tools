import { ComputeTokenDiff } from "./review-page.worker";

describe('ComputeTokenDiff', () => {
  it('computes the token diff correctly', () => {
    // Arrange
    const beforeTokens: any[] = [
      { value: 'A', id: 1 },
      { value: 'B', id: 2 },
      { value: 'D', id: 4 },
      { value: 'F', id: 6 },
      { value: 'G', id: 7 }
    ];
    const afterTokens: any[] = [
        { value: 'A', id: 1 },
        { value: 'C', id: 3 },
        { value: 'D', id: 4 },
        { value: 'G', id: 7 }
    ];
    const expectedDiff: jasmine.Expected<jasmine.ArrayLike<any>> = [
      { value: 'A', id: 1, diffKind: 'Unchanged' },
      { value: 'B', id: 2, diffKind: 'Removed' },
      { value: 'C', id: 3, diffKind: 'Added' },
      { value: 'D', id: 4, diffKind: 'Unchanged' },
      { value: 'F', id: 6, diffKind: 'Removed' },
      { value: 'G', id: 7, diffKind: 'Unchanged' }
    ];

    // Act
    const diff : any = ComputeTokenDiff(beforeTokens, afterTokens);

    // Assert
    expect(diff).toEqual(expectedDiff);
  });
});