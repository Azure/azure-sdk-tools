import path from "node:path";
import { getUniqueDirName } from "../utils.ts";
import type { TypeCheckParameters, TypeCheckResult } from "../types.ts";

const fileName = "Temp.java";
let container: ContainerHost | undefined = undefined;

const importRegex = /^import\s+([a-zA-Z0-9_.]+);/gm;

interface MavenCoordinates {
    groupId: string;
    artifactId: string;
    version: string;
}

export async function getMavenCoordinatesFromAPI(
    javaClass: string,
): Promise<MavenCoordinates | undefined> {
    const query = encodeURIComponent(`fc:${javaClass}`);
    const url = `https://search.maven.org/solrsearch/select?q=${query}&rows=1&wt=json`;

    const res = await host.fetch(url);
    if (!res.ok) throw new Error("Failed to query Maven Central");

    const data = (await res.json()) as any;
    const doc = data.response?.docs?.[0];

    if (doc) {
        return {
            artifactId: doc.a,
            groupId: doc.g,
            version: doc.v,
        };
    }

    return undefined;
}

async function parseMavenDependencies(
    code: string,
    pkgName?: string,
): Promise<MavenCoordinates[]> {
    const deps = new Set<MavenCoordinates>();
    let match;
    while ((match = importRegex.exec(code)) !== null) {
        const dep = await getMavenCoordinatesFromAPI(match[1]);
        if (dep && (!pkgName || dep.artifactId !== pkgName)) {
            deps.add(dep);
        }
    }
    return Array.from(deps);
}

export async function typecheckJava({
    code,
    clientDist,
    pkgName,
}: TypeCheckParameters): Promise<TypeCheckResult> {
    const projectDir = path.join("tmp", getUniqueDirName());
    const filePath = `${projectDir}/${fileName}`;

    if (!container) {
        container = await host.container({
            image: "eclipse-temurin:24-jdk-alpine",
            networkEnabled: true,
            persistent: true,
        });
    }

    try {
        await container.writeText(filePath, code);

        // If clientDist is a jar, copy it and add to classpath
        let classpath = ".";
        if (clientDist) {
            await container.copyTo(clientDist, projectDir);
            const distName = path.basename(clientDist);
            classpath += `:${distName}`;
        }

        // Parse dependencies from code and generate a minimal pom.xml
        const deps = await parseMavenDependencies(code, pkgName);
        let installRes: ShellOutput | undefined;
        if (deps.length > 0) {
            const pomXml = `
<project xmlns="http://maven.apache.org/POM/4.0.0" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:schemaLocation="http://maven.apache.org/POM/4.0.0 http://www.maven.apache.org/xsd/maven-4.0.0.xsd">
  <modelVersion>4.0.0</modelVersion>
  <groupId>tempmod</groupId>
  <artifactId>tempmod</artifactId>
  <version>1.0-SNAPSHOT</version>
  <dependencies>
    ${deps
        .map(
            (d) => `<dependency>
      <groupId>${d.groupId}</groupId>
      <artifactId>${d.artifactId}</artifactId>
      <version>${d.version}</version>
    </dependency>`,
        )
        .join("\n")}
  </dependencies>
</project>
      `.trim();
            await container.writeText(path.join(projectDir, "pom.xml"), pomXml);

            await container.exec("apk", ["add", "--no-cache", "maven"]);
            installRes = await container.exec(
                "mvn",
                ["dependency:copy-dependencies"],
                { cwd: projectDir },
            );
            classpath += ":target/dependency/*";
        }

        const javacResult = await container.exec(
            "javac",
            ["-classpath", classpath, fileName],
            { cwd: projectDir },
        );

        return {
            succeeded:
                (!installRes ||
                    (installRes?.exitCode === 0 && !installRes.failed)) &&
                javacResult.exitCode === 0 &&
                !javacResult.failed,
            output: [
                `mvn output:\n${(installRes?.stdout ?? "") + (installRes?.stderr ?? "")}`,
                `javac output:\n${(javacResult.stdout ?? "") + (javacResult.stderr ?? "")}`,
            ].join("\n"),
        };
    } finally {
        try {
            await container.exec("rm", ["-rf", projectDir]);
        } catch (cleanupErr) {
            console.warn("Cleanup failed:", cleanupErr);
        }
    }
}
