export function hasWorkflowActions(actions: unknown): boolean {
  return typeof actions === "object" && actions !== null && Object.keys(actions).length > 0;
}