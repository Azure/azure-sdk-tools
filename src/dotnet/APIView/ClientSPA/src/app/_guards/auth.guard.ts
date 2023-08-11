import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, RouterStateSnapshot } from '@angular/router';

import { AuthService } from '../_services/auth/auth.service';

export const AuthGuard: CanActivateFn = async (route: ActivatedRouteSnapshot, state: RouterStateSnapshot) => {
  const authService = inject(AuthService);
  let isLoggedIn : any = false;
  try {
    isLoggedIn = await authService.isLoggedIn().toPromise();
    if (isLoggedIn != true)
    {
      window.location.href = "http://localhost:5000/login";
    }
  }
  catch (error){
    console.log(error);
    isLoggedIn = false;
    window.location.href = "http://localhost:5000/login";
  }

  return isLoggedIn;
};
