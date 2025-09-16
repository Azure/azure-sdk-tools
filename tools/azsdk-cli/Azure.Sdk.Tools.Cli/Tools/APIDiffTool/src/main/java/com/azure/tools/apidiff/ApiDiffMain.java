package com.azure.tools.apidiff;

import com.azure.tools.apidiff.emit.DiffEngine;
import com.azure.tools.apidiff.model.ApiChange;
import com.azure.tools.apidiff.parser.AstCollector;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;

import java.nio.file.Path;
import java.util.*;

public class ApiDiffMain {
    public static void main(String[] args) throws Exception {
        Map<String,String> argMap = parseArgs(args);
        String oldPath = argMap.get("--old");
        String newPath = argMap.get("--new");
        String format = argMap.getOrDefault("--format", "json");
        if (oldPath == null || newPath == null) {
            System.err.println("Missing --old or --new path");
            System.exit(2);
        }
        AstCollector collector = new AstCollector();
        var oldMethods = collector.collectMethods(Path.of(oldPath));
        var newMethods = collector.collectMethods(Path.of(newPath));
        DiffEngine engine = new DiffEngine();
        List<ApiChange> changes = engine.diff(oldMethods, newMethods);
        if ("json".equalsIgnoreCase(format)) {
            ObjectMapper mapper = new ObjectMapper();
            mapper.enable(SerializationFeature.INDENT_OUTPUT);
            System.out.println(mapper.writeValueAsString(changes));
        } else {
            for (var c : changes) {
                System.out.println(c.getKind() + "\t" + c.getSymbol());
            }
        }
    }

    private static Map<String,String> parseArgs(String[] args){
        Map<String,String> m = new HashMap<>();
        for (int i=0;i<args.length;i++) {
            if (args[i].startsWith("--")) {
                String key = args[i];
                String val = (i+1 < args.length && !args[i+1].startsWith("--")) ? args[++i] : "true";
                m.put(key, val);
            }
        }
        return m;
    }
}
