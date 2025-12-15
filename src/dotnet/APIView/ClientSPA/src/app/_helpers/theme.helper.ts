/**
 * Helper class for theme-related utilities
 */
export class ThemeHelper {
  /**
   * Maps application theme to highlight.js theme
   */
  static getHighlightTheme(appTheme: string): string {
    switch (appTheme) {
      case 'dark-theme':
        return 'atom-one-dark';
      case 'light-theme':
        return 'atom-one-light';
      case 'dark-solarized-theme':
        return 'monokai';
      default:
        return 'atom-one-light';
    }
  }
}
