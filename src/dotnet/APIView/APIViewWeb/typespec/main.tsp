import "@typespec/http";
import "@typespec/openapi3";
import "./models.tsp";
import "./autoreview.tsp";
import "./apirevisions.tsp";
import "./comments.tsp";

using TypeSpec.Http;

/**
 * External-facing APIs for APIView that support API Key Authentication or Azure AD Token Authentication.
 * Version: 1.0.0
 */
@service(#{
    title: "APIView External APIs"
})
@server("https://apiview.dev/", "APIView Production")
@server("https://apiviewstagingmain.z13.web.core.windows.net/", "APIView Staging")

@useAuth(ApiKeyAuth<ApiKeyLocation.header, "ApiKey"> | BearerAuth)
namespace APIView;
