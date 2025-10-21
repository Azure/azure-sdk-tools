import { Request, Response, NextFunction } from "express";
import { timingSafeEqual } from "crypto";

// API Key Authentication Middleware
export function authApiKey(req: Request, res: Response, next: NextFunction) {
    const apiKey = req.headers["x-api-key"] as string;
    const expectedKey = process.env.API_KEY;
    
    if (!apiKey || !expectedKey) {
        res.status(403).json({ message: "Unauthorized: Invalid API Key" });
        return;
    }
    
    // Use timing-safe comparison to prevent timing attacks
    try {
        const apiKeyBuffer = Buffer.from(apiKey);
        const expectedKeyBuffer = Buffer.from(expectedKey);
        
        // Ensure both buffers are the same length before comparison
        if (apiKeyBuffer.length !== expectedKeyBuffer.length) {
            res.status(403).json({ message: "Unauthorized: Invalid API Key" });
            return;
        }
        
        if (timingSafeEqual(apiKeyBuffer, expectedKeyBuffer)) {
            next();
        } else {
            res.status(403).json({ message: "Unauthorized: Invalid API Key" });
        }
    } catch (error) {
        res.status(403).json({ message: "Unauthorized: Invalid API Key" });
    }
}
