import { configDefaults, defineConfig } from "vitest/config";

export default defineConfig({
    test: {
        coverage: {
            exclude: [
                ...(configDefaults.coverage.exclude ?? []),

                // Config files (not in defaults)
                "eslint*.config.js",
            ],
            include: ["src/**/*.ts"],
        },
    },
});
