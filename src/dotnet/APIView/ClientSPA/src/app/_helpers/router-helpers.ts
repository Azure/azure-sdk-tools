import { ActivatedRoute } from "@angular/router";

export function getQueryParams(route: ActivatedRoute) {
  return route.snapshot.queryParamMap.keys.reduce((params: { [key: string]: any; }, key) => {
    params[key] = route.snapshot.queryParamMap.get(key);
    return params;
  }, {});
}