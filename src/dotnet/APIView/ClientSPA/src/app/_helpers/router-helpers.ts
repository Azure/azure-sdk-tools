import { ActivatedRoute } from "@angular/router";
import { SCROLL_TO_NODE_QUERY_PARAM } from "./common-helpers";

export function getQueryParams(route: ActivatedRoute, excludedKeys: string[] = [SCROLL_TO_NODE_QUERY_PARAM]) {
  return route.snapshot.queryParamMap.keys.reduce((params: { [key: string]: any; }, key) => {
    if (!excludedKeys.includes(key)) {
      params[key] = route.snapshot.queryParamMap.get(key);
    }
    return params;
  }, {});
}