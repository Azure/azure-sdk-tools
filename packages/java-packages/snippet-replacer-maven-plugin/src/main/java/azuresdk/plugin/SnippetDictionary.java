package azuresdk.plugin;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

public class SnippetDictionary {
    private HashMap<String, List<String>> _snippetDictionary = new HashMap<>();

    public boolean isActive() {
        return !_snippetDictionary.isEmpty();
    }

    public void beginSnippet(String key) {
        if (!this._snippetDictionary.containsKey((key))) {
            this._snippetDictionary.put(key, new ArrayList<>());
        }
    }

    public void processLine(String line) {
        for (Map.Entry<String, List<String>> entry : this._snippetDictionary.entrySet()) {
            entry.getValue().add(line);
        }
    }

    public List<String> finalizeSnippet(String key) {
        List<String> value = this._snippetDictionary.get(key);
        this._snippetDictionary.remove(key);

        return value;
    }
}
