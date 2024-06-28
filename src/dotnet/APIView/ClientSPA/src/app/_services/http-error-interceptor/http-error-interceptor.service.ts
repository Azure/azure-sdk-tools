import { HttpErrorResponse, HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, Observable, throwError } from 'rxjs';

@Injectable()
export class HttpErrorInterceptorService implements HttpInterceptor{
  intercept(request: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    return next.handle(request)
      .pipe(
        catchError((error: HttpErrorResponse) => {
          let errorMessage = 'Unknown error occurred';
          if (error.error instanceof ErrorEvent) {
            // Client-side error
            errorMessage = `Client Side: ${error.error.message}`;
          } else {
            // Server-side error
            errorMessage = `Server Side: ${error.status}\nMessage: ${error.message}`;
          }
          // Here you can add more error handling logic, like logging errors or showing a notification to the user
          console.error(errorMessage);
          return throwError(() => new Error(errorMessage));
        })
      );
  }
}
