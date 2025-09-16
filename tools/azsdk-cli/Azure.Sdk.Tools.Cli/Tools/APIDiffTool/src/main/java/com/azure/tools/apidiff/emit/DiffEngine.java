package com.azure.tools.apidiff.emit;

import com.azure.tools.apidiff.model.ApiChange;
import com.azure.tools.apidiff.parser.AstCollector;

import java.util.*;
import java.util.stream.Collectors;

public class DiffEngine {
    public List<ApiChange> diff(List<AstCollector.MethodSig> oldMethods, List<AstCollector.MethodSig> newMethods){
        Map<String, AstCollector.MethodSig> oldMap = oldMethods.stream().collect(Collectors.toMap(m -> m.fqn, m -> m, (a,b)->a));
        Map<String, AstCollector.MethodSig> newMap = newMethods.stream().collect(Collectors.toMap(m -> m.fqn, m -> m, (a,b)->a));
        List<ApiChange> changes = new ArrayList<>();

        // Added
        for (var entry : newMap.entrySet()) {
            if (!oldMap.containsKey(entry.getKey())) {
                changes.add(new ApiChange("MethodAdded", entry.getKey(), "added", null));
            }
        }
        // Removed
        for (var entry : oldMap.entrySet()) {
            if (!newMap.containsKey(entry.getKey())) {
                changes.add(new ApiChange("MethodRemoved", entry.getKey(), "removed", null));
            }
        }
        // Param name changes (same signature key, different ordered param names)
        for (var key : newMap.keySet()) {
            if (oldMap.containsKey(key)) {
                var o = oldMap.get(key);
                var n = newMap.get(key);
                if (!o.paramNames.equals(n.paramNames)) {
                    var meta = new ApiChange.Meta(String.join(",", o.paramNames), String.join(",", n.paramNames), true);
                    changes.add(new ApiChange("MethodSignatureChanged", key, "parameter-names", meta));
                }
            }
        }
        return changes;
    }
}
