import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

/**
 * Service to hold the current review context (language, reviewId, approvers, etc.)
 * This avoids prop drilling these values through multiple component layers.
 * 
 * Components that need review context can inject this service directly
 * instead of receiving it via @Input() from parent components.
 */
@Injectable({
    providedIn: 'root'
})
export class ReviewContextService {
    private language$ = new BehaviorSubject<string | undefined>(undefined);
    private reviewId$ = new BehaviorSubject<string | undefined>(undefined);
    private languageApprovers$ = new BehaviorSubject<string[]>([]);

    /**
     * Set the current review language.
     * Called by review-page when the review loads.
     */
    setLanguage(language: string | undefined): void {
        this.language$.next(language);
    }

    /**
     * Get the current review language synchronously.
     * Use this when you need the value immediately (e.g., in permission checks).
     */
    getLanguage(): string | undefined {
        return this.language$.value;
    }

    /**
     * Get the language as an observable for reactive updates.
     */
    getLanguage$(): Observable<string | undefined> {
        return this.language$.asObservable();
    }

    /**
     * Set the current review ID.
     * Called by review-page when the review loads.
     */
    setReviewId(reviewId: string | undefined): void {
        this.reviewId$.next(reviewId);
    }

    /**
     * Get the current review ID synchronously.
     */
    getReviewId(): string | undefined {
        return this.reviewId$.value;
    }

    /**
     * Get the review ID as an observable for reactive updates.
     */
    getReviewId$(): Observable<string | undefined> {
        return this.reviewId$.asObservable();
    }

    /**
     * Set the list of approvers for the current review's language.
     * Called by review-page after loading approvers.
     */
    setLanguageApprovers(approvers: string[]): void {
        this.languageApprovers$.next(approvers);
    }

    /**
     * Get the list of approvers for the current review's language synchronously.
     */
    getLanguageApprovers(): string[] {
        return this.languageApprovers$.value;
    }

    /**
     * Get the language approvers as an observable for reactive updates.
     */
    getLanguageApprovers$(): Observable<string[]> {
        return this.languageApprovers$.asObservable();
    }

    /**
     * Clear all context (e.g., when navigating away from review page).
     */
    clear(): void {
        this.language$.next(undefined);
        this.reviewId$.next(undefined);
        this.languageApprovers$.next([]);
    }
}
