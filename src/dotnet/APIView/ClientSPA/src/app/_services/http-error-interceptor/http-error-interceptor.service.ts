import { HttpErrorResponse, HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, Observable, of, throwError } from 'rxjs';
import { environment } from 'src/environments/environment';

@Injectable()
export class HttpErrorInterceptorService implements HttpInterceptor{
  intercept(request: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    return next.handle(request)
      .pipe(
        catchError((error: HttpErrorResponse) => {
          let errorMessage = 'Unknown error occurred';
          if (error.error instanceof ErrorEvent) {
            errorMessage = `Client Side: ${error.error.message}`;
          } else {
            errorMessage = `Server Side: ${error.message}`;
          }
          const customError = {
            ...error,
            message: errorMessage
          };

          if (!environment.production) {
            return throwError(() => customError);
          }
          return of();
        })
      );
  }
}
