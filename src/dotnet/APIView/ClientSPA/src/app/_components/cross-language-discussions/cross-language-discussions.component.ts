import { AfterViewInit, ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output, SimpleChanges, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TimeagoModule } from 'ngx-timeago';
import { CodePanelData, CodePanelRowData, CodePanelRowDatatype, CrossLanguageContentDto } from 'src/app/_models/codePanelModels';
import { CommentItemModel, CommentType } from 'src/app/_models/commentItemModel';
import { Review } from 'src/app/_models/review';
import { UserProfile } from 'src/app/_models/userProfile';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { CommentThreadUpdateAction, CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { Subject, take, takeUntil } from 'rxjs';
import { StructuredToken } from 'src/app/_models/structuredToken';
import { getStructuredTokenClass } from 'src/app/_helpers/common-helpers';
import { LanguageNamesPipe } from 'src/app/_pipes/language-names.pipe';
import { TooltipModule } from 'primeng/tooltip';
import { environment } from 'src/environments/environment';

export interface CrossLanguageCodeEntry {
  language: string;
  codeLines: CodePanelRowData[];
  packageName: string;
  packageVersion: string;
  isCurrent: boolean;
}

export interface DiscussionSummary {
  crossLanguageId: string;
  threadId: string;
  displayName: string;
  commentPreview: string;
  breadcrumb: string;
  codeEntries: CrossLanguageCodeEntry[];
  allComments: CommentItemModel[];
  isResolved: boolean;
  languages: Set<string>;
  silentLanguages: string[];
  lastActivity: Date;
  requiresAttention: boolean;
}

@Component({
  selector: 'app-cross-language-discussions',
  templateUrl: './cross-language-discussions.component.html',
  styleUrls: ['./cross-language-discussions.component.scss'],
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TimeagoModule,
    TooltipModule,
    LanguageNamesPipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CrossLanguageDiscussionsComponent implements OnInit, OnChanges, OnDestroy, AfterViewInit {
  @Input() comments: CommentItemModel[] = [];
  @Input() review: Review | undefined = undefined;
  @Input() userProfile: UserProfile | undefined;
  @Input() activeApiRevisionId: string | null = null;
  @Input() codePanelData: CodePanelData | null = null;
  @Input() crossLanguageRowData: CrossLanguageContentDto[] = [];
  @Input() language: string | undefined;
  @Input() crossLanguagePackageId: string | undefined;
  @Input() jumpToDiscussionId: string | null = null;

  @Output() numberOfActiveDiscussionsEmitter: EventEmitter<number> = new EventEmitter<number>();
  @Output() commentCreatedEmitter: EventEmitter<CommentItemModel> = new EventEmitter<CommentItemModel>();

  assetsPath: string = environment.assetsPath;
  CodePanelRowDatatype = CodePanelRowDatatype;

  // View state
  selectedDiscussion: DiscussionSummary | null = null;

  @ViewChild('conversationContainer') conversationContainerRef: ElementRef<HTMLElement> | undefined;

  // Dashboard data
  discussions: DiscussionSummary[] = [];
  attentionDiscussions: DiscussionSummary[] = [];
  numberOfActiveDiscussions: number = 0;
  isLoading: boolean = true;

  // Dashboard filters
  filterStatus: 'all' | 'active' | 'resolved' = 'active';
  searchText: string = '';
  filteredDiscussions: DiscussionSummary[] = [];

  // Detail view
  activeTabIndex: number = 0;
  replyText: string = '';

  private destroy$ = new Subject<void>();
  private elementIdToCrossLangId = new Map<string, string>();
  private crossReviewComments: CommentItemModel[] = [];
  private userRoleMap = new Map<string, string>();
  private userLangMap = new Map<string, string>(); // username → extracted language

  constructor(
    private commentsService: CommentsService,
    private signalRService: SignalRService,
    private changeDetectorRef: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.handleRealTimeCommentUpdates();
  }

  ngAfterViewInit() {
    this.scrollConversationToBottom();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['codePanelData'] && this.codePanelData) {
      this.buildElementIdToCrossLangMap();
    }
    if (changes['crossLanguagePackageId'] && this.crossLanguagePackageId) {
      this.loadCrossReviewComments();
    }
    if (changes['comments'] || changes['codePanelData'] || changes['crossLanguageRowData']) {
      this.buildDiscussions();
    }
    if (changes['jumpToDiscussionId'] && this.jumpToDiscussionId) {
      const target = this.discussions.find(d => d.crossLanguageId === this.jumpToDiscussionId);
      if (target) {
        this.selectDiscussion(target);
      }
    }
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Cross-review comment loading ───────────────────────────────────

  private loadCrossReviewComments() {
    if (!this.crossLanguagePackageId) return;
    this.commentsService.getCrossLanguageCommentsByPackageId(this.crossLanguagePackageId)
      .pipe(take(1))
      .subscribe({
        next: (comments) => {
          this.crossReviewComments = comments;
          console.log('[Discussions] Loaded', comments.length, 'cross-review comments for package:', this.crossLanguagePackageId);
          this.buildDiscussions();
        }
      });
  }

  // ── Selection ────────────────────────────────────────────────────

  selectDiscussion(discussion: DiscussionSummary) {
    this.selectedDiscussion = discussion;
    this.activeTabIndex = discussion.codeEntries.findIndex(e => e.isCurrent);
    if (this.activeTabIndex < 0) this.activeTabIndex = 0;
    this.replyText = '';
    this.changeDetectorRef.markForCheck();
    // Scroll conversation to bottom after render
    setTimeout(() => this.scrollConversationToBottom(), 0);
  }

  private scrollConversationToBottom() {
    const el = this.conversationContainerRef?.nativeElement;
    if (el) el.scrollTop = el.scrollHeight;
  }

  // ── Data building ──────────────────────────────────────────────────

  private buildElementIdToCrossLangMap() {
    this.elementIdToCrossLangId.clear();
    if (!this.codePanelData?.nodeMetaData) return;

    let count = 0;
    for (const nodeIdHashed of Object.keys(this.codePanelData.nodeMetaData)) {
      const node = this.codePanelData.nodeMetaData[nodeIdHashed];
      if (node.codeLines) {
        for (const codeLine of node.codeLines) {
          if (codeLine.crossLanguageId && codeLine.nodeId) {
            this.elementIdToCrossLangId.set(codeLine.nodeId, codeLine.crossLanguageId);
            count++;
          }
        }
      }
    }
    console.log('[Discussions] Built elementIdToCrossLangId map with', count, 'entries from', Object.keys(this.codePanelData.nodeMetaData).length, 'nodes');
  }

  private resolveCrossLanguageId(comment: CommentItemModel): string | undefined {
    if (comment.crossLanguageId) return comment.crossLanguageId;
    return this.elementIdToCrossLangId.get(comment.elementId);
  }

  private buildDiscussions() {
    // Merge local review comments with cross-review comments, deduplicating by ID
    const commentMap = new Map<string, CommentItemModel>();
    for (const c of this.comments) {
      commentMap.set(c.id, c);
    }
    for (const c of this.crossReviewComments) {
      if (!commentMap.has(c.id)) {
        commentMap.set(c.id, c);
      }
    }
    const allComments = Array.from(commentMap.values());

    // Keep username→role map up to date from every known comment
    for (const c of allComments) {
      if (c.createdBy && c.roleOfCreator) {
        this.userRoleMap.set(c.createdBy, c.roleOfCreator);
        const lang = this.extractLanguageFromRole(c.roleOfCreator);
        if (lang) this.userLangMap.set(c.createdBy, lang);
      }
    }

    console.log('[Discussions] buildDiscussions called. local comments:', this.comments.length,
      'cross-review comments:', this.crossReviewComments.length,
      'merged (deduplicated):', allComments.length,
      'elementIdToCrossLangId map size:', this.elementIdToCrossLangId.size,
      'codePanelData:', !!this.codePanelData);

    const crossLangComments: { comment: CommentItemModel; resolvedCrossLangId: string }[] = [];
    for (const c of allComments) {
      if (c.isDeleted) continue;
      const resolvedId = this.resolveCrossLanguageId(c);
      if (resolvedId) {
        crossLangComments.push({ comment: c, resolvedCrossLangId: resolvedId });
      }
    }

    console.log('[Discussions] Found', crossLangComments.length, 'cross-language comments out of', allComments.length, 'total');
    if (crossLangComments.length === 0 && allComments.length > 0) {
      // Log a sample comment to debug why none matched
      const sample = allComments.find(c => !c.isDeleted);
      if (sample) {
        console.log('[Discussions] Sample comment:', {
          id: sample.id,
          elementId: sample.elementId,
          crossLanguageId: sample.crossLanguageId,
          crossLanguagePackageId: sample.crossLanguagePackageId,
          mapHas: this.elementIdToCrossLangId.has(sample.elementId)
        });
      }
    }

    if (crossLangComments.length === 0) {
      this.discussions = [];
      this.attentionDiscussions = [];
      this.filteredDiscussions = [];
      this.numberOfActiveDiscussions = 0;
      this.numberOfActiveDiscussionsEmitter.emit(0);
      this.isLoading = false;
      this.changeDetectorRef.markForCheck();
      return;
    }

    // Group by threadId — each thread is a separate discussion
    const threadMap = new Map<string, { crossLangId: string; comments: CommentItemModel[] }>();
    for (const { comment, resolvedCrossLangId } of crossLangComments) {
      const threadKey = comment.threadId || comment.elementId;
      if (!threadMap.has(threadKey)) {
        threadMap.set(threadKey, { crossLangId: resolvedCrossLangId, comments: [] });
      }
      threadMap.get(threadKey)!.comments.push(comment);
    }

    this.discussions = [];
    this.numberOfActiveDiscussions = 0;
    const currentUser = this.userProfile?.userName;

    for (const [threadId, { crossLangId, comments }] of threadMap) {
      // Sort comments chronologically within the thread
      comments.sort((a, b) => new Date(a.createdOn).getTime() - new Date(b.createdOn).getTime());

      const languages = new Set<string>();
      for (const c of comments) {
        if (c.crossLanguagePackageId) languages.add(c.crossLanguagePackageId);
      }

      const isResolved = comments.some(c => c.isResolved);
      if (!isResolved) this.numberOfActiveDiscussions++;

      const lastComment = comments[comments.length - 1];
      const lastActivity = new Date(lastComment.createdOn);

      const requiresAttention = this.checkRequiresAttention(comments, currentUser);

      const segments = crossLangId.split('.');
      const rawName = segments[segments.length - 1] || crossLangId;
      const displayName = rawName.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/([A-Z]+)([A-Z][a-z])/g, '$1 $2');
      const breadcrumb = segments.length > 1 ? segments.slice(0, -1).join('.') : '';

      // Build a preview from the first comment text (strip HTML, truncate)
      const firstText = comments[0]?.commentText || '';
      const stripped = firstText.replace(/<[^>]*>/g, '').trim();
      const commentPreview = stripped.length > 80 ? stripped.substring(0, 80) + '…' : stripped;

      const codeEntries = this.resolveCodeEntries(crossLangId, comments[0]?.elementId);

      // A language "participated" if any commenter's role maps to it, or if an upvoter's role maps to it.
      // roleOfCreator is e.g. "Java Architect" — match against codeEntry language names.
      const participatingLanguages = new Set<string>();
      const roleToLanguage = (role: string): string | undefined => {
        if (!role) return undefined;
        const roleLower = role.toLowerCase();
        return codeEntries.find(e => roleLower.includes(e.language.toLowerCase()))?.language;
      };

      for (const c of comments) {
        // Commenter's own role counts as participation
        const commenterLang = roleToLanguage(c.roleOfCreator);
        if (commenterLang) participatingLanguages.add(commenterLang);

        // Upvoters' roles also count
        for (const voter of (c.upvotes ?? [])) {
          const voterRole = this.userRoleMap.get(voter);
          const voterLang = roleToLanguage(voterRole ?? '');
          if (voterLang) participatingLanguages.add(voterLang);
        }
      }

      // A language is "silent" if it has a code entry but hasn't commented or upvoted.
      // TypeSpec is excluded — it's a source spec, not an implementing language.
      const silentLanguages = codeEntries
        .filter(e => !e.isCurrent && !participatingLanguages.has(e.language) && e.language.toLowerCase() !== 'typespec')
        .map(e => e.language);

      this.discussions.push({
        crossLanguageId: crossLangId,
        threadId,
        displayName,
        commentPreview,
        breadcrumb,
        codeEntries,
        allComments: comments,
        isResolved,
        languages,
        silentLanguages,
        lastActivity,
        requiresAttention
      });
    }

    // Sort by last activity (most recent first)
    this.discussions.sort((a, b) => b.lastActivity.getTime() - a.lastActivity.getTime());
    this.attentionDiscussions = this.discussions.filter(d => d.requiresAttention && !d.isResolved);
    this.numberOfActiveDiscussionsEmitter.emit(this.numberOfActiveDiscussions);

    // If detail view is open, keep it updated; otherwise auto-select first discussion
    if (this.selectedDiscussion) {
      const updated = this.discussions.find(d => d.threadId === this.selectedDiscussion!.threadId);
      if (updated) {
        this.selectedDiscussion = updated;
      }
    } else if (this.discussions.length > 0) {
      this.selectDiscussion(this.discussions[0]);
    }

    this.applyFilters();
    this.isLoading = false;
    this.changeDetectorRef.markForCheck();
  }

  private checkRequiresAttention(comments: CommentItemModel[], currentUser: string | undefined): boolean {
    if (!currentUser) return false;

    for (const c of comments) {
      // taggedUsers may be a Set or a plain array depending on deserialization
      const tagged: any = c.taggedUsers;
      if (tagged && (typeof tagged.has === 'function' ? tagged.has(currentUser) : Array.isArray(tagged) && tagged.includes(currentUser))) return true;
    }

    // Someone replied after user's last comment
    let userLastCommentIdx = -1;
    for (let i = comments.length - 1; i >= 0; i--) {
      if (comments[i].createdBy === currentUser) {
        userLastCommentIdx = i;
        break;
      }
    }
    return userLastCommentIdx >= 0 && userLastCommentIdx < comments.length - 1;
  }

  // ── Filters ────────────────────────────────────────────────────────

  setStatusFilter(status: 'all' | 'active' | 'resolved') {
    this.filterStatus = status;
    this.applyFilters();
  }

  applyFilters() {
    let result = this.discussions;

    if (this.filterStatus === 'active') {
      result = result.filter(d => !d.isResolved);
    } else if (this.filterStatus === 'resolved') {
      result = result.filter(d => d.isResolved);
    }

    if (this.searchText.trim()) {
      const search = this.searchText.toLowerCase();
      result = result.filter(d =>
        d.crossLanguageId.toLowerCase().includes(search) ||
        d.displayName.toLowerCase().includes(search) ||
        d.allComments.some(c => c.commentText.toLowerCase().includes(search))
      );
    }

    this.filteredDiscussions = result;
    this.changeDetectorRef.markForCheck();
  }

  // ── Code entries ───────────────────────────────────────────────────

  private resolveCodeEntries(crossLanguageId: string, elementId?: string): CrossLanguageCodeEntry[] {
    const entries: CrossLanguageCodeEntry[] = [];
    const clId = crossLanguageId.toLowerCase();

    if (this.crossLanguageRowData?.length) {
      for (const entry of this.crossLanguageRowData) {
        if (clId in entry.content) {
          const isCurrent = entry.language === this.language;
          entries.push({
            language: entry.language,
            codeLines: entry.content[clId] || [],
            packageName: entry.packageName,
            packageVersion: entry.packageVersion,
            isCurrent
          });
        }
      }
    }

    // If the current language wasn't found in crossLanguageRowData, fall back to codePanelData
    if (this.language && !entries.some(e => e.isCurrent) && this.codePanelData?.nodeMetaData) {
      const currentCodeLines = elementId
        ? this.findCodeLineByElementId(elementId)
        : this.findCurrentLanguageCodeLines(crossLanguageId);
      if (currentCodeLines.length > 0) {
        entries.unshift({
          language: this.language,
          codeLines: currentCodeLines,
          packageName: '',
          packageVersion: '',
          isCurrent: true
        });
      }
    }

    // Order: current first, TypeSpec second, rest alphabetically
    entries.sort((a, b) => {
      if (a.isCurrent !== b.isCurrent) return a.isCurrent ? -1 : 1;
      const aTs = a.language.toLowerCase() === 'typespec';
      const bTs = b.language.toLowerCase() === 'typespec';
      if (aTs !== bTs) return aTs ? -1 : 1;
      return a.language.localeCompare(b.language);
    });

    console.log('[Discussions] resolveCodeEntries for', crossLanguageId,
      '→ found', entries.length, 'languages:',
      entries.map(e => e.language),
      '| crossLanguageRowData has', this.crossLanguageRowData?.length ?? 0, 'entries with languages:',
      this.crossLanguageRowData?.map(e => e.language),
      '| sample content keys from first entry:',
      this.crossLanguageRowData?.[0] ? Object.keys(this.crossLanguageRowData[0].content).slice(0, 3) : 'none'
    );

    return entries;
  }

  private findCurrentLanguageCodeLines(crossLanguageId: string): CodePanelRowData[] {
    if (!this.codePanelData?.nodeMetaData) return [];

    for (const nodeIdHashed of Object.keys(this.codePanelData.nodeMetaData)) {
      const node = this.codePanelData.nodeMetaData[nodeIdHashed];
      if (node.codeLines) {
        for (const codeLine of node.codeLines) {
          if (codeLine.crossLanguageId === crossLanguageId) {
            return [codeLine];
          }
        }
      }
    }
    return [];
  }

  private findCodeLineByElementId(elementId: string): CodePanelRowData[] {
    if (!this.codePanelData?.nodeMetaData) return [];

    for (const nodeIdHashed of Object.keys(this.codePanelData.nodeMetaData)) {
      const node = this.codePanelData.nodeMetaData[nodeIdHashed];
      if (node.codeLines) {
        for (const codeLine of node.codeLines) {
          if (codeLine.nodeId === elementId) {
            return [codeLine];
          }
        }
      }
    }
    return [];
  }

  // ── Helpers ────────────────────────────────────────────────────────

  getTokenClass(token: StructuredToken) {
    return getStructuredTokenClass(token);
  }

  // Extract the language portion from a role string like "Python Architect" → "Python".
  // Strips common suffix words; falls back to the full role if nothing matches.
  extractLanguageFromRole(role: string): string {
    if (!role) return '';
    const suffixes = ['architect', 'engineer', 'developer', 'lead', 'reviewer', 'owner', 'member', 'manager'];
    const parts = role.trim().split(/\s+/);
    const filtered = parts.filter(p => !suffixes.includes(p.toLowerCase()));
    return filtered.join(' ') || role;
  }

  getVoterRole(voter: string): string {
    const lang = this.userLangMap.get(voter);
    return lang ? `${voter} (${lang})` : voter;
  }

  getRoleLabel(role: string): string {
    return this.extractLanguageFromRole(role);
  }

  submitReply() {
    if (!this.replyText.trim() || !this.selectedDiscussion || !this.review?.id) return;
    const firstComment = this.selectedDiscussion.allComments[0];
    this.commentsService.createComment(
      this.review.id,
      firstComment.apiRevisionId || this.activeApiRevisionId || '',
      firstComment.elementId,
      this.replyText.trim(),
      CommentType.APIRevision,
      false,
      undefined,
      this.selectedDiscussion.threadId,
      this.selectedDiscussion.crossLanguageId,
      firstComment.crossLanguagePackageId
    ).pipe(take(1)).subscribe({
      next: (response: CommentItemModel) => {
        const updates: CommentUpdatesDto = {
          commentThreadUpdateAction: CommentThreadUpdateAction.CommentCreated,
          reviewId: this.review?.id!,
          elementId: response.elementId,
          comment: response,
          threadId: response.threadId,
          title: ''
        };
        this.replyText = '';
        this.commentCreatedEmitter.emit(response);
        this.signalRService.pushCommentUpdates(updates);
        this.changeDetectorRef.markForCheck();
      }
    });
  }

  isUpvotedByMe(comment: CommentItemModel): boolean {
    return !!this.userProfile?.userName && (comment.upvotes ?? []).includes(this.userProfile.userName);
  }

  toggleUpvote(comment: CommentItemModel) {
    if (!this.userProfile?.userName) return;
    const reviewId = comment.reviewId || this.review?.id;
    if (!reviewId) return;
    this.commentsService.toggleCommentUpVote(reviewId, comment.id).pipe(take(1)).subscribe({
      next: () => {
        const user = this.userProfile!.userName;
        const ups = comment.upvotes ?? [];
        const idx = ups.indexOf(user);
        if (idx >= 0) {
          comment.upvotes = ups.filter(u => u !== user);
        } else {
          comment.upvotes = [...ups, user];
        }
        this.changeDetectorRef.markForCheck();
      },
      error: (err) => {
        console.error('[Discussions] toggleUpvote failed:', err);
      }
    });
  }

  deleteComment(comment: CommentItemModel) {
    if (!this.review?.id) return;
    this.commentsService.deleteComment(this.review.id, comment.id).pipe(take(1)).subscribe({
      next: () => {
        const idx = this.comments.findIndex(c => c.id === comment.id);
        if (idx >= 0) this.comments[idx].isDeleted = true;
        this.buildDiscussions();
        const updates: CommentUpdatesDto = {
          commentThreadUpdateAction: CommentThreadUpdateAction.CommentDeleted,
          reviewId: this.review?.id!,
          commentId: comment.id,
          elementId: comment.elementId,
          threadId: comment.threadId,
          title: ''
        };
        this.signalRService.pushCommentUpdates(updates);
      }
    });
  }

  toggleResolveDiscussion() {
    if (!this.selectedDiscussion || !this.review?.id) return;
    const elementId = this.selectedDiscussion.allComments[0]?.elementId;
    const threadId = this.selectedDiscussion.threadId;
    const newIsResolved = !this.selectedDiscussion.isResolved;
    const action$ = this.selectedDiscussion.isResolved
      ? this.commentsService.unresolveComments(this.review.id, elementId, threadId)
      : this.commentsService.resolveComments(this.review.id, elementId, threadId);
    action$.pipe(take(1)).subscribe({
      next: () => {
        // Optimistically update local state so buildDiscussions() reflects the change
        for (const comment of this.selectedDiscussion!.allComments) {
          comment.isResolved = newIsResolved;
          const inLocal = this.comments.find(c => c.id === comment.id);
          if (inLocal) inLocal.isResolved = newIsResolved;
          const inCross = this.crossReviewComments.find(c => c.id === comment.id);
          if (inCross) inCross.isResolved = newIsResolved;
        }
        this.buildDiscussions();
      }
    });
  }

  // ── Comment actions ────────────────────────────────────────────────

  handleSaveCommentActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.review?.id!;
    if (commentUpdates.commentId) {
      // Update existing comment
    } else {
      const resolutionLocked = commentUpdates.allowAnyOneToResolve !== undefined ? !commentUpdates.allowAnyOneToResolve : false;
      this.commentsService.createComment(
        this.review?.id!,
        commentUpdates.revisionId!,
        commentUpdates.elementId!,
        commentUpdates.commentText!,
        CommentType.APIRevision,
        resolutionLocked,
        commentUpdates.severity,
        commentUpdates.threadId,
        commentUpdates.crossLanguageId,
        commentUpdates.crossLanguagePackageId,
        commentUpdates.isTodo ?? false
      ).pipe(take(1)).subscribe({
        next: (response: CommentItemModel) => {
          commentUpdates.comment = response;
          if (!commentUpdates.threadId && response.threadId) {
            commentUpdates.threadId = response.threadId;
          }
          if (!this.comments.some(c => c.id === response.id)) {
            this.comments.push(response);
            this.buildDiscussions();
          }
          this.signalRService.pushCommentUpdates(commentUpdates);
        }
      });
    }
  }

  handleDeleteCommentActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.review?.id!;
    this.commentsService.deleteComment(this.review?.id!, commentUpdates.commentId!).pipe(take(1)).subscribe({
      next: () => {
        const idx = this.comments.findIndex(c => c.id === commentUpdates.commentId);
        if (idx >= 0) {
          this.comments[idx].isDeleted = true;
          this.buildDiscussions();
        }
        this.signalRService.pushCommentUpdates(commentUpdates);
      }
    });
  }

  handleCommentUpvoteActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.review?.id!;
    this.commentsService.toggleCommentUpVote(this.review?.id!, commentUpdates.commentId!).pipe(take(1)).subscribe();
  }

  handleCommentDownvoteActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.review?.id!;
    this.commentsService.toggleCommentDownVote(this.review?.id!, commentUpdates.commentId!).pipe(take(1)).subscribe();
  }

  handleCommentResolutionActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.review?.id!;
    if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentResolved) {
      this.commentsService.resolveComments(this.review?.id!, commentUpdates.elementId!, commentUpdates.threadId).pipe(take(1)).subscribe({
        next: () => this.buildDiscussions()
      });
    }
    if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentUnResolved) {
      this.commentsService.unresolveComments(this.review?.id!, commentUpdates.elementId!, commentUpdates.threadId).pipe(take(1)).subscribe({
        next: () => this.buildDiscussions()
      });
    }
  }

  private handleRealTimeCommentUpdates() {
    this.signalRService.onCommentUpdates().pipe(takeUntil(this.destroy$)).subscribe({
      next: (commentUpdates: CommentUpdatesDto) => {
        const isForThisReview =
            (commentUpdates.reviewId && commentUpdates.reviewId === this.review?.id) ||
            (commentUpdates.comment && commentUpdates.comment.reviewId === this.review?.id);

        if (!isForThisReview) return;

        const hasCrossLang = commentUpdates.comment?.crossLanguageId ||
            (commentUpdates.comment?.elementId && this.elementIdToCrossLangId.has(commentUpdates.comment.elementId));

        if (hasCrossLang && commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentCreated && commentUpdates.comment) {
          if (!this.comments.some(c => c.id === commentUpdates.comment!.id)) {
            this.comments.push(commentUpdates.comment);
            this.buildDiscussions();
          }
        }
        this.changeDetectorRef.markForCheck();
      }
    });
  }

  trackByDiscussionId(index: number, discussion: DiscussionSummary): string {
    return discussion.threadId;
  }

  trackByCommentId(index: number, comment: CommentItemModel): string {
    return comment.id;
  }
}
