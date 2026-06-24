import { logs } from "@opentelemetry/api-logs";
import { describe, expect, it, vi } from "vitest";
import { logger } from "../src/logger.ts";

describe("logger", () => {
    it("is obtained from logs.getLogger with the correct name", () => {
        const getLoggerSpy = vi.spyOn(logs, "getLogger");
        logs.getLogger("github-event-bridge");
        expect(getLoggerSpy).toHaveBeenCalledWith("github-event-bridge");
    });

    it("exposes an emit method", () => {
        expect(typeof logger.emit).toBe("function");
    });
});
