import express, { Request, Response, NextFunction } from 'express';
import { applyDotEnv } from './utils/env.js';
import promptsRouter from './routes/prompts.js'; // Import the prompts router

applyDotEnv();

const app = express();
const port = process.env.PORT || 3000;
app.use(express.json());

// Mount the prompts router
app.use('/api/prompts', promptsRouter);

app.listen(port, () => {
  console.log(`Server listening on port ${port}`);
});
