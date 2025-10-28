import { DBSchema } from 'idb';

export enum NotificationsFilter {
  All,
  Page,
}

export class SiteNotification {
  constructor(
    reviewId = '',
    activeAPIrevisionId = '',
    title = '',
    message = '',
    level: 'success' | 'info' | 'warning' | 'error' = 'info',
    createdOn = new Date()
  ) {
    this.reviewId = reviewId;
    this.activeAPIrevisionId = activeAPIrevisionId;
    this.title = title;
    this.message = message;
    this.level = level;
    this.createdOn = createdOn;
    this.id = `${reviewId}-${activeAPIrevisionId}-${this.title}-${this.level}-${this.message}`;
  }
  id : string;
  reviewId : string;
  activeAPIrevisionId : string;
  title : string;
  level : 'success' | 'info' | 'warning' | 'error';
  message : string;
  createdOn : Date;
}

export interface NotificationsDb extends DBSchema {
  notifications: {
    key: string;
    value: SiteNotification;
  };
}
