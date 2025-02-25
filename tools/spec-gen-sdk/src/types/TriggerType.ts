export const triggerTypeSchema = {
  // How this generation is triggered.
  $id: 'TriggerType',
  type: 'string',
  enum: ['pullRequest', 'continuousIntegration', 'manual']
};

export type TriggerType = 'pullRequest' | 'continuousIntegration' | 'manual';
