import { typecheckPython } from "./src/python/typecheck.ts";

system({
  title: "Python code typechecking",
  description: "Registers a function that typechecks a Python file",
  system: ["system.python"],
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

  const clientDist = (env.vars["system.python_typecheck.client-dist"] ??
    env.vars["client-dist"]) as string | undefined;
  const pkgName = (env.vars["system.python_typecheck.client-dist-name"] ??
    env.vars["client-dist-name"]) as string | undefined;

  defTool(
    "python_typecheck",
    "Typechecks Python code and it typechecking fails, returns a list of python errors that you should fix.",
    {
      type: "object",
      properties: {
        code: {
          type: "string",
          description: "The content of the Python file.",
        },
      },
      required: ["code"],
    },
    async (args) => {
      const res = await typecheckPython({
        code: args.code,
        clientDist,
        pkgName,
      });
      return JSON.stringify(res, null, 2);
    },
  );
}
