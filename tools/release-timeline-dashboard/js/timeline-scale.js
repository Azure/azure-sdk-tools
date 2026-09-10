export function createTimelineScale(start, end) {
  const startTime = new Date(start).getTime();
  const endTime = new Date(end).getTime();
  const span = Math.max(1, endTime - startTime);
  return {
    position(value) {
      const ratio = (new Date(value).getTime() - startTime) / span;
      return Math.max(0, Math.min(100, ratio * 100));
    },
    ticks(count = 6) {
      return Array.from({ length: count }, (_, index) => {
        const ratio = index / (count - 1);
        return {
          position: ratio * 100,
          value: new Date(startTime + span * ratio),
        };
      });
    },
  };
}

export function median(values) {
  if (!values.length) return null;
  const sorted = [...values].sort((left, right) => left - right);
  return sorted[Math.floor((sorted.length - 1) / 2)];
}
