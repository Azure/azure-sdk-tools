import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import {  BehaviorSubject, Observable, of, shareReplay, switchMap, tap } from 'rxjs';
import { AppVersion  } from 'src/app/_models/auth_service_models';
import { ConfigService } from '../config/config.service';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  baseUrl : string =  this.configService.apiUrl + "auth";
  isLoggedIn$: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
  
  constructor(private http: HttpClient, private configService: ConfigService) { }
  
  isLoggedIn(): Observable<boolean> {
    return this.isLoggedIn$.pipe(
      switchMap(isLoggedIn => {
        if (!isLoggedIn) {
          return this.http.get(this.baseUrl, { withCredentials: true }).pipe(
            tap((response: any) => {
              this.isLoggedIn$.next(response.isLoggedIn);
            }),
            shareReplay(1)
          );
        } else {
          return of(isLoggedIn);
        }
      })
    );
  }

  appVersion() : Observable<AppVersion> {
    return this.http.get<AppVersion>(this.baseUrl + "/appversion", { withCredentials: true });
  }
}

