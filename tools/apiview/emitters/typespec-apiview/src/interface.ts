/** Marks an object as being part of the ApiView server-side contract. */
export interface ApiViewSerializable {
  /** Serializes the object to the format ApiView expects. */
  serialize(): object;
}
