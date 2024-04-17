export function ComputeTokenDiff(beforeTokens: any[], afterTokens: any[]) : [any[], any[], boolean] {
  const diffResultA: any[] = [];
  const diffResultB: any[] = [];
  let hasDiff = false;

  const beforeTokensMap = new Map(beforeTokens.map((token, index) => [`${token.id}${token.value}${index}`, token]));
  const afterTokensMap = new Map(afterTokens.map((token, index) => [`${token.id}${token.value}${index}`, token]));

  beforeTokensMap.forEach((value, key) => {
    if (afterTokensMap.has(key)) {
      diffResultA.push({ ...value, diffKind: 'Unchanged' });
    } else {
      diffResultA.push({ ...value, diffKind: 'Removed' });
      hasDiff = true;
    }
  });

  afterTokensMap.forEach((value, key) => {
    if (beforeTokensMap.has(key)) {
      diffResultB.push({ ...value, diffKind: 'Unchanged' });
    } else {
      diffResultB.push({ ...value, diffKind: 'Added' });
      hasDiff = true;
    }
  });

  return [diffResultA, diffResultB, hasDiff];
}