import { Injectable } from '@angular/core';
import { Observable, } from 'rxjs';
import { ConfigService } from '../config/config.service';
import { HttpClient } from '@angular/common/http';
import { UserProfile } from 'src/app/_models/userProfile';
import { UserPreferenceModel } from 'src/app/_models/userPreferenceModel';

@Injectable({
  providedIn: 'root'
})
export class UserProfileService {
  baseUrl : string =  this.configService.apiUrl + "userprofile";

  constructor(private http: HttpClient, private configService: ConfigService) { }

  getUserProfile() : Observable<UserProfile> {
    return this.http.get<UserProfile>(this.baseUrl, { withCredentials: true });
  }

  updateUserPrefernece(userPreferenceModel : UserPreferenceModel) : Observable<any> {   
    return this.http.put(this.baseUrl + "/preference", userPreferenceModel, { withCredentials: true });
  }
}
