export function ComputeTokenDiff(beforeTokens: any[], afterTokens: any[]) : [any[], any[], boolean] {
  const diffResultA: any[] = [];
  const diffResultB: any[] = [];
  let hasDiff = false;

  const beforeTokensMap = new Map(beforeTokens.map((token, index) => [`${token.id}${token.value}${index}`, token]));
  const afterTokensMap = new Map(afterTokens.map((token, index) => [`${token.id}${token.value}${index}`, token]));

  beforeTokensMap.forEach((value, key) => {
    if (afterTokensMap.has(key)) {
      diffResultA.push({ ...value });
    } else {
      if (afterTokens.length > 0) {
        diffResultA.push({ ...value, renderClasses: new Set([...value.renderClasses, 'diff-change']) });
      } else {
        diffResultA.push({ ...value });
      }
      hasDiff = true;
    }
  });

  afterTokensMap.forEach((value, key) => {
    if (beforeTokensMap.has(key)) {
      diffResultB.push({ ...value });
    } else {
      if (beforeTokens.length > 0) {
        diffResultB.push({ ...value, renderClasses: new Set([...value.renderClasses, 'diff-change']) });
      } else {
        diffResultB.push({ ...value });
      }
      hasDiff = true;
    }
  });

  return [diffResultA, diffResultB, hasDiff];
}