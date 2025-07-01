import { ReviewLine } from "../../models/apiview-models";

export type AnnotatedReviewLines = {
  siblingModule: {
    [id: number]: ReviewLine[];
  };
  children: {
    [id: number]: ReviewLine[];
  };
};
