package com.azure.tools.apiview.processor.analysers;

import com.azure.tools.apiview.processor.model.APIListing;
import com.azure.tools.apiview.processor.model.ChildItem;
import com.azure.tools.apiview.processor.model.Token;
import com.azure.tools.apiview.processor.model.TypeKind;

import javax.xml.stream.XMLInputFactory;
import javax.xml.stream.XMLStreamException;
import javax.xml.stream.XMLStreamReader;
import java.io.FileInputStream;
import java.io.FileNotFoundException;
import java.nio.file.Path;
import java.util.Arrays;
import java.util.List;

import static com.azure.tools.apiview.processor.model.TokenKind.COMMENT;
import static com.azure.tools.apiview.processor.model.TokenKind.KEYWORD;
import static com.azure.tools.apiview.processor.model.TokenKind.NEW_LINE;
import static com.azure.tools.apiview.processor.model.TokenKind.STRING_LITERAL;
import static com.azure.tools.apiview.processor.model.TokenKind.TEXT;
import static com.azure.tools.apiview.processor.model.TokenKind.TYPE_NAME;
import static com.azure.tools.apiview.processor.model.TokenKind.WHITESPACE;

public class XMLASTAnalyser implements Analyser {
    private final APIListing apiListing;

    private boolean isFirstElement = true;
    private Path currentFile;

    private int startElementCharOffset = -1;
    private Token lastToken = null;

    public XMLASTAnalyser(final APIListing apiListing) {
        this.apiListing = apiListing;
    }

    @Override
    public void analyse(final List<Path> allFiles) {
        allFiles.forEach(this::processFile);
    }

    private void processFile(Path file) {
        currentFile = file;

        String filename = file.toFile().getName();
        apiListing.addChildItem(new ChildItem(filename, filename, TypeKind.ASSEMBLY));

        final XMLInputFactory factory = XMLInputFactory.newInstance();
        factory.setProperty(XMLInputFactory.SUPPORT_DTD, false);

        XMLStreamReader reader = null;
        try {
            reader = factory.createXMLStreamReader(new FileInputStream(file.toFile()));

            while (reader.hasNext()) {
                final int eventType = reader.next();
                switch (eventType) {
                    case XMLStreamReader.DTD: {
                        System.out.println("IGNORING DTD " + reader.getText());
                        break;
                    }

                    case XMLStreamReader.PROCESSING_INSTRUCTION: {
                        System.out.println("IGNORING PROCESSING_INSTRUCTION " + reader.getText());
                        break;
                    }

                    case XMLStreamReader.NOTATION_DECLARATION: {
                        System.out.println("IGNORING NOTATION_DECLARATION " + reader.getText());
                        break;
                    }

                    case XMLStreamReader.NAMESPACE: {
                        System.out.println("IGNORING NAMESPACE " + reader.getText());
                        break;
                    }

                    case XMLStreamReader.ENTITY_DECLARATION: {
                        System.out.println("IGNORING ENTITY_DECLARATION " + reader.getText());
                        break;
                    }

                    case XMLStreamReader.START_DOCUMENT: {
                        System.out.println("IGNORING START_DOCUMENT " + reader.getText());
                        break;
                    }

                    case XMLStreamReader.END_DOCUMENT: {
                        addNewLine();
                        break;
                    }

                    case XMLStreamReader.START_ELEMENT: {
                        startElement(reader);
                        break;
                    }
                    case XMLStreamReader.END_ELEMENT: {
                        endElement(reader);
                        break;
                    }

                    case XMLStreamReader.ATTRIBUTE: {
                        System.out.println("IGNORING ATTRIBUTE " + reader.getText());
                        break;
                    }

                    case XMLStreamReader.CDATA: {
                        System.out.println("IGNORING CDATA " + reader.getText());
                        break;
                    }

                    case XMLStreamReader.COMMENT: {
                        addComment("<!--" + reader.getText() + "-->");
                        break;
                    }

                    case XMLStreamReader.SPACE: {
                        System.out.println("IGNORING SPACE " + reader.getText());
                        break;
                    }

                    case XMLStreamReader.CHARACTERS: {
                        addCharacters(reader);
                        break;
                    }
                }
            }
        } catch (XMLStreamException | FileNotFoundException e) {
            e.printStackTrace();
        } finally {
            if (reader != null) {
                try {
                    reader.close();
                } catch (XMLStreamException e) {
                    e.printStackTrace();
                }
            }
        }
    }

    private void startElement(final XMLStreamReader reader) {
        startElementCharOffset = reader.getLocation().getCharacterOffset();

        final int attributeCount = reader.getAttributeCount();
        final int namespaceCount = reader.getNamespaceCount();

        final String elementName = reader.getLocalName();
        final String definitionId = elementName + "/" + reader.getLocation().getLineNumber();

        final String startElementString = attributeCount == 0 && namespaceCount == 0 ? "<" + elementName + ">" : "<" + elementName + " ";
        final Token startElementToken = new Token(KEYWORD, startElementString, definitionId);

        if (isFirstElement) {
            isFirstElement = false;
            startElementToken.setNavigateToId(currentFile.toFile().getName());
        }

        addToken(startElementToken);

        for (int i = 0; i < namespaceCount; i++) {
            String key = reader.getNamespacePrefix(i);
            key = key == null ? "xmlns" : "xmlns:" + key;

            final String value = reader.getNamespaceURI(i);

            addAttribute(key, value);

            if (i < namespaceCount - 1 || attributeCount > 0) {
                addToken(new Token(WHITESPACE, " "));
            }
        }

        if (attributeCount > 0) {
            for (int i = 0; i < attributeCount; i++) {
                final String prefix = reader.getAttributePrefix(i);
                final String key = (prefix == "" ? "" : prefix + ":")  + reader.getAttributeLocalName(i);
                final String value = reader.getAttributeValue(i);

                addAttribute(key, value);

                if (i < attributeCount - 1) {
                    addToken(new Token(WHITESPACE, " "));
                }
            }

            addToken(new Token(KEYWORD, ">"));
        }
    }

    private void endElement(final XMLStreamReader reader) {
        final String elementName = reader.getLocalName();
        final String definitionId = elementName + "/" + reader.getLocation().getLineNumber();

        if (lastToken != null && reader.getLocation().getCharacterOffset() == startElementCharOffset) {
            // This element ends at the same place it starts, so it is a self-closing element, and we should try
            // to retrofit in the self-closing '/>' into the previous token to more closely emulate the input file.
            final String lastTokenValue = lastToken.getValue();
            final String newValue = (lastTokenValue.endsWith(">")
                 ? lastTokenValue.substring(0, lastTokenValue.length() - 1)
                 : lastTokenValue)
                 + " />";
            lastToken.setValue(newValue);
        } else {
            addToken(new Token(KEYWORD, "</" + elementName + ">", definitionId));
        }
    }

    private void addAttribute(String key, String value) {
        addToken(new Token(TYPE_NAME, key));
        addToken(new Token(STRING_LITERAL, "=\"" + value + "\""));
    }

    private void addCharacters(XMLStreamReader reader) {
        final String characters = reader.getText();

        int head = 0;
        int tail = 0;
        for (int i = 0; i < characters.length(); i++) {
            final char ch = characters.charAt(i);
            if (ch == '\n') {
                if (tail > head) {
                    final String substring = characters.substring(head, tail);
                    if (substring.trim().isEmpty()) {
                        addToken(new Token(WHITESPACE, substring));
                    } else {
                        addToken(new Token(TEXT, substring));
                    }
                }

                // fixing issue in APIView where you can't have consecutive newlines.
                addToken(new Token(WHITESPACE, " "));

                addNewLine();
                head = i + 1;
                tail = i + 1;
            } else {
                tail++;
            }
        }

        if (tail > head) {
            addToken(new Token(TEXT, characters.substring(head, tail)));
        }
    }

    private void addComment(String comment) {
        addToken(new Token(COMMENT, comment));
    }

    private void addNewLine() {
        addToken(new Token(NEW_LINE));
    }

    private void addToken(Token token) {
        apiListing.getTokens().add(token);
        lastToken = token;
    }
}