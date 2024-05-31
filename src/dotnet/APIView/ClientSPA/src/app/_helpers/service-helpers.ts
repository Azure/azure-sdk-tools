import { HttpErrorResponse } from "@angular/common/http";
import { ConfigService } from "../_services/config/config.service";

export function HandleApiError(error: HttpErrorResponse, configService: ConfigService) {
  if (error.status === 401) {
    window.location.href = configService.webAppUrl + "login";
  }
}