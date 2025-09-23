package com.azure.tools.apiview.processor.diff;

import com.azure.tools.apiview.processor.diff.dto.ApiChangeDto;
import com.azure.tools.apiview.processor.diff.model.*;

import java.util.*;

/**
 * Computes semantic differences between two ApiSymbolTable instances.
 */
public class DiffEngine {

    public List<ApiChangeDto> diff(ApiSymbolTable oldTable, ApiSymbolTable newTable) {
        List<ApiChangeDto> out = new ArrayList<ApiChangeDto>();

        // Class additions/removals
        Set<String> allClasses = new HashSet<String>();
        allClasses.addAll(oldTable.classes.keySet());
        allClasses.addAll(newTable.classes.keySet());
        for (String fqn : allClasses) {
            ClassSymbol oldCls = oldTable.classes.get(fqn);
            ClassSymbol newCls = newTable.classes.get(fqn);
            if (oldCls == null) {
                out.add(classChange("AddedClass", null, fqn, newCls));
            } else if (newCls == null) {
                out.add(classChange("RemovedClass", fqn, null, oldCls));
            } else {
                // members
                diffFields(fqn, oldCls, newCls, out);
                diffMethods(fqn, oldCls, newCls, out);
            }
        }

        // Sort for determinism (by changeType then signature/fqn)
        Collections.sort(out, new Comparator<ApiChangeDto>() {
            public int compare(ApiChangeDto a, ApiChangeDto b) {
                int ct = safe(a.changeType).compareTo(safe(b.changeType));
                if (ct != 0) return ct;
                String as = a.meta.signature != null ? a.meta.signature : a.meta.fqn;
                String bs = b.meta.signature != null ? b.meta.signature : b.meta.fqn;
                return safe(as).compareTo(safe(bs));
            }
        });
        return out;
    }

    private void diffFields(String fqn, ClassSymbol oldCls, ClassSymbol newCls, List<ApiChangeDto> out) {
        Set<String> names = new HashSet<String>();
        names.addAll(oldCls.fields.keySet());
        names.addAll(newCls.fields.keySet());
        for (String name : names) {
            FieldSymbol of = oldCls.fields.get(name);
            FieldSymbol nf = newCls.fields.get(name);
            if (of == null) {
                out.add(fieldChange("AddedField", null, describeField(nf), fqn, nf));
            } else if (nf == null) {
                out.add(fieldChange("RemovedField", describeField(of), null, fqn, of));
            } else {
                // Check type change
                if (!safe(of.type).equals(safe(nf.type))) {
                    ApiChangeDto ch = fieldChange("ModifiedFieldType", describeField(of), describeField(nf), fqn, nf);
                    ch.category = "FieldType";
                    ch.impact = "Breaking"; // changing type typically breaking
                    out.add(ch);
                }
                // Deprecation change
                if (of.deprecated != nf.deprecated) {
                    ApiChangeDto ch = fieldChange("ModifiedFieldDeprecation", describeField(of), describeField(nf), fqn, nf);
                    ch.category = "Deprecation";
                    ch.impact = "NonBreaking"; // deprecation addition not breaking
                    out.add(ch);
                }
            }
        }
    }

    private void diffMethods(String fqn, ClassSymbol oldCls, ClassSymbol newCls, List<ApiChangeDto> out) {
        // Compare by signature maps
        Set<String> allSigs = new HashSet<String>();
        allSigs.addAll(oldCls.methodsBySignature.keySet());
        allSigs.addAll(newCls.methodsBySignature.keySet());
        for (String sig : allSigs) {
            MethodSymbol om = oldCls.methodsBySignature.get(sig);
            MethodSymbol nm = newCls.methodsBySignature.get(sig);
            if (om == null) {
                out.add(methodAdded(fqn, nm));
            } else if (nm == null) {
                out.add(methodRemoved(fqn, om));
            } else {
                if (!safe(om.returnType).equals(safe(nm.returnType))) {
                    ApiChangeDto ch = methodChange("ModifiedMethodReturnType", fqn, om, nm);
                    ch.category = "ReturnType";
                    ch.impact = "Breaking";
                    out.add(ch);
                }
                if (om.deprecated != nm.deprecated) {
                    ApiChangeDto ch = methodChange("ModifiedMethodDeprecation", fqn, om, nm);
                    ch.category = "Deprecation";
                    ch.impact = "NonBreaking";
                    out.add(ch);
                }
                if (!safe(om.visibility).equals(safe(nm.visibility))) {
                    ApiChangeDto ch = methodChange("ModifiedMethodVisibility", fqn, om, nm);
                    ch.category = "Visibility";
                    ch.impact = "Breaking";
                    out.add(ch);
                }
                // Parameter name changes (types same, names differ)
                if (sameParamTypes(om, nm) && !sameParamNames(om, nm)) {
                    ApiChangeDto ch = methodChange("ModifiedMethodParameterNames", fqn, om, nm);
                    ch.meta.paramNameChange = Boolean.TRUE;
                    ch.category = "Parameters";
                    ch.impact = "NonBreaking";
                    out.add(ch);
                }
            }
        }
        // Detect overload added/removed by comparing methodsByName
        Set<String> methodNames = new HashSet<String>();
        methodNames.addAll(oldCls.methodsByName.keySet());
        methodNames.addAll(newCls.methodsByName.keySet());
        for (String name : methodNames) {
            List<MethodSymbol> oldOver = oldCls.methodsByName.get(name);
            List<MethodSymbol> newOver = newCls.methodsByName.get(name);
            int oldCount = oldOver == null ? 0 : oldOver.size();
            int newCount = newOver == null ? 0 : newOver.size();
            if (oldCount != newCount) {
                ApiChangeDto ch = new ApiChangeDto();
                ch.changeType = oldCount < newCount ? "AddedOverload" : "RemovedOverload";
                ch.meta.symbolKind = "Method";
                ch.meta.fqn = fqn;
                ch.meta.methodName = name;
                ch.category = "Overload";
                ch.impact = oldCount < newCount ? "NonBreaking" : "Breaking"; // removal is breaking
                out.add(ch);
            }
        }
    }

    private ApiChangeDto methodAdded(String fqn, MethodSymbol m) {
        ApiChangeDto ch = baseMethodChange("AddedMethod", fqn, m);
        ch.after = describeMethod(m);
        ch.impact = "NonBreaking"; // adding method typically non-breaking
        return ch;
    }

    private ApiChangeDto methodRemoved(String fqn, MethodSymbol m) {
        ApiChangeDto ch = baseMethodChange("RemovedMethod", fqn, m);
        ch.before = describeMethod(m);
        ch.impact = "Breaking";
        return ch;
    }

    private ApiChangeDto methodChange(String type, String fqn, MethodSymbol oldM, MethodSymbol newM) {
        ApiChangeDto ch = baseMethodChange(type, fqn, newM);
        ch.before = describeMethod(oldM);
        ch.after = describeMethod(newM);
        return ch;
    }

    private ApiChangeDto baseMethodChange(String type, String fqn, MethodSymbol m) {
        ApiChangeDto ch = new ApiChangeDto();
        ch.changeType = type;
        ch.meta.symbolKind = "Method";
        ch.meta.fqn = fqn;
        ch.meta.methodName = m.name;
    ch.meta.signature = m.fullSignature;
    ch.meta.returnType = m.returnType;
    ch.meta.parameterTypes = extractParamTypes(m);
    ch.meta.parameterNames = extractParamNames(m);
        ch.meta.visibility = m.visibility;
        ch.meta.deprecated = m.deprecated ? Boolean.TRUE : null;
        ch.category = inferCategory(type);
        return ch;
    }

    private ApiChangeDto classChange(String type, String before, String after, ClassSymbol cls) {
        ApiChangeDto ch = new ApiChangeDto();
        ch.changeType = type;
        ch.before = before;
        ch.after = after;
        ch.meta.symbolKind = "Class";
        ch.meta.fqn = after != null ? after : before;
    // Class visibility not currently captured; infer from modifiers
    ch.meta.visibility = cls.modifiers.contains("public") ? "public" : (cls.modifiers.contains("protected") ? "protected" : null);
        ch.meta.deprecated = cls.deprecated ? Boolean.TRUE : null;
        ch.category = "Type";
        ch.impact = type.startsWith("Removed") ? "Breaking" : "NonBreaking";
        return ch;
    }

    private ApiChangeDto fieldChange(String type, String before, String after, String fqn, FieldSymbol f) {
        ApiChangeDto ch = new ApiChangeDto();
        ch.changeType = type;
        ch.before = before;
        ch.after = after;
        ch.meta.symbolKind = "Field";
        ch.meta.fqn = fqn;
        ch.meta.fieldName = f.name;
        ch.meta.visibility = f.visibility;
        ch.meta.deprecated = f.deprecated ? Boolean.TRUE : null;
        ch.category = type.startsWith("Modified") ? "Field" : (type.startsWith("Added") ? "Field" : "Field");
        if (type.startsWith("Removed")) ch.impact = "Breaking";
        else if (type.startsWith("Added")) ch.impact = "NonBreaking";
        return ch;
    }

    private String describeMethod(MethodSymbol m) {
        StringBuilder sb = new StringBuilder();
        sb.append(m.visibility).append(" ");
    boolean isConstructor = "void".equals(m.returnType) && m.name != null && m.name.length() > 0 && m.name.equals(simpleNameFromFqn(m.fqn));
    if (isConstructor) {
            sb.append(m.name);
        } else {
            sb.append(m.returnType).append(" ").append(m.name);
        }
        sb.append("(");
        String[] pTypes = extractParamTypes(m);
        String[] pNames = extractParamNames(m);
        for (int i = 0; i < pTypes.length; i++) {
            if (i > 0) sb.append(", ");
            sb.append(pTypes[i]).append(" ").append(pNames[i]);
        }
        sb.append(")");
        return sb.toString();
    }

    private String describeField(FieldSymbol f) {
        return f.visibility + " " + f.type + " " + f.name;
    }

    private String safe(String s) { return s == null ? "" : s; }

    private String inferCategory(String changeType) {
        if (changeType.contains("ReturnType")) return "ReturnType";
        if (changeType.contains("Parameter")) return "Parameters";
        if (changeType.contains("Overload")) return "Overload";
        if (changeType.contains("Visibility")) return "Visibility";
        return "Method";
    }

    private String[] extractParamTypes(MethodSymbol m) {
        String[] arr = new String[m.params.size()];
        for (int i = 0; i < m.params.size(); i++) arr[i] = m.params.get(i).type;
        return arr;
    }

    private String[] extractParamNames(MethodSymbol m) {
        String[] arr = new String[m.params.size()];
        for (int i = 0; i < m.params.size(); i++) arr[i] = m.params.get(i).name;
        return arr;
    }

    private boolean sameParamTypes(MethodSymbol a, MethodSymbol b) {
        if (a.params.size() != b.params.size()) return false;
        for (int i = 0; i < a.params.size(); i++) {
            if (!safe(a.params.get(i).type).equals(safe(b.params.get(i).type))) return false;
        }
        return true;
    }

    private boolean sameParamNames(MethodSymbol a, MethodSymbol b) {
        if (a.params.size() != b.params.size()) return false;
        for (int i = 0; i < a.params.size(); i++) {
            if (!safe(a.params.get(i).name).equals(safe(b.params.get(i).name))) return false;
        }
        return true;
    }

    private String simpleNameFromFqn(String fqn) {
        if (fqn == null) return null;
        int idx = fqn.lastIndexOf('.');
        return idx == -1 ? fqn : fqn.substring(idx + 1);
    }
}
