import { typecheckJava } from "./src/java/typecheck.ts";

system({
  title: "Java code typechecking",
  description:
    "Registers a function that typechecks a Java file inside a container",
  system: ["system.java"],
  parameters: {
    "client-dist": {
      type: "string",
      description:
        "The client distribution that should be installed when verifying the samples",
      required: false,
    },
    "client-dist-name": {
      type: "string",
      description:
        "The name of the client distribution, e.g. groupId:artifactId",
      required: false,
    },
  },
});

export default function (ctx: ChatGenerationContext) {
  const { defTool, env } = ctx;

  const clientDist = (env.vars["system.java_typecheck.client-dist"] ??
    env.vars["client-dist"]) as string | undefined;
  const pkgName = (env.vars["system.java_typecheck.client-dist-name"] ??
    env.vars["client-dist-name"]) as string | undefined;

  defTool(
    "java_typecheck",
    "Typechecks Java code and if compilation fails, returns a list of javac errors that you should fix.",
    {
      type: "object",
      properties: {
        code: {
          type: "string",
          description: "The content of the Java file.",
        },
      },
      required: ["code"],
    },
    async (args) => {
      const res = await typecheckJava({
        code: args.code,
        clientDist,
        pkgName,
      });
      return JSON.stringify(res, null, 2);
    },
  );
}
