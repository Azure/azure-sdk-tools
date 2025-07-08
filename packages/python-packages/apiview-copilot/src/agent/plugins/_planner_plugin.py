from semantic_kernel.functions import kernel_function


class PlannerPlugin:
    @kernel_function(description="First step for any deletion that describes how to process the delete operation.")
    async def plan_delete_operation(self, container: str, id: str):
        """
        First step for any deletion that describes how to process the delete operation.
        Args:
            container (str): The container of the object to delete.
            id (str): The ID of the object to delete.
        """
        if container in ["guidelines", "guideline"]:
            return "We do not delete guidelines."
        elif container in ["examples", "example"]:
            return """
To delete an example, you must first unlink it from any related memories or guidelines.
Once it is completely unlinked, you can delete the example from the database.
            """
        elif container in ["memories", "memory"]:
            return """
To delete a memory, you must first unlink it from any related examples or guidelines.
Once it is completely unlinked, you can delete the memory from the database.
            """
        elif container in ["review-jobs", "review-job"]:
            return "Simply delete the review job from the database."
        else:
            raise ValueError(f"Unsupported container: {container}")
