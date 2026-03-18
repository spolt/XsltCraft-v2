import Editor from "@monaco-editor/react"
import { useRef, useEffect } from "react"

export type XsltError = {
  message: string
  line: number
  column: number
}

type Props = {
  value: string
  onChange: (value: string) => void
  onEditorReady?: (goTo: (term: string) => void) => void
  errors?: XsltError[]
}

/* ── XSLT 1.0 element definitions ── */
const xsltElements: { label: string; detail: string; snippet: string }[] = [
  { label: "xsl:template",          detail: "Define a template rule",              snippet: 'xsl:template match="$1">\n\t$0\n</xsl:template>' },
  { label: "xsl:apply-templates",   detail: "Apply templates to children",         snippet: 'xsl:apply-templates select="$1"/>' },
  { label: "xsl:call-template",     detail: "Call a named template",               snippet: 'xsl:call-template name="$1">\n\t$0\n</xsl:call-template>' },
  { label: "xsl:value-of",          detail: "Output value of an expression",       snippet: 'xsl:value-of select="$1"/>' },
  { label: "xsl:for-each",          detail: "Loop over a node-set",                snippet: 'xsl:for-each select="$1">\n\t$0\n</xsl:for-each>' },
  { label: "xsl:if",                detail: "Conditional processing",              snippet: 'xsl:if test="$1">\n\t$0\n</xsl:if>' },
  { label: "xsl:choose",            detail: "Multi-branch conditional",            snippet: 'xsl:choose>\n\t<xsl:when test="$1">\n\t\t$0\n\t</xsl:when>\n\t<xsl:otherwise>\n\t</xsl:otherwise>\n</xsl:choose>' },
  { label: "xsl:when",              detail: "Branch inside choose",                snippet: 'xsl:when test="$1">\n\t$0\n</xsl:when>' },
  { label: "xsl:otherwise",         detail: "Default branch inside choose",        snippet: 'xsl:otherwise>\n\t$0\n</xsl:otherwise>' },
  { label: "xsl:variable",          detail: "Declare a variable",                  snippet: 'xsl:variable name="$1" select="$2"/>' },
  { label: "xsl:param",             detail: "Declare a parameter",                 snippet: 'xsl:param name="$1" select="$2"/>' },
  { label: "xsl:with-param",        detail: "Pass parameter to template",          snippet: 'xsl:with-param name="$1" select="$2"/>' },
  { label: "xsl:sort",              detail: "Sort nodes in for-each/apply",        snippet: 'xsl:sort select="$1" order="${2:ascending}"/>' },
  { label: "xsl:copy",              detail: "Copy current node",                   snippet: 'xsl:copy>\n\t$0\n</xsl:copy>' },
  { label: "xsl:copy-of",           detail: "Deep copy of nodes",                  snippet: 'xsl:copy-of select="$1"/>' },
  { label: "xsl:text",              detail: "Output literal text",                 snippet: 'xsl:text>$1</xsl:text>' },
  { label: "xsl:element",           detail: "Create element dynamically",          snippet: 'xsl:element name="$1">\n\t$0\n</xsl:element>' },
  { label: "xsl:attribute",         detail: "Create attribute dynamically",        snippet: 'xsl:attribute name="$1">$2</xsl:attribute>' },
  { label: "xsl:attribute-set",     detail: "Define reusable attribute set",       snippet: 'xsl:attribute-set name="$1">\n\t$0\n</xsl:attribute-set>' },
  { label: "xsl:comment",           detail: "Output XML comment",                  snippet: 'xsl:comment>$1</xsl:comment>' },
  { label: "xsl:processing-instruction", detail: "Output processing instruction",  snippet: 'xsl:processing-instruction name="$1">$2</xsl:processing-instruction>' },
  { label: "xsl:number",            detail: "Insert formatted number",             snippet: 'xsl:number value="$1" format="${2:1}"/>' },
  { label: "xsl:output",            detail: "Control output format",               snippet: 'xsl:output method="${1:html}" encoding="${2:UTF-8}" indent="${3:yes}"/>' },
  { label: "xsl:stylesheet",        detail: "Root stylesheet element",             snippet: 'xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">\n\t$0\n</xsl:stylesheet>' },
  { label: "xsl:import",            detail: "Import another stylesheet",           snippet: 'xsl:import href="$1"/>' },
  { label: "xsl:include",           detail: "Include another stylesheet",          snippet: 'xsl:include href="$1"/>' },
  { label: "xsl:key",               detail: "Declare a key for lookup",            snippet: 'xsl:key name="$1" match="$2" use="$3"/>' },
  { label: "xsl:decimal-format",    detail: "Define decimal format",               snippet: 'xsl:decimal-format name="$1" decimal-separator="${2:.}" grouping-separator="${3:,}"/>' },
  { label: "xsl:namespace-alias",   detail: "Alias namespace in output",           snippet: 'xsl:namespace-alias stylesheet-prefix="$1" result-prefix="$2"/>' },
  { label: "xsl:preserve-space",    detail: "Preserve whitespace for elements",    snippet: 'xsl:preserve-space elements="$1"/>' },
  { label: "xsl:strip-space",       detail: "Strip whitespace from elements",      snippet: 'xsl:strip-space elements="$1"/>' },
  { label: "xsl:message",           detail: "Output a message (debug)",            snippet: 'xsl:message>$1</xsl:message>' },
  { label: "xsl:fallback",          detail: "Fallback for unsupported features",   snippet: 'xsl:fallback>$1</xsl:fallback>' },
]

/* ── XPath 1.0 functions ── */
const xpathFunctions: { label: string; detail: string; snippet: string }[] = [
  // String
  { label: "concat",           detail: "Concatenate strings",            snippet: "concat($1, $2)" },
  { label: "contains",         detail: "Test if string contains another",snippet: "contains($1, '$2')" },
  { label: "starts-with",      detail: "Test if string starts with",     snippet: "starts-with($1, '$2')" },
  { label: "substring",        detail: "Extract substring",              snippet: "substring($1, $2, $3)" },
  { label: "substring-before", detail: "String before first occurrence", snippet: "substring-before($1, '$2')" },
  { label: "substring-after",  detail: "String after first occurrence",  snippet: "substring-after($1, '$2')" },
  { label: "string-length",    detail: "Length of string",               snippet: "string-length($1)" },
  { label: "normalize-space",  detail: "Trim and collapse whitespace",   snippet: "normalize-space($1)" },
  { label: "translate",        detail: "Character-by-character replace",  snippet: "translate($1, '$2', '$3')" },
  { label: "string",           detail: "Convert to string",              snippet: "string($1)" },
  // Number
  { label: "number",           detail: "Convert to number",              snippet: "number($1)" },
  { label: "sum",              detail: "Sum of numeric values",           snippet: "sum($1)" },
  { label: "floor",            detail: "Round down",                      snippet: "floor($1)" },
  { label: "ceiling",          detail: "Round up",                        snippet: "ceiling($1)" },
  { label: "round",            detail: "Round to nearest integer",        snippet: "round($1)" },
  { label: "format-number",    detail: "Format number with pattern",      snippet: "format-number($1, '$2')" },
  // Boolean
  { label: "not",              detail: "Negate boolean",                  snippet: "not($1)" },
  { label: "true",             detail: "Boolean true",                    snippet: "true()" },
  { label: "false",            detail: "Boolean false",                   snippet: "false()" },
  { label: "boolean",          detail: "Convert to boolean",              snippet: "boolean($1)" },
  // Node-set
  { label: "count",            detail: "Count nodes",                     snippet: "count($1)" },
  { label: "position",         detail: "Current position in context",     snippet: "position()" },
  { label: "last",             detail: "Last position in context",        snippet: "last()" },
  { label: "name",             detail: "Name of node",                    snippet: "name($1)" },
  { label: "local-name",       detail: "Local name without prefix",       snippet: "local-name($1)" },
  { label: "namespace-uri",    detail: "Namespace URI of node",           snippet: "namespace-uri($1)" },
  { label: "generate-id",      detail: "Generate unique ID for node",     snippet: "generate-id($1)" },
  { label: "document",         detail: "Load external XML document",      snippet: "document('$1')" },
  { label: "key",              detail: "Lookup nodes by key",             snippet: "key('$1', $2)" },
  { label: "current",          detail: "Current context node",            snippet: "current()" },
]

let completionRegistered = false

function registerXsltCompletions(monaco: any) {
  if (completionRegistered) return
  completionRegistered = true

  const CompletionItemKind = monaco.languages.CompletionItemKind
  const CompletionItemInsertTextRule = monaco.languages.CompletionItemInsertTextRule

  monaco.languages.registerCompletionItemProvider("xml", {
    triggerCharacters: ["<", ":"],

    provideCompletionItems(model: any, position: any) {
      const wordInfo = model.getWordUntilPosition(position)
      const lineContent = model.getLineContent(position.lineNumber)
      const textBefore = lineContent.substring(0, position.column - 1)
      const textAfter = lineContent.substring(position.column - 1)

      // Find what the user actually typed: could be "xsl:if", "xsl:", "if", etc.
      // Monaco splits at ":" so we scan backwards manually
      const prefixMatch = textBefore.match(/<?(xsl:?\w*)$/)
      const typedPrefix = prefixMatch ? prefixMatch[1] : wordInfo.word

      // Calculate the start column of just the "xsl:..." part (after the "<")
      const prefixStartCol = prefixMatch
        ? position.column - prefixMatch[1].length
        : wordInfo.startColumn

      // Check if there's a trailing ">" right after cursor (from auto-close)
      const hasTrailingBracket = textAfter.startsWith(">")

      // Range that replaces the typed prefix (xsl:if) — keeps the "<" the user typed
      const replaceRange = {
        startLineNumber: position.lineNumber,
        endLineNumber: position.lineNumber,
        startColumn: prefixStartCol,
        endColumn: hasTrailingBracket ? position.column + 1 : position.column,
      }

      const wordRange = {
        startLineNumber: position.lineNumber,
        endLineNumber: position.lineNumber,
        startColumn: wordInfo.startColumn,
        endColumn: wordInfo.endColumn,
      }

      const suggestions: any[] = []

      // Inside attribute value (select="...", test="...", match="...") → XPath functions
      const inAttrValue = /(?:select|test|match|use)\s*=\s*"[^"]*$/.test(textBefore)
      if (inAttrValue) {
        for (const fn of xpathFunctions) {
          suggestions.push({
            label: fn.label,
            kind: CompletionItemKind.Function,
            detail: fn.detail,
            insertText: fn.snippet,
            insertTextRules: CompletionItemInsertTextRule.InsertAsSnippet,
            range: wordRange,
          })
        }
        return { suggestions }
      }

      // Only show XSLT elements when user is typing a tag (after "<")
      const isTypingTag = /<\s*\w*:?\w*$/.test(textBefore)
      if (isTypingTag) {
        for (const el of xsltElements) {
          // Filter: only show elements matching what user typed so far
          if (typedPrefix && !el.label.startsWith(typedPrefix) && !el.label.includes(typedPrefix)) continue

          suggestions.push({
            label: el.label,
            kind: CompletionItemKind.Snippet,
            detail: el.detail,
            insertText: el.snippet,
            insertTextRules: CompletionItemInsertTextRule.InsertAsSnippet,
            range: replaceRange,
            filterText: el.label,
            sortText: `0_${el.label}`,
          })
        }
      }

      // XPath functions — only show when NOT typing a tag
      if (!isTypingTag && wordInfo.word.length > 0) {
        for (const fn of xpathFunctions) {
          suggestions.push({
            label: fn.label,
            kind: CompletionItemKind.Function,
            detail: `XPath: ${fn.detail}`,
            insertText: fn.snippet,
            insertTextRules: CompletionItemInsertTextRule.InsertAsSnippet,
            range: wordRange,
            sortText: `1_${fn.label}`,
          })
        }
      }

      return { suggestions }
    },
  })
}

export default function XsltEditor({ value, onChange, onEditorReady, errors }: Props) {

  const monacoRef = useRef<any>(null)
  const editorRef = useRef<any>(null)

  function handleBeforeMount(monaco: any) {
    monacoRef.current = monaco
    registerXsltCompletions(monaco)
  }

  function handleMount(editor: any) {
    editorRef.current = editor

    if (onEditorReady) {
      onEditorReady((term: string) => {
        const model = editor.getModel()
        if (!model) return

        const matches = model.findMatches(
          term,
          false,
          false,
          false,
          null,
          false
        )

        if (matches.length > 0) {
          const { startLineNumber, startColumn } = matches[0].range
          editor.revealLineInCenter(startLineNumber)
          editor.setPosition({ lineNumber: startLineNumber, column: startColumn })
          editor.focus()
        }
      })
    }
  }

  // Set/clear Monaco error markers when errors change
  useEffect(() => {
    const monaco = monacoRef.current
    const editor = editorRef.current
    if (!monaco || !editor) return

    const model = editor.getModel()
    if (!model) return

    if (!errors || errors.length === 0) {
      monaco.editor.setModelMarkers(model, "xslt", [])
      return
    }

    const markers = errors.map((err) => ({
      severity: monaco.MarkerSeverity.Error,
      message: err.message,
      startLineNumber: err.line || 1,
      startColumn: err.column || 1,
      endLineNumber: err.line || 1,
      endColumn: (err.column || 1) + 20,
    }))

    monaco.editor.setModelMarkers(model, "xslt", markers)
  }, [errors])

  return (
    <Editor
      height="100%"
      language="xml"
      theme="vs-dark"
      value={value}
      onChange={(v) => onChange(v || "")}
      beforeMount={handleBeforeMount}
      onMount={handleMount}
      options={{
        quickSuggestions: true,
        suggestOnTriggerCharacters: true,
        wordBasedSuggestions: "currentDocument",
        snippetSuggestions: "top",
        minimap: { enabled: false },
        fontSize: 14,
        tabSize: 2,
        formatOnPaste: true,
        autoClosingBrackets: "never",
      }}
    />
  )
}
