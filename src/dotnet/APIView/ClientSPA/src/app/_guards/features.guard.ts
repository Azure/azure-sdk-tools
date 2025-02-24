import { ActivatedRouteSnapshot, CanActivateFn, Router, RouterStateSnapshot } from '@angular/router';
import { ConfigService } from '../_services/config/config.service';
import { inject } from '@angular/core';
import { UserProfileService } from '../_services/user-profile/user-profile.service';

export const FeaturesGuard: CanActivateFn = (route: ActivatedRouteSnapshot, state: RouterStateSnapshot) => {
  const userProfileService = inject(UserProfileService);
  const configService = inject(ConfigService);

  userProfileService.getUserProfile().subscribe((userProfile) => {
    if (!userProfile.preferences.useBetaIndexPage) {
      window.location.href = configService.webAppUrl;
    }
  });
  return true;
};
