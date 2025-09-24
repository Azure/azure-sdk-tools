package com.azure.tools.apiview.processor.diff;

import com.azure.tools.apiview.processor.diff.dto.ApiChangeDto;
import com.azure.tools.apiview.processor.diff.dto.ApiDiffResult;
import com.azure.tools.apiview.processor.diff.model.*;

import java.util.*;

/**
 * Computes semantic differences between two class symbol maps.
 */
public class DiffEngine {

    public ApiDiffResult diff(Map<String, ClassSymbol> oldClasses, Map<String, ClassSymbol> newClasses) {
        List<ApiChangeDto> list = new ArrayList<ApiChangeDto>();

        // Class additions/removals
        Set<String> allClasses = new HashSet<String>();
        allClasses.addAll(oldClasses.keySet());
        allClasses.addAll(newClasses.keySet());
        for (String fqn : allClasses) {
            ClassSymbol oldCls = oldClasses.get(fqn);
            ClassSymbol newCls = newClasses.get(fqn);
            if (oldCls == null) {
                list.add(classChange("AddedClass", null, fqn, newCls));
            } else if (newCls == null) {
                list.add(classChange("RemovedClass", fqn, null, oldCls));
            } else {
                // members
                diffFields(fqn, oldCls, newCls, list);
                diffMethods(fqn, oldCls, newCls, list);
            }
        }

        // Sort for determinism (by changeType then signature/fqn)
        Collections.sort(list, new Comparator<ApiChangeDto>() {
            public int compare(ApiChangeDto a, ApiChangeDto b) {
                int ct = safe(a.getChangeType()).compareTo(safe(b.getChangeType()));
                if (ct != 0) {
                    return ct;
                }
                String as = a.getMeta().getSignature() != null ? a.getMeta().getSignature() : a.getMeta().getFqn();
                String bs = b.getMeta().getSignature() != null ? b.getMeta().getSignature() : b.getMeta().getFqn();
                return safe(as).compareTo(safe(bs));
            }
        });
        ApiDiffResult result = new ApiDiffResult();
        for (ApiChangeDto c : list) result.addChange(c);
        return result;
    }

    private void diffFields(String fqn, ClassSymbol oldCls, ClassSymbol newCls, List<ApiChangeDto> out) {
        Set<String> names = new HashSet<String>();
        names.addAll(oldCls.getFields().keySet());
        names.addAll(newCls.getFields().keySet());
        for (String name : names) {
            FieldSymbol of = oldCls.getFields().get(name);
            FieldSymbol nf = newCls.getFields().get(name);
            if (of == null) {
                out.add(fieldChange("AddedField", null, describeField(nf), fqn, nf));
            } else if (nf == null) {
                out.add(fieldChange("RemovedField", describeField(of), null, fqn, of));
            } else {
                // Check type change
                if (!safe(of.getType()).equals(safe(nf.getType()))) {
                    ApiChangeDto ch = fieldChange("ModifiedFieldType", describeField(of), describeField(nf), fqn, nf);
                    ch.setCategory("FieldType");
                    out.add(ch);
                }
                // Deprecation change
                if (of.isDeprecated() != nf.isDeprecated()) {
                    ApiChangeDto ch = fieldChange("ModifiedFieldDeprecation", describeField(of), describeField(nf), fqn, nf);
                    ch.setCategory("Deprecation");
                    out.add(ch);
                }
            }
        }
    }

    private void diffMethods(String fqn, ClassSymbol oldCls, ClassSymbol newCls, List<ApiChangeDto> out) {
        // Compare by signature maps
        Set<String> allSigs = new HashSet<String>();
        allSigs.addAll(oldCls.getMethodsBySignature().keySet());
        allSigs.addAll(newCls.getMethodsBySignature().keySet());
        for (String sig : allSigs) {
            MethodSymbol om = oldCls.getMethodsBySignature().get(sig);
            MethodSymbol nm = newCls.getMethodsBySignature().get(sig);
            if (om == null) {
                out.add(methodAdded(fqn, nm));
            } else if (nm == null) {
                out.add(methodRemoved(fqn, om));
            } else {
                if (!safe(om.getReturnType()).equals(safe(nm.getReturnType()))) {
                    ApiChangeDto ch = methodChange("ModifiedMethodReturnType", fqn, om, nm);
                    ch.setCategory("ReturnType");
                    out.add(ch);
                }
                if (om.isDeprecated() != nm.isDeprecated()) {
                    ApiChangeDto ch = methodChange("ModifiedMethodDeprecation", fqn, om, nm);
                    ch.setCategory("Deprecation");
                    out.add(ch);
                }
                if (!safe(om.getVisibility()).equals(safe(nm.getVisibility()))) {
                    ApiChangeDto ch = methodChange("ModifiedMethodVisibility", fqn, om, nm);
                    ch.setCategory("Visibility");
                    out.add(ch);
                }
                // Parameter name changes (types same, names differ)
                if (sameParamTypes(om, nm) && !sameParamNames(om, nm)) {
                    ApiChangeDto ch = methodChange("ModifiedMethodParameterNames", fqn, om, nm);
                    ch.getMeta().setParamNameChange(Boolean.TRUE);
                    ch.setCategory("Parameters");
                    out.add(ch);
                }
            }
        }
        // Detect overload added/removed by comparing methodsByName
        Set<String> methodNames = new HashSet<String>();
        methodNames.addAll(oldCls.getMethodsByName().keySet());
        methodNames.addAll(newCls.getMethodsByName().keySet());
        for (String name : methodNames) {
            List<MethodSymbol> oldOver = oldCls.getMethodsByName().get(name);
            List<MethodSymbol> newOver = newCls.getMethodsByName().get(name);
            int oldCount = oldOver == null ? 0 : oldOver.size();
            int newCount = newOver == null ? 0 : newOver.size();
            if (oldCount != newCount) {
                ApiChangeDto ch = new ApiChangeDto();
                ch.setChangeType(oldCount < newCount ? "AddedOverload" : "RemovedOverload");
                ch.getMeta().setSymbolKind("Method").setFqn(fqn).setMethodName(name);
                ch.setCategory("Overload"); // removal considered breaking if fewer overloads, omitted impact field
                out.add(ch);
            }
        }
    }

    private ApiChangeDto methodAdded(String fqn, MethodSymbol m) {
        ApiChangeDto ch = baseMethodChange("AddedMethod", fqn, m);
        ch.setAfter(describeMethod(m)); // adding method typically non-breaking
        return ch;
    }

    private ApiChangeDto methodRemoved(String fqn, MethodSymbol m) {
        ApiChangeDto ch = baseMethodChange("RemovedMethod", fqn, m);
        ch.setBefore(describeMethod(m));
        return ch;
    }

    private ApiChangeDto methodChange(String type, String fqn, MethodSymbol oldM, MethodSymbol newM) {
        ApiChangeDto ch = baseMethodChange(type, fqn, newM);
        ch.setBefore(describeMethod(oldM));
        ch.setAfter(describeMethod(newM));
        return ch;
    }

    private ApiChangeDto baseMethodChange(String type, String fqn, MethodSymbol m) {
        ApiChangeDto ch = new ApiChangeDto();
        ch.setChangeType(type);
        ApiChangeDto.Meta meta = ch.getMeta();
        meta.setSymbolKind("Method")
            .setFqn(fqn)
            .setMethodName(m.getName())
            .setSignature(m.getFullSignature())
            .setReturnType(m.getReturnType())
            .setParameterTypes(extractParamTypes(m))
            .setParameterNames(extractParamNames(m))
            .setVisibility(m.getVisibility())
            .setDeprecated(m.isDeprecated() ? Boolean.TRUE : null);
        ch.setCategory(inferCategory(type));
        return ch;
    }

    private ApiChangeDto classChange(String type, String before, String after, ClassSymbol cls) {
        ApiChangeDto ch = new ApiChangeDto();
        ch.setChangeType(type);
        ch.setBefore(before);
        ch.setAfter(after);
        ApiChangeDto.Meta meta = ch.getMeta();
        meta.setSymbolKind("Class").setFqn(after != null ? after : before);
        // Class visibility not currently captured; infer from modifiers
        meta.setVisibility(cls.getModifiers().contains("public") ? "public" : (cls.getModifiers().contains("protected") ? "protected" : null));
        meta.setDeprecated(cls.isDeprecated() ? Boolean.TRUE : null);
        ch.setCategory("Type"); // removed impact classification
        return ch;
    }

    private ApiChangeDto fieldChange(String type, String before, String after, String fqn, FieldSymbol f) {
        ApiChangeDto ch = new ApiChangeDto();
        ch.setChangeType(type);
        ch.setBefore(before);
        ch.setAfter(after);
        ApiChangeDto.Meta meta = ch.getMeta();
        meta.setSymbolKind("Field")
            .setFqn(fqn)
            .setFieldName(f.getName())
            .setVisibility(f.getVisibility())
            .setDeprecated(f.isDeprecated() ? Boolean.TRUE : null);
        ch.setCategory(type.startsWith("Modified") ? "Field" : (type.startsWith("Added") ? "Field" : "Field")); // impact removed
        return ch;
    }

    private String describeMethod(MethodSymbol m) {
        StringBuilder sb = new StringBuilder();
        sb.append(m.getVisibility()).append(" ");
        boolean isConstructor = "void".equals(m.getReturnType()) && m.getName() != null && m.getName().length() > 0 && m.getName().equals(simpleNameFromFqn(m.getFqn()));
        if (isConstructor) {
            sb.append(m.getName());
        } else {
            sb.append(m.getReturnType()).append(" ").append(m.getName());
        }
        sb.append("(");
        String[] pTypes = extractParamTypes(m);
        String[] pNames = extractParamNames(m);
        for (int i = 0; i < pTypes.length; i++) {
            if (i > 0) {
                sb.append(", ");
            }
            sb.append(pTypes[i]).append(" ").append(pNames[i]);
        }
        sb.append(")");
        return sb.toString();
    }

    private String describeField(FieldSymbol f) {
        return f.getVisibility() + " " + f.getType() + " " + f.getName();
    }

    private String safe(String s) { return s == null ? "" : s; }

    private String inferCategory(String changeType) {
        if (changeType.contains("ReturnType")) {
            return "ReturnType";
        }
        if (changeType.contains("Parameter")) {
            return "Parameters";
        }
        if (changeType.contains("Overload")) {
            return "Overload";
        }
        if (changeType.contains("Visibility")) {
            return "Visibility";
        }
        return "Method";
    }

    private String[] extractParamTypes(MethodSymbol m) {
        String[] arr = new String[m.getParams().size()];
        for (int i = 0; i < m.getParams().size(); i++) arr[i] = m.getParams().get(i).getType();
        return arr;
    }

    private String[] extractParamNames(MethodSymbol m) {
        String[] arr = new String[m.getParams().size()];
        for (int i = 0; i < m.getParams().size(); i++) arr[i] = m.getParams().get(i).getName();
        return arr;
    }

    private boolean sameParamTypes(MethodSymbol a, MethodSymbol b) {
        if (a.getParams().size() != b.getParams().size()) {
            return false;
        }
        for (int i = 0; i < a.getParams().size(); i++) {
            if (!safe(a.getParams().get(i).getType()).equals(safe(b.getParams().get(i).getType()))) return false;
        }
        return true;
    }

    private boolean sameParamNames(MethodSymbol a, MethodSymbol b) {
        if (a.getParams().size() != b.getParams().size()) {
            return false;
        }
        for (int i = 0; i < a.getParams().size(); i++) {
            if (!safe(a.getParams().get(i).getName()).equals(safe(b.getParams().get(i).getName()))) return false;
        }
        return true;
    }

    private String simpleNameFromFqn(String fqn) {
        if (fqn == null) return null;
        int idx = fqn.lastIndexOf('.');
        return idx == -1 ? fqn : fqn.substring(idx + 1);
    }
}
