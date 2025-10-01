import { CommentItemModel } from '../_models/commentItemModel';
import { CodePanelRowData } from '../_models/codePanelModels';

export class CommentRelationHelper {

  static calculateRelatedComments(allComments: CommentItemModel[]): void {
    if (!allComments || allComments.length === 0) return;
    
    allComments.forEach(comment => {
      comment.hasRelatedComments = false;
      comment.relatedCommentsCount = 0;
    });
    
    const commentsByCorrelationId = allComments
      .filter(comment => comment.correlationId && !comment.isDeleted && !comment.isResolved)
      .reduce((map, comment) => {
        const key = comment.correlationId!;
        if (!map.has(key)) {
          map.set(key, []);
        }
        map.get(key)!.push(comment);
        return map;
      }, new Map<string, CommentItemModel[]>());

    commentsByCorrelationId.forEach((comments,) => {
      if (comments.length > 1) { 
        comments.forEach(comment => {
          comment.hasRelatedComments = true;
          comment.relatedCommentsCount = comments.length - 1; 
        });
      }
    });
  }
  
  static getRelatedComments(comment: CommentItemModel, allComments: CommentItemModel[]): CommentItemModel[] {
    if (!comment.correlationId || !allComments) {
      return [];
    }
    
    return allComments.filter(c => 
      c.correlationId === comment.correlationId && 
      !c.isDeleted &&
      !c.isResolved
    );
  }

  static getVisibleRelatedComments(comment: CommentItemModel, allComments: CommentItemModel[], codePanelRowData: CodePanelRowData[]): CommentItemModel[] {
    const relatedComments = this.getRelatedComments(comment, allComments);
    
    const visibleNodeIds = new Set<string>();
    codePanelRowData.forEach(row => {
      if (row.nodeId) {
        visibleNodeIds.add(row.nodeId);
      }
    });
    
    return relatedComments.filter(c => visibleNodeIds.has(c.elementId));
  }

  static getRelatedCommentsCount(comment: CommentItemModel, allComments: CommentItemModel[], codePanelRowData?: CodePanelRowData[]): number {
    if (!comment.correlationId || !allComments) {
      return 0;
    }

    if (!codePanelRowData || codePanelRowData.length === 0) {
      return comment.relatedCommentsCount ?? 
             allComments.find(c => c.id === comment.id)?.relatedCommentsCount ?? 
             0;
    }

    const visibleRelatedComments = this.getVisibleRelatedComments(comment, allComments, codePanelRowData);
    return Math.max(0, visibleRelatedComments.length - 1);
  }

  static hasRelatedComments(comment: CommentItemModel, allComments: CommentItemModel[], codePanelRowData?: CodePanelRowData[]): boolean {
    return this.getRelatedCommentsCount(comment, allComments, codePanelRowData) > 0;
  }
}