import { typecheckTypeScript } from "./src/typescript/typecheck.ts";

system({
  title: "Typescript code typechecking",
  description:
    "Registers a function that typechecks a TypeScript file inside a container",
  system: ["system.typescript"],
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

  const clientDist = (env.vars["system.typescript_typecheck.client-dist"] ??
    env.vars["client-dist"]) as string | undefined;
  const pkgName = (env.vars["system.typescript_typecheck.client-dist-name"] ??
    env.vars["client-dist-name"]) as string | undefined;

  defTool(
    "typescript_typecheck",
    "Typechecks TypeScript code and it typechecking fails, returns a list of typescript errors that you should fix.",
    {
      type: "object",
      properties: {
        code: {
          type: "string",
          description: "The content of the TypeScript file.",
        },
      },
      required: ["code"],
    },
    async (args) => {
      const res = await typecheckTypeScript({
        code: args.code,
        clientDist,
        pkgName,
      });
      return JSON.stringify(res, null, 2);
    },
  );
}
