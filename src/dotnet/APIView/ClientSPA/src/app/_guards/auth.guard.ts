import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, RouterStateSnapshot } from '@angular/router';

import { AuthService } from '../_services/auth/auth.service';
import { firstValueFrom } from 'rxjs';
import { ConfigService } from '../_services/config/config.service';

export const AuthGuard: CanActivateFn = async (route: ActivatedRouteSnapshot, state: RouterStateSnapshot) => {
  const authService = inject(AuthService);
  const configService = inject(ConfigService);

  let isLoggedIn : any = false;
  try {
    isLoggedIn = await firstValueFrom(authService.isLoggedIn());

    if (isLoggedIn != true)
    {
      window.location.href = configService.webAppUrl + "login?returnUrl=" + window.location.href;
    }
  }
  catch (error){
    isLoggedIn = false;
    window.location.href = configService.webAppUrl + "login?returnUrl=" + window.location.href;
  }
  return isLoggedIn;
};
