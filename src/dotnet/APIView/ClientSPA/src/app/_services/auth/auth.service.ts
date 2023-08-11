import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  baseUrl : string =  environment.apiUrl + "auth";

  constructor(private http: HttpClient) { }

  isLoggedIn() {
    return this.http.get(this.baseUrl, { withCredentials: true });
  }
}
