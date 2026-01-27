Use the following methods to classify the plane (check in order):

1. **PR Label Analysis**: If the question includes an azure-rest-api-specs PR link:
   - Check if the PR has a `data-plane` label → classify as **data-plane**
   - Check if the PR has a `management-plane` label → classify as **management-plane**

2. **File Path Analysis**: If the question mentions a file path:
   - Path contains `resource-manager` → classify as **management-plane**
   - Path contains `data-plane` → classify as **data-plane**

3. **Keyword Detection**:
   - Question contains management-plane, ARM → classify as **management-plane**
   - Question contains data-plane → classify as **data-plane**

4. **No Clear Signal**: If none of the above methods provide a clear signal, set plane to **unknown**