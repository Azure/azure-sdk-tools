export class DoublyLinkedList<T> {
  head: DoublyLinkedListNode<T> | undefined = undefined;
  tail: DoublyLinkedListNode<T> | undefined = undefined;
  length: number = 0;

  append(value: T): void {
    const newNode = new DoublyLinkedListNode(value, this.length);
    if (!this.head) {
      this.head = newNode;
      this.tail = newNode;
    } else {
      if (this.tail) {
        this.tail.next = newNode;
        newNode.prev = this.tail;
        this.tail = newNode;
      }
    }
    this.length++;
  }
}

export class DoublyLinkedListNode<T> {
  value: T;
  next: DoublyLinkedListNode<T> | undefined = undefined;
  prev: DoublyLinkedListNode<T> | undefined = undefined;
  index: number;
  
  constructor(value: T, index: number) {
    this.value = value;
    this.index = index;
  }
}