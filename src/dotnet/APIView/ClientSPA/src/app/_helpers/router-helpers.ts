import { ActivatedRoute } from "@angular/router";

export const REVIEW_ID_ROUTE_PARAM = "reviewId";
export const ACTIVE_API_REVISION_ID_QUERY_PARAM = "activeApiRevisionId";
export const ACTIVE_SAMPLES_REVISION_ID_QUERY_PARAM = "activeSamplesRevisionId";
export const DIFF_API_REVISION_ID_QUERY_PARAM = "diffApiRevisionId";
export const DIFF_STYLE_QUERY_PARAM = "diffStyle";
export const SCROLL_TO_NODE_QUERY_PARAM = "nId";

export const INDEX_PAGE_NAME = "Index";
export const REVIEW_PAGE_NAME = "Review";
export const CONVERSATION_PAGE_NAME = "Conversation";
export const REVISION_PAGE_NAME = "Revision";
export const SAMPLES_PAGE_NAME = "Samples";

export function getQueryParams(route: ActivatedRoute, excludedKeys: string[] = [SCROLL_TO_NODE_QUERY_PARAM]) {
  return route.snapshot.queryParamMap.keys.reduce((params: { [key: string]: any; }, key) => {
    if (!excludedKeys.includes(key)) {
      params[key] = route.snapshot.queryParamMap.get(key);
    }
    return params;
  }, {});
}