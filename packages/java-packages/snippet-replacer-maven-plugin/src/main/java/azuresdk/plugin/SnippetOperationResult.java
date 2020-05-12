package azuresdk.plugin;

import java.util.ArrayList;
import java.util.List;

public class SnippetOperationResult<T> {
    public T result;
    List<VerifyResult> errorList;

    public SnippetOperationResult(T resultObject, List<VerifyResult> errors){
        super();
        this.result = resultObject;
        this.errorList = errors;
    }
}
