import { HttpErrorResponse, HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, EMPTY, Observable, throwError } from 'rxjs';
import { environment } from 'src/environments/environment';

@Injectable()
export class HttpErrorInterceptorService implements HttpInterceptor{
  private static lastRedirectTime = 0;
  private static readonly REDIRECT_COOLDOWN_MS = 1000;

  intercept(request: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    return next.handle(request)
      .pipe(
        catchError((error: HttpErrorResponse) => {
          if (error.status === 403) {
            const now = Date.now();            
            if (now - HttpErrorInterceptorService.lastRedirectTime > HttpErrorInterceptorService.REDIRECT_COOLDOWN_MS) {
              HttpErrorInterceptorService.lastRedirectTime = now;
              // Remove 'spa.' only if it appears at the start of the hostname
              const url = new URL(window.location.origin);
              if (url.hostname.startsWith('spa.')) {
                url.hostname = url.hostname.substring(4); // Remove 'spa.' (length 4)
              }
              const baseUrl = url.origin;
              window.location.href = `${baseUrl}/Unauthorized?returnUrl=/`;
            }
            
            return EMPTY;
          }

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
          return throwError(() => customError);
        })
      );
  }
}
