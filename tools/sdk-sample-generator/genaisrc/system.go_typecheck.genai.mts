import { typecheckGo } from "./src/go/typecheck.ts";

system({
  title: "Go code typechecking",
  description: "Registers a function that typechecks a Go file",
  system: ["system.go"],
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

  const clientDist = (env.vars["system.go_typecheck.client-dist"] ??
    env.vars["client-dist"]) as string | undefined;
  const pkgName = (env.vars["system.go_typecheck.client-dist-name"] ??
    env.vars["client-dist-name"]) as string | undefined;

  defTool(
    "go_typecheck",
    "Typechecks Go code and it typechecking fails, returns a list of go errors that you should fix.",
    {
      type: "object",
      properties: {
        code: {
          type: "string",
          description: "The content of the Go file.",
        },
      },
      required: ["code"],
    },
    async (args) => {
      const res = await typecheckGo({
        code: args.code,
        clientDist,
        pkgName,
      });
      return JSON.stringify(res, null, 2);
    },
  );
}
