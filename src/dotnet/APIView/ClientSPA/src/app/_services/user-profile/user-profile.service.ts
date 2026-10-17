import { Injectable } from '@angular/core';
import { Observable, shareReplay, tap } from 'rxjs';
import { ConfigService } from '../config/config.service';
import { HttpClient, HttpParams } from '@angular/common/http';
import { UserProfile } from 'src/app/_models/userProfile';
import { UserPreferenceModel } from 'src/app/_models/userPreferenceModel';

@Injectable({
  providedIn: 'root'
})
export class UserProfileService {
  baseUrl : string =  this.configService.apiUrl + "userprofile";

  private currentUserProfile$: Observable<UserProfile> | null = null;

  constructor(private http: HttpClient, private configService: ConfigService) { }

  getUserProfile(userName: string | undefined = undefined) : Observable<UserProfile> {
    if (userName) {
      return this.http.get<UserProfile>(this.baseUrl,
        { params: new HttpParams().append('userName', userName), withCredentials: true });
    }
    if (!this.currentUserProfile$) {
      this.currentUserProfile$ = this.http.get<UserProfile>(this.baseUrl,
        { withCredentials: true }).pipe(
          tap({ error: () => { this.currentUserProfile$ = null; } }),
          shareReplay(1)
        );
    }
    return this.currentUserProfile$;
  }

  invalidateCache(): void {
    this.currentUserProfile$ = null;
  }

  updateUserPrefernece(userPreferenceModel : UserPreferenceModel) : Observable<any> {
    return this.http.put(this.baseUrl + "/preference", userPreferenceModel, { withCredentials: true })
      .pipe(tap(() => this.invalidateCache()));
  }

  updateUserProfile(userProfile : UserProfile) : Observable<any> {
    return this.http.put(this.baseUrl, userProfile, { withCredentials: true })
      .pipe(tap(() => this.invalidateCache()));
  }
}
