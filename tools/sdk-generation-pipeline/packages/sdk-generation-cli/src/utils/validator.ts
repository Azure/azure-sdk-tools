export const assertNullOrEmpty = (value: string) => {
    if (!value || value.length === 0) {
        throw new Error(' must not be null or empty');
    }
};
