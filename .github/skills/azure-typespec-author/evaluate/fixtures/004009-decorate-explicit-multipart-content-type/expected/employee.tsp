import "@typespec/http";
using TypeSpec.Http;
namespace WidgetUpload;
/** Employee profile upload metadata. */
model Employee { name: string; }
/** Upload an employee profile on Widget's data plane, not ARM. */
@route("/employees/{employeeName}/profile")
@post
op upload(
  @path employeeName: string,
  @header contentType: "multipart/form-data",
  @multipartBody body: { file: HttpPart<bytes>; metadata: HttpPart<Employee>; }
): void;
