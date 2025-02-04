
// Function to check if a lifetime should be elided
export function shouldElideLifetime(lifetime: string): boolean {
    return lifetime.startsWith("'life") || /^\d+$/.test(lifetime);
}
