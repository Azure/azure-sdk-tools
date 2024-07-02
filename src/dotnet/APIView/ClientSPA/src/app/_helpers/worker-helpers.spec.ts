import { ComputeTokenDiff } from "./worker-helpers";

describe('ComputeTokenDiff', () => {
  it('computes the token diff correctly', () => {
    // Arrange
    let beforeTokens: any[] = [
      { value: 'A', id: 1 },
      { value: 'B', id: 2 },
      { value: 'D', id: 4 },
      { value: 'F', id: 6 },
      { value: 'G', id: 7 }
    ];
    let afterTokens: any[] = [
        { value: 'A', id: 1 },
        { value: 'C', id: 3 },
        { value: 'D', id: 4 },
        { value: 'G', id: 7 }
    ];
    let expectedDiffA: jasmine.Expected<jasmine.ArrayLike<any>> = [
      { value: 'A', id: 1, diffKind: 'Unchanged' },
      { value: 'B', id: 2, diffKind: 'Removed' },
      { value: 'D', id: 4, diffKind: 'Unchanged' },
      { value: 'F', id: 6, diffKind: 'Removed' },
      { value: 'G', id: 7, diffKind: 'Removed' }
    ];

    let expectedDiffB: jasmine.Expected<jasmine.ArrayLike<any>> = [
      { value: 'A', id: 1, diffKind: 'Unchanged' },
      { value: 'C', id: 3, diffKind: 'Added' },
      { value: 'D', id: 4, diffKind: 'Unchanged' },
      { value: 'G', id: 7, diffKind: 'Added' }
    ];

    // Act
    let diff = ComputeTokenDiff(beforeTokens, afterTokens);

    // Assert
    expect(diff[0]).toEqual(expectedDiffA);
    expect(diff[1]).toEqual(expectedDiffB);
    expect(diff[2]).toBeTrue();

    diff = ComputeTokenDiff(beforeTokens, beforeTokens);

    let expectedDiffC: jasmine.Expected<jasmine.ArrayLike<any>> = [
      { value: 'A', id: 1, diffKind: 'Unchanged' },
      { value: 'B', id: 2, diffKind: 'Unchanged' },
      { value: 'D', id: 4, diffKind: 'Unchanged' },
      { value: 'F', id: 6, diffKind: 'Unchanged' },
      { value: 'G', id: 7, diffKind: 'Unchanged' }
    ];

    // Assert
    expect(diff[0]).toEqual(expectedDiffC);
    expect(diff[1]).toEqual(expectedDiffC);
    expect(diff[2]).toBeFalse();

    beforeTokens = [
      { value: 'namespace', id: null },
      { value: ' ', id: null },
      { value: 'Azure', id: "Azure" },
      { value: '.', id: null },
      { value: 'Identity', id: "Azure.Identity" },
      { value: ' ', id: null },
      { value: '{', id: null }
    ];

    expectedDiffC = [
      { value: 'namespace', id: null, diffKind: 'Unchanged' },
      { value: ' ', id: null, diffKind: 'Unchanged' },
      { value: 'Azure', id: "Azure", diffKind: 'Unchanged' },
      { value: '.', id: null, diffKind: 'Unchanged' },
      { value: 'Identity', id: "Azure.Identity", diffKind: 'Unchanged' },
      { value: ' ', id: null, diffKind: 'Unchanged' },
      { value: '{', id: null, diffKind: 'Unchanged' }
    ];

    diff = ComputeTokenDiff(beforeTokens, beforeTokens);

    // Assert
    expect(diff[0]).toEqual(expectedDiffC);
    expect(diff[1]).toEqual(expectedDiffC);
    expect(diff[2]).toBeFalse();
  });
});