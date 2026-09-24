export class IngestionError extends Error {
  constructor(status, code, message) {
    super(message);
    this.name = "IngestionError";
    this.status = status;
    this.code = code;
  }
}