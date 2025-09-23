from uuid import uuid4


class CommentGrouper:
    """
    Determine which comments in a batch are similar enough to be
    grouped together with a correlation_id.
    """

    def __init__(self, comments):
        self.comments = comments

    def group(self) -> list:
        """
        Algorithm to group comments together.
        """
        signature_map = {}
        generic_only = []
        # any comments which relate to the same guidelines and memories are considered similar
        for idx, comment in enumerate(self.comments):
            # skip comments that have no guideline_ids AND no memory_ids
            if not comment.guideline_ids and not comment.memory_ids:
                if comment.is_generic:
                    generic_only.append(idx)
                continue

            guideline_ids = ",".join(sorted(comment.guideline_ids))
            memory_ids = ",".join(sorted(comment.memory_ids))
            signature = hash((guideline_ids, memory_ids))
            if signature in signature_map:
                signature_map[signature].append(idx)
            else:
                signature_map[signature] = [idx]

        # update correlation ID for comments which are grouped together
        for signature, indices in signature_map.items():
            if len(indices) > 1:
                correlation_id = str(uuid4())
                for idx in indices:
                    self.comments[idx].correlation_id = correlation_id

        return self.comments
