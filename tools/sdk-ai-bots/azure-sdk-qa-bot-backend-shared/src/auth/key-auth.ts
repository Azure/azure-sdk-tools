import { Request, Response, NextFunction } from "express";

// API Key Authentication Middleware
export function authApiKey(req: Request, res: Response, next: NextFunction) {
    const apiKey = req.headers["x-api-key"] as string;
    if (apiKey && apiKey === process.env.API_KEY) {
        next();
    } else {
        res.status(403).json({ message: "Unauthorized: Invalid API Key" });
    }
}
