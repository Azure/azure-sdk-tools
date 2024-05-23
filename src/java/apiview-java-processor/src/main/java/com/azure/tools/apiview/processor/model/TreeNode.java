package com.azure.tools.apiview.processor.model;

import com.azure.json.JsonSerializable;
import com.azure.json.JsonWriter;

import java.io.IOException;
import java.util.*;

import static com.azure.tools.apiview.processor.model.TokenKind.WHITESPACE;

public class TreeNode implements JsonSerializable<TreeNode> {
    private static final String TAG_HIDE_FROM_NAVIGATION = "HideFromNavigation";
    private static final String PROPERTY_ICON_NAME = "IconName";
    private static final String PROPERTY_SUBKIND = "SubKind";

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

    /**
     * Used for things we don't want to show in the left navigation,
     * and for which we don't want to attach any JavaDoc.
     */
    public static TreeNode createHiddenNode() {
        return new TreeNode(null, null, TreeNodeKind.UNKNOWN).hideFromNavigation();
    }

    public TreeNode(String name, String id, TreeNodeKind kind) {
        this.name = name;
        this.kind = Objects.requireNonNull(kind);
        this.id = id;
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

    public void addTag(String tag) {
        tags.add(tag);
    }

    public void addProperty(String key, String value) {
        properties.put(key, value);
    }

    public TreeNode addSpace() {
        topTokens.add(new Token(WHITESPACE, " "));
        return this;
    }

    public TreeNode addNewline() {
        topTokens.add(new Token(TokenKind.NEW_LINE, "\n"));
        return this;
    }

    public TreeNode addTopToken(TokenKind kind, String value) {
        topTokens.add(new Token(kind, value));
        return this;
    }

    public TreeNode addTopToken(TokenKind kind, String value, String id) {
        topTokens.add(new Token(kind, value, id));
        return this;
    }

    public TreeNode addTopToken(Token token) {
        topTokens.add(token);
        return this;
    }

    public TreeNode addBottomToken(TokenKind kind, String value) {
        bottomTokens.add(new Token(kind, value));
        return this;
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

        jsonWriter.writeStringField("Name", name)
                  .writeStringField("Kind", kind.getName());

        if (id != null) {
            jsonWriter.writeStringField("Id", id);
        }

        if (tags != null && !tags.isEmpty()) {
            jsonWriter.writeArrayField("Tags", tags, JsonWriter::writeString);
        }

        if (properties != null && !properties.isEmpty()) {
            jsonWriter.writeMapField("Properties", properties, JsonWriter::writeString);
        }

        if (topTokens != null && !topTokens.isEmpty()) {
            jsonWriter.writeArrayField("TopTokens", topTokens, JsonWriter::writeJson);
        }

        if (children != null && !children.isEmpty()) {
            jsonWriter.writeArrayField("Children", children, JsonWriter::writeJson);
        }

        if (bottomTokens != null && !bottomTokens.isEmpty()) {
            jsonWriter.writeArrayField("BottomTokens", bottomTokens, JsonWriter::writeJson);
        }

        return jsonWriter.writeEndObject();
    }

    @Override
    public String toString() {
        return "TreeNode{" +
                "name='" + name + '\'' +
                ", id='" + id + '\'' +
                ", kind=" + kind +
                ", tags=" + tags +
                ", properties=" + properties +
                '}';
    }
}
