import { CommentSeverity } from '../_models/commentItemModel';

export class CommentSeverityHelper {
  static readonly severityOptions = [
    { label: 'Question', value: CommentSeverity.Question },
    { label: 'Suggestion', value: CommentSeverity.Suggestion },
    { label: 'Should Fix', value: CommentSeverity.ShouldFix },
    { label: 'Must Fix', value: CommentSeverity.MustFix }
  ];

  static normalizeSeverity(severity: CommentSeverity | string | null | undefined): string | null {
    if (severity === null || severity === undefined) return null;
    return typeof severity === 'string' ? severity.toLowerCase() : CommentSeverity[severity]?.toLowerCase() || null;
  }

  static getSeverityEnumValue(severity: CommentSeverity | string | null | undefined): CommentSeverity | null {
    if (severity === null || severity === undefined) return null;
    if (typeof severity === 'number') return severity; 
    
    const normalized = CommentSeverityHelper.normalizeSeverity(severity);
    switch (normalized) {
      case 'question': return CommentSeverity.Question;
      case 'suggestion': return CommentSeverity.Suggestion;
      case 'shouldfix': return CommentSeverity.ShouldFix;
      case 'mustfix': return CommentSeverity.MustFix;
      default: return null;
    }
  }

  static getSeverityLabel(severity: CommentSeverity | string | null | undefined): string {
    const normalized = CommentSeverityHelper.normalizeSeverity(severity);
    switch (normalized) {
      case 'question': return 'Question';
      case 'suggestion': return 'Suggestion';
      case 'shouldfix': return 'Should fix';
      case 'mustfix': return 'Must fix';
      default: return '';
    }
  }

  static getSeverityBadgeClass(severity: CommentSeverity | string | null | undefined): string {
    const normalized = CommentSeverityHelper.normalizeSeverity(severity);
    switch (normalized) {
      case 'question': return 'severity-question';
      case 'suggestion': return 'severity-suggestion';
      case 'shouldfix': return 'severity-should-fix';
      case 'mustfix': return 'severity-must-fix';
      default: return '';
    }
  }
}
