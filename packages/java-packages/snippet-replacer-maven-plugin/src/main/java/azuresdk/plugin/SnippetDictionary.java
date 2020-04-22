package azuresdk.plugin;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

public class SnippetDictionary {
    private HashMap<String, List<String>> _snippetDictionary = new HashMap<String, List<String>>();

    public SnippetDictionary(){

    }

    public boolean IsActive(){
        return !_snippetDictionary.isEmpty();
    }

    public void BeginSnippet(String key) {
        if(!this._snippetDictionary.containsKey((key)))
        {
            this._snippetDictionary.put(key, new ArrayList<String>());
        }
    }

    public void ProcessLine(String line){
        for (Map.Entry<String, List<String>> entry : this._snippetDictionary.entrySet() ){
            entry.getValue().add(line);
        }
    }

    public List<String> FinalizeSnippet(String key){
        List<String> value = this._snippetDictionary.get(key);
        this._snippetDictionary.remove(key);

        return value;
    }
}
