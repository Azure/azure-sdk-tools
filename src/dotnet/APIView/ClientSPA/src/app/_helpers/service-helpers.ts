import { HttpErrorResponse } from "@angular/common/http";
import { ConfigService } from "../_services/config/config.service";

export function HandleRedirectDueToExpiredCredentials(error: HttpErrorResponse, configService: ConfigService) {
  if (error.status === 302) {
    window.location.href = configService.webAppUrl + "login";
  }
}