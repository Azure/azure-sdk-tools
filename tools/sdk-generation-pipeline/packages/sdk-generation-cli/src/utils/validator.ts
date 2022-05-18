export const assertNullOrEmpty = (value: string) => {
    console.log(`get here`);
    if (!value || value.length === 0) {
        throw new Error(' must not be null or empty');
    }
};
