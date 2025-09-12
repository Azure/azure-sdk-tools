import express, { Request, Response } from 'express';
import { authApiKey } from '../auth/key-auth.js';
import { LinkContentExtractor } from '../input/LinkContentExtractor.js';
import { PromptGenerator } from '../input/PromptGenerator.js';
import { ImageContentExtractor } from '../input/ImageContentExtractor.js';
import { applyDotEnv } from '../utils/env.js';

interface PreprocessRequestBody {
  text: string;
  images?: string[];
}

interface PreprocessWarning {
  id: string;
  warning: string;
}

interface PreprocessResult {
  text: string;
  warnings?: PreprocessWarning[];
}

// TODO: remove dupliocate applyDotEnv
applyDotEnv();

const urlRegex = /https?:\/\/[^\s"'<>]+/g;
const linkContentExtractor = new LinkContentExtractor();
const promptGenerator = new PromptGenerator();
const imageContentExtractor = new ImageContentExtractor();

const router = express.Router();

// TODO: add logging
router.post('/preprocess', authApiKey, async (req: Request, res: Response) => {
  const body = req.body as PreprocessRequestBody;
  const inlineLinkUrls = body.text ? body.text.match(urlRegex)?.map((link) => new URL(link)) || [] : [];
  const linkContents = await linkContentExtractor.extract(inlineLinkUrls);
  const imageUrls = body.images?.map((url) => new URL(url)) || [];
  const imageContents = await imageContentExtractor.extract(imageUrls);
  const prompt = promptGenerator.generate(undefined, body.text, imageContents, linkContents);
  const warnings: PreprocessWarning[] = [
    ...linkContents.filter((c) => c.error).map((c) => ({ id: c.id, warning: c.error!.message })),
    ...imageContents.filter((c) => c.error).map((c) => ({ id: c.id, warning: c.error!.message })),
  ];
  const result: PreprocessResult = {
    text: prompt,
    warnings,
  };
  res.json(result);
});

export default router;
