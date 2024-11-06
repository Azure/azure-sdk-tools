import { requireJsonc } from '../utils/requireJsonc';
import { getTypeTransformer } from './validator';

export const initOutputSchema = requireJsonc(__dirname + '/InitOutputSchema.json');

export type InitOutput = {
  envs?: { [key: string]: string };
};

export const getInitOutput = getTypeTransformer<InitOutput>(initOutputSchema, 'InitOutput');
