export const triggerTypeSchema = {
  // How this generation is triggered.
  $id: 'TriggerType',
  type: 'string',
  enum: ['pullRequest', 'continuousIntegration']
};

export type TriggerType = 'pullRequest' | 'continuousIntegration';
