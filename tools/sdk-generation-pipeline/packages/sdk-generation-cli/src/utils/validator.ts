export const assertNullOrEmpty = (value: string) => {
    if (!value || value.length === 0) {
        throw new Error(' Value should not be null or empty');
    }
};
