import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { AppVersion } from 'src/app/_models/auth_service_models';
import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  baseUrl : string =  environment.apiUrl + "auth";

  constructor(private http: HttpClient) { }

  isLoggedIn() : Observable<boolean> {
    return this.http.get(this.baseUrl, { withCredentials: true }).pipe(
      map((response : any) => {
        return response.isLoggedIn;
      }));
  }

  appVersion() : Observable<AppVersion> {
    return this.http.get<AppVersion>(this.baseUrl + "/appversion", { withCredentials: true });
  }
}
