import { typecheckDotNet } from "./src/dotnet/typecheck.ts";

system({
  title: "C# code typechecking",
  description:
    "Registers a function that typechecks a C# file inside a container",
  system: ["system.csharp"],
  parameters: {
    "client-dist": {
      type: "string",
      description:
        "The client distribution that should be installed when verifying the samples.",
    },
    "client-dist-name": {
      type: "string",
      description: "The name of the client distribution, e.g. package name",
    },
  },
});

export default function (ctx: ChatGenerationContext) {
  const { defTool, env } = ctx;

  const clientDist = (env.vars["system.csharp_typecheck.client-dist"] ??
    env.vars["client-dist"]) as string | undefined;
  const pkgName = (env.vars["system.csharp_typecheck.client-dist-name"] ??
    env.vars["client-dist-name"]) as string | undefined;

  defTool(
    "csharp_typecheck",
    "Typechecks C# code and if typechecking fails, returns a list of C# compilation errors that you should fix.",
    {
      type: "object",
      properties: {
        code: {
          type: "string",
          description: "The content of the C# file.",
        },
      },
      required: ["code"],
    },
    async (args) => {
      const res = await typecheckDotNet({
        code: args.code,
        clientDist,
        pkgName,
      });
      return JSON.stringify(res, null, 2);
    },
  );
}
