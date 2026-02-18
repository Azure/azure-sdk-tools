import { Component } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { take, forkJoin } from 'rxjs';
import { USER_NAME_ROUTE_PARAM } from 'src/app/_helpers/router-helpers';
import { UserProfile } from 'src/app/_models/userProfile';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { PermissionsService } from 'src/app/_services/permissions/permissions.service';
import { GroupPermissions, ROLE_DISPLAY_NAMES } from 'src/app/_models/permissions';

@Component({
    selector: 'app-profile-page',
    templateUrl: './profile-page.component.html',
    styleUrl: './profile-page.component.scss',
    standalone: false
})
export class ProfilePageComponent {
  userName : string | null = null;
  userEmail : string | undefined = undefined;
  userProfile : UserProfile | undefined;

  notificationEmail: string | null = null;
  themes = [
    { label: "light", data: "light-theme" },
    { label: "dark", data: "dark-theme" },
    { label: "dark-solarized", data: "dark-solarized-theme" }
  ];
  selectedTheme = { label: "light", data: "light-theme" };
  disableSaveButton : boolean = true;
  isLoaded: boolean | undefined = undefined;

  // New properties for permissions display
  userGroups: GroupPermissions[] = [];
  adminUsernames: string[] = [];
  readonly ROLE_DISPLAY_NAMES = ROLE_DISPLAY_NAMES;

  constructor(private route: ActivatedRoute, private userProfileService: UserProfileService,
    private permissionsService: PermissionsService) {}

  ngOnInit() {
    this.userName = this.route.snapshot.paramMap.get(USER_NAME_ROUTE_PARAM);
    if (this.userName) {
      this.userProfileService.getUserProfile().subscribe({
        next: (userProfile : UserProfile) => {
          this.userProfile = userProfile;
          this.notificationEmail = userProfile.email;
          this.selectedTheme = this.themes.filter(t => t.data === userProfile.preferences.theme)[0];

          // Load user's groups and admin list
          this.loadPermissionsInfo();

          if (this.userName !== userProfile.userName) {
            this.userProfileService.getUserProfile(this.userName!).subscribe({
              next: (userProfile: UserProfile) => {
                this.userEmail = userProfile.email;
                this.isLoaded = true;
              },
              error: (error: any) => {
                this.isLoaded = false;
              }
            });
          } else {
            this.isLoaded = true;
          }
        },
        error: (error: any) => {
          this.isLoaded = false;
        }
      });
    }
  }

  loadPermissionsInfo() {
    forkJoin({
      groups: this.permissionsService.getMyGroups(),
      admins: this.permissionsService.getAdminUsernames()
    }).pipe(take(1)).subscribe({
      next: (result) => {
        this.userGroups = result.groups;
        this.adminUsernames = result.admins;
      },
      error: (error) => {
        console.error('Failed to load permissions info', error);
      }
    });
  }

  saveProfileChanges() {
    this.disableSaveButton = true;
    this.userProfile!.email = this.notificationEmail!;
    this.userProfile!.preferences.theme = this.selectedTheme.data;
    this.userProfileService.updateUserProfile(this.userProfile!).pipe(take(1)).subscribe({
      next: (response: any) => {
        window.location.reload();
      },
      error: (error: any) => {
        this.disableSaveButton = false;
      }
    });
  }

  onProfileChange(event: any){
    if (event !== null && event !== undefined) {
      // Update the model for input fields
      if (typeof event === 'string') {
        this.notificationEmail = event;
      }
    }
    this.disableSaveButton = false;
  }

  formatGroupRoles(group: GroupPermissions): string {
    return group.roles.map(role => {
      if (role.kind === 'global') {
        return this.ROLE_DISPLAY_NAMES[role.role] || role.role;
      } else {
        return `${this.ROLE_DISPLAY_NAMES[role.role] || role.role} (${role.language})`;
      }
    }).join(', ');
  }

  formatRoleBadge(role: any): string {
    if (role.kind === 'global') {
      return this.ROLE_DISPLAY_NAMES[role.role] || role.role;
    } else {
      return `${this.ROLE_DISPLAY_NAMES[role.role] || role.role} - ${role.language}`;
    }
  }

  formatAdminList(): string {
    return this.adminUsernames.join(', ');
  }
}
