import { ActivatedRoute } from "@angular/router";

export function getQueryParams(route: ActivatedRoute, excludedKeys: string[] = ["nId"]) {
  return route.snapshot.queryParamMap.keys.reduce((params: { [key: string]: any; }, key) => {
    if (!excludedKeys.includes(key)) {
      params[key] = route.snapshot.queryParamMap.get(key);
    }
    return params;
  }, {});
}