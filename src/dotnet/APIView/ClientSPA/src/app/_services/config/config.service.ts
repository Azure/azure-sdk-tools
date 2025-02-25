import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { BehaviorSubject, map } from 'rxjs';
import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ConfigService {
  private appTheme = new BehaviorSubject<string>('light-theme');
  appTheme$ = this.appTheme.asObservable();

  private assetsPath : string = environment.assetsPath;
  private config: any = {
    apiUrl : "api/",
    hubUrl : "hubs/",
    webAppUrl : "http://localhost:5000/"
  };

  constructor(private http: HttpClient) { }

  loadConfig() {
    return this.http.get(`${this.assetsPath}/config.json`).pipe(
      map((config: any) => {
        this.config = config;
      }));
  }

  setAppTheme(value: string) {
    this.appTheme.next(value);
  }

  get apiUrl() : string {
    return this.config.apiUrl;
  }

  get hubUrl() : string {
    return this.config.hubUrl;
  }

  get webAppUrl () : string {
    return this.config.webAppUrl;
  }
}