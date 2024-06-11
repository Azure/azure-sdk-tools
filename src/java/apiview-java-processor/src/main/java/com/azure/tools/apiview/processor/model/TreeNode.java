package com.azure.tools.apiview.processor.model;

import com.azure.json.JsonSerializable;
import com.azure.json.JsonWriter;

import java.io.IOException;
import java.util.*;

import static com.azure.tools.apiview.processor.model.TokenKind.WHITESPACE;
import static com.azure.tools.apiview.processor.analysers.models.Constants.*;

public class TreeNode implements JsonSerializable<TreeNode> {

    // The name of the tree node which will be used for page navigation.
    private final String name;

    //  Id of the node, which should be unique at the node level. i.e. unique among its siblings.
    //  Also the id should be valid HTML id.
    private final String id;

    // What kind of node is it. (namespace, class, module, method e.t.c)
    private final TreeNodeKind kind;

    // for things like deprecated, hidden, etc
    private final Set<String> tags;

    // "If the node needs more specification e.g. Use SubKind entry to make the node kind more specific. Feel free to
    // push any other data that you think will be useful here, then file an issue for further implementation in APIView."
    private final Map<String, String> properties;

    // The main data of the node.
    private final List<Token> topTokens;

    // Data that closes out the node.
    private final List<Token> bottomTokens;

    // Node immediate descendants.
    private final List<TreeNode> children;

    private boolean isHideFromNavigation = false;

    // Using this to customise output based on language, flavor, etc
    private APIListing apiListing;

    public TreeNode(String name, String id, TreeNodeKind kind) {
        this.name = name;
        this.kind = Objects.requireNonNull(kind);
        this.id = Objects.requireNonNull(id);
        this.tags = new LinkedHashSet<>();
        this.properties = new LinkedHashMap<>();
        this.topTokens = new ArrayList<>();
        this.bottomTokens = new ArrayList<>();
        this.children = new ArrayList<>();

        if (kind.getSubKind() != null && !kind.getSubKind().isEmpty()) {
            addProperty(PROPERTY_SUBKIND, kind.getSubKind());
        }
    }

    public String getName() {
        return name;
    }

    public String getId() {
        return id;
    }

    public TreeNodeKind getKind() {
        return kind;
    }

    public Set<String> getTags() {
        return Collections.unmodifiableSet(tags);
    }

    public Map<String, String> getProperties() {
        return Collections.unmodifiableMap(properties);
    }

    public List<Token> getTopTokens() {
        return Collections.unmodifiableList(topTokens);
    }

    public List<Token> getBottomTokens() {
        return Collections.unmodifiableList(bottomTokens);
    }

    public List<TreeNode> getChildren() {
        return Collections.unmodifiableList(children);
    }

    // we smuggle this through all tree nodes, so that we can use it in the renderer for icon selection
    void setApiListing(APIListing apiListing) {
        this.apiListing = apiListing;
        if (kind != null && !isHideFromNavigation) {
            addProperty(PROPERTY_ICON_NAME, kind.getIconName(apiListing));
        }
        children.forEach(child -> child.setApiListing(apiListing));
    }

    public TreeNode addTag(String tag) {
        tags.add(tag);
        return this;
    }

    public TreeNode addProperty(String key, String value) {
        properties.put(key, value);
        return this;
    }

    public TreeNode addSpace() {
       return addTopToken(new Token(WHITESPACE, " "));
    }

    public TreeNode addNewline() {
        return addTopToken(new Token(TokenKind.NEW_LINE, "\n"));
    }

    public TreeNode addTopToken(TokenKind kind, String value) {
        return addTopToken(new Token(kind, value));
    }

    public TreeNode addTopToken(TokenKind kind, String value, String id) {
        return addTopToken(new Token(kind, value, id));
    }

    public TreeNode addTopToken(Token token) {
        topTokens.add(token);
        return this;
    }

    public TreeNode addBottomToken(TokenKind kind, String value) {
        return addBottomToken(new Token(kind, value));
    }

    public TreeNode addBottomToken(Token token) {
        bottomTokens.add(token);
        return this;
    }

    /**
     * Adds the given TreeNode as a child of this TreeNode, returning this TreeNode (not the child).
     * @param child The child to add
     * @return The parent of the child (i.e. this TreeNode)
     */
    public TreeNode addChild(TreeNode child) {
        children.add(child);
        child.setApiListing(apiListing);
        return this;
    }

    public TreeNode hideFromNavigation() {
        addTag(TAG_HIDE_FROM_NAVIGATION);
        this.isHideFromNavigation = true;
        this.properties.remove(PROPERTY_ICON_NAME);
        return this;
    }

    @Override
    public JsonWriter toJson(JsonWriter jsonWriter) throws IOException {
        jsonWriter.writeStartObject();

        jsonWriter.writeStringField(JSON_NAME_NAME, name)
                  .writeStringField(JSON_NAME_KIND, kind.getName());

        if (id != null) {
            jsonWriter.writeStringField(JSON_NAME_ID, id);
        }

        if (!tags.isEmpty()) {
            jsonWriter.writeArrayField(JSON_NAME_TAGS, tags, JsonWriter::writeString);
        }

        if (!properties.isEmpty()) {
            jsonWriter.writeMapField(JSON_NAME_PROPERTIES, properties, JsonWriter::writeString);
        }

        if (!topTokens.isEmpty()) {
            jsonWriter.writeArrayField(JSON_NAME_TOP_TOKENS, topTokens, JsonWriter::writeJson);
        }

        if (!children.isEmpty()) {
            jsonWriter.writeArrayField(JSON_NAME_CHILDREN, children, JsonWriter::writeJson);
        }

        if (!bottomTokens.isEmpty()) {
            jsonWriter.writeArrayField(JSON_NAME_BOTTOM_TOKENS, bottomTokens, JsonWriter::writeJson);
        }

        return jsonWriter.writeEndObject();
    }

    @Override
    public String toString() {
        return "TreeNode{" +
                "name='" + name + '\'' +
                '}';
    }
}
