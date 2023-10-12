import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, RouterStateSnapshot } from '@angular/router';

import { AuthService } from '../_services/auth/auth.service';
import { firstValueFrom } from 'rxjs';
import { environment } from 'src/environments/environment';

export const AuthGuard: CanActivateFn = async (route: ActivatedRouteSnapshot, state: RouterStateSnapshot) => {
  const authService = inject(AuthService);
  let isLoggedIn : any = false;
  try {
    isLoggedIn = await firstValueFrom(authService.isLoggedIn());

    if (isLoggedIn != true)
    {
      console.log(`Inside AuthGuard isLoggedIn ${isLoggedIn}`);
      window.location.href = environment.webAppUrl + "login";
    }
  }
  catch (error){
    isLoggedIn = false;
    window.location.href = environment.webAppUrl + "login";
  }
  return isLoggedIn;
};
