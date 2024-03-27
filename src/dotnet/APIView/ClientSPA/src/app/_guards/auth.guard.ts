import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, RouterStateSnapshot } from '@angular/router';

import { AuthService } from '../_services/auth/auth.service';
import { firstValueFrom } from 'rxjs';
import { environment } from 'src/environments/environment';
import { ConfigService } from '../_services/config/config.service';

export const AuthGuard: CanActivateFn = async (route: ActivatedRouteSnapshot, state: RouterStateSnapshot) => {
  const authService = inject(AuthService);
  const configService = inject(ConfigService);

  let isLoggedIn : any = false;
  try {
    isLoggedIn = await firstValueFrom(authService.isLoggedIn());

    if (isLoggedIn != true)
    {
      console.log(`Inside AuthGuard isLoggedIn ${isLoggedIn}`);
      window.location.href = configService.webAppUrl + "login";
    }
  }
  catch (error){
    isLoggedIn = false;
    window.location.href = configService.webAppUrl + "login";
  }
  return isLoggedIn;
};
