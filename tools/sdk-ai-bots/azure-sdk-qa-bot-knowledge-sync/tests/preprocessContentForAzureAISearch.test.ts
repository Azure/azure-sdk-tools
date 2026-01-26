import { describe, it, expect } from 'vitest';
import { preprocessContentForAzureAISearch } from '../src/DailySyncKnowledge';

describe('preprocessContentForAzureAISearch', () => {
    describe('Code block conversion', () => {
        it('should escape ``` to prevent parser issues', () => {
            const input = `Some text
\`\`\`
code line 1
code line 2
\`\`\`
More text`;

            const expected = String.raw`Some text
\`\`\`
code line 1
code line 2
\`\`\`
More text`;

            expect(preprocessContentForAzureAISearch(input)).toBe(expected);
        });

        it('should replace ``` with language identifier', () => {
            const input = `\`\`\`python
def hello():
    print("world")
\`\`\``;

            const expected = String.raw`\`\`\`python
def hello():
    print("world")
\`\`\``;

            expect(preprocessContentForAzureAISearch(input)).toBe(expected);
        });

        it('should handle multiple code blocks', () => {
            const input = `First block:
\`\`\`javascript
const x = 1;
\`\`\`

Second block:
\`\`\`python
# generated _client.py
class PetStoreClient(_PetStoreClientOperationsMixin):
    def __init__(self, endpoint: str, **kwargs: Any) -> None: ...

// generated operations/_operations.py
class _PetStoreClientOperationsMixin:

    @distributed_trace
    def info(self, **kwargs: Any) -> None:

class BillingsOperations:

    @distributed_trace
    def history(self, **kwargs: Any) -> None:

class ActionsOperations:

    @distributed_trace
    def open(self, **kwargs: Any) -> None:

    @distributed_trace
    def close(self, **kwargs: Any) -> None:

# generated pets/operations/_operations.py
class PetsOperations:

    @distributed_trace
    def info(self, **kwargs: Any) -> None:

class PetsActionsOperations:

    @distributed_trace
    def open(self, **kwargs: Any) -> None:

    @distributed_trace
    def close(self, **kwargs: Any) -> None:

#usage sample
from pet_store import PetStoreClient

client = PetStoreClient()
client.info()
client.billings.history()
client.pets.info()
client.pets.actions.feed()
client.pets.actions.pet()
client.actions.open()
client.actions.close()
\`\`\``;

            const expected = String.raw`First block:
\`\`\`javascript
const x = 1;
\`\`\`

Second block:
\`\`\`python
// generated _client.py
class PetStoreClient(_PetStoreClientOperationsMixin):
    def __init__(self, endpoint: str, **kwargs: Any) -> None: ...

// generated operations/_operations.py
class _PetStoreClientOperationsMixin:

    @distributed_trace
    def info(self, **kwargs: Any) -> None:

class BillingsOperations:

    @distributed_trace
    def history(self, **kwargs: Any) -> None:

class ActionsOperations:

    @distributed_trace
    def open(self, **kwargs: Any) -> None:

    @distributed_trace
    def close(self, **kwargs: Any) -> None:

// generated pets/operations/_operations.py
class PetsOperations:

    @distributed_trace
    def info(self, **kwargs: Any) -> None:

class PetsActionsOperations:

    @distributed_trace
    def open(self, **kwargs: Any) -> None:

    @distributed_trace
    def close(self, **kwargs: Any) -> None:

// usage sample
from pet_store import PetStoreClient

client = PetStoreClient()
client.info()
client.billings.history()
client.pets.info()
client.pets.actions.feed()
client.pets.actions.pet()
client.actions.open()
client.actions.close()
\`\`\``;

            expect(preprocessContentForAzureAISearch(input)).toBe(expected);
        });

        it('should preserve code content exactly', () => {
            const input = `\`\`\`typescript
function test() {
    if (true) {
        console.log("nested");
    }
}
\`\`\``;

            const expected = String.raw`\`\`\`typescript
function test() {
    if (true) {
        console.log("nested");
    }
}
\`\`\``;

            expect(preprocessContentForAzureAISearch(input)).toBe(expected);
        });

        it('should handle backticks without language identifier', () => {
            const input = `\`\`\`
plain code
\`\`\``;

            const expected = String.raw`\`\`\`
plain code
\`\`\``;

            expect(preprocessContentForAzureAISearch(input)).toBe(expected);
        });
    });

    describe('Comment conversion inside code blocks', () => {
        it('should convert # comments to // inside code blocks', () => {
            const input = `\`\`\`python
# This is a comment
def hello():
    pass
\`\`\``;

            const expected = String.raw`\`\`\`python
// This is a comment
def hello():
    pass
\`\`\``;

            expect(preprocessContentForAzureAISearch(input)).toBe(expected);
        });

        it('should convert multiple # comments inside code blocks', () => {
            const input = `\`\`\`python
# Comment 1
# Comment 2
code here
\`\`\``;

            const expected = String.raw`\`\`\`python
// Comment 1
// Comment 2
code here
\`\`\``;

            expect(preprocessContentForAzureAISearch(input)).toBe(expected);
        });

        it('should preserve # in middle of lines inside code blocks', () => {
            const input = `\`\`\`python
# Start comment
text = "value" # inline comment
\`\`\``;

            const expected = String.raw`\`\`\`python
// Start comment
text = "value" # inline comment
\`\`\``;

            expect(preprocessContentForAzureAISearch(input)).toBe(expected);
        });

        it('should not convert # outside code blocks', () => {
            const input = `# generated by dataclasses
# Header 1
## Header 2`;

            const expected = String.raw`# generated by dataclasses
# Header 1
## Header 2`;

            expect(preprocessContentForAzureAISearch(input)).toBe(expected);
        });
    });

    describe('Combined transformations', () => {
        it('should convert code blocks and preserve markdown headers', () => {
            const input = `# TypeSpec Documentation

\`\`\`python
def example():
    pass
\`\`\`

# Another Section`;

            const expected = String.raw`# TypeSpec Documentation

\`\`\`python
def example():
    pass
\`\`\`

# Another Section`;

            expect(preprocessContentForAzureAISearch(input)).toBe(expected);
        });

        it('should handle # comments inside code block', () => {
            const input = `\`\`\`
# generated by dataclasses
class MyClass:
    pass
\`\`\``;

            const expected = String.raw`\`\`\`
// generated by dataclasses
class MyClass:
    pass
\`\`\``;

            expect(preprocessContentForAzureAISearch(input)).toBe(expected);
        });

        it('should handle complex real-world example', () => {
            const input = `# TypeSpec Documentation

# generated from source

Here's an example:

\`\`\`typescript
interface User {
    name: string;
    age: number;
}
\`\`\`

And another:

\`\`\`python
# generated by tool
# Another comment
class User:
    def __init__(self):
        pass
\`\`\``;

            const expected = String.raw`# TypeSpec Documentation

# generated from source

Here's an example:

\`\`\`typescript
interface User {
    name: string;
    age: number;
}
\`\`\`

And another:

\`\`\`python
// generated by tool
// Another comment
class User:
    def __init__(self):
        pass
\`\`\``;

            expect(preprocessContentForAzureAISearch(input)).toBe(expected);
        });
    });

    describe('Edge cases', () => {
        it('should handle empty string', () => {
            expect(preprocessContentForAzureAISearch('')).toBe('');
        });

        it('should handle content with no transformations needed', () => {
            const input = `Regular markdown content
With multiple lines
And **bold** text`;

            expect(preprocessContentForAzureAISearch(input)).toBe(input);
        });

        it('should handle inline backticks (not code blocks)', () => {
            const input = `Use \`inline code\` like this`;

            expect(preprocessContentForAzureAISearch(input)).toBe(input);
        });
    });
});
