import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map } from 'rxjs';
import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ConfigService {
  private assetsPath : string = environment.assetsPath;
  private config: any = {
    apiUrl : "api/",
    webAppUrl : "http://localhost:5000/"
  };

  constructor(private http: HttpClient) { }

  loadConfig() {
    return this.http.get(`${this.assetsPath}/config.json`).pipe(
      map((config: any) => {
        this.config = config;
      }));
  }

  get apiUrl() : string {
    return this.config.apiUrl;
  }

  get webAppUrl () : string {
    return this.config.webAppUrl;
  }
}