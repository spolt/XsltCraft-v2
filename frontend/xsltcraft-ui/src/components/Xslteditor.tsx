import Editor, { type Monaco } from "@monaco-editor/react"
import type { editor as MonacoEditor, languages as MonacoLanguages } from "monaco-editor"
import { useRef, useEffect } from "react"
import xmlFormatter from 'xml-formatter'

export type XsltError = {
  message: string
  line: number
  column: number
}

type EditorFns = {
  goTo: (term: string) => void
  insertTextAtLine: (lineNumber: number, text: string) => void
  toggleComment: () => void
  formatDocument: () => void
}

type Props = {
  value: string
  onChange: (value: string) => void
  onEditorReady?: (fns: EditorFns) => void
  onRequestImageInsert?: (lineNumber: number) => void
  errors?: XsltError[]
  options?: MonacoEditor.IStandaloneEditorConstructionOptions
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

/* ── Common HTML elements for XSLT output ── */
const htmlElements: { label: string; detail: string; snippet: string }[] = [
  // Table
  { label: "table",    detail: "HTML table",                    snippet: 'table>\n\t$0\n</table>'    },
  { label: "thead",    detail: "Table header group",            snippet: 'thead>\n\t$0\n</thead>'    },
  { label: "tbody",    detail: "Table body group",              snippet: 'tbody>\n\t$0\n</tbody>'    },
  { label: "tfoot",    detail: "Table footer group",            snippet: 'tfoot>\n\t$0\n</tfoot>'    },
  { label: "tr",       detail: "Table row",                     snippet: 'tr>\n\t$0\n</tr>'          },
  { label: "td",       detail: "Table data cell",               snippet: 'td>$1</td>'                },
  { label: "th",       detail: "Table header cell",             snippet: 'th>$1</th>'                },
  { label: "colgroup", detail: "Group of table columns",        snippet: 'colgroup>\n\t$0\n</colgroup>' },
  { label: "col",      detail: "Table column (self-closing)",   snippet: 'col style="$1"/>'          },
  // Block
  { label: "div",      detail: "Generic block container",       snippet: 'div>$1</div>'              },
  { label: "p",        detail: "Paragraph",                     snippet: 'p>$1</p>'                  },
  { label: "span",     detail: "Inline container",              snippet: 'span>$1</span>'            },
  { label: "section",  detail: "Document section",              snippet: 'section>\n\t$0\n</section>'},
  { label: "header",   detail: "Page/section header",           snippet: 'header>\n\t$0\n</header>'  },
  { label: "footer",   detail: "Page/section footer",           snippet: 'footer>\n\t$0\n</footer>'  },
  { label: "main",     detail: "Main content area",             snippet: 'main>\n\t$0\n</main>'      },
  { label: "article",  detail: "Self-contained content",        snippet: 'article>\n\t$0\n</article>'},
  { label: "aside",    detail: "Sidebar / tangential content",  snippet: 'aside>\n\t$0\n</aside>'    },
  // Headings
  { label: "h1",       detail: "Heading level 1",               snippet: 'h1>$1</h1>'                },
  { label: "h2",       detail: "Heading level 2",               snippet: 'h2>$1</h2>'                },
  { label: "h3",       detail: "Heading level 3",               snippet: 'h3>$1</h3>'                },
  { label: "h4",       detail: "Heading level 4",               snippet: 'h4>$1</h4>'                },
  { label: "h5",       detail: "Heading level 5",               snippet: 'h5>$1</h5>'                },
  { label: "h6",       detail: "Heading level 6",               snippet: 'h6>$1</h6>'                },
  // Lists
  { label: "ul",       detail: "Unordered list",                snippet: 'ul>\n\t$0\n</ul>'          },
  { label: "ol",       detail: "Ordered list",                  snippet: 'ol>\n\t$0\n</ol>'          },
  { label: "li",       detail: "List item",                     snippet: 'li>$1</li>'                },
  // Text / inline
  { label: "strong",   detail: "Bold / important text",         snippet: 'strong>$1</strong>'        },
  { label: "em",       detail: "Italic / emphasised text",      snippet: 'em>$1</em>'                },
  { label: "b",        detail: "Bold text",                     snippet: 'b>$1</b>'                  },
  { label: "i",        detail: "Italic text",                   snippet: 'i>$1</i>'                  },
  { label: "u",        detail: "Underlined text",               snippet: 'u>$1</u>'                  },
  { label: "small",    detail: "Small / fine-print text",       snippet: 'small>$1</small>'          },
  { label: "sub",      detail: "Subscript text",                snippet: 'sub>$1</sub>'              },
  { label: "sup",      detail: "Superscript text",              snippet: 'sup>$1</sup>'              },
  { label: "pre",      detail: "Preformatted text",             snippet: 'pre>$1</pre>'              },
  { label: "code",     detail: "Inline code",                   snippet: 'code>$1</code>'            },
  // Links / media (self-closing handled by auto-close suppression)
  { label: "a",        detail: "Hyperlink",                     snippet: 'a href="$1">$2</a>'        },
  { label: "img",      detail: "Image (self-closing)",          snippet: 'img src="$1" alt="$2"/>'   },
  { label: "br",       detail: "Line break (self-closing)",     snippet: 'br/>'                      },
  { label: "hr",       detail: "Horizontal rule (self-closing)",snippet: 'hr/>'                      },
  // Forms
  { label: "form",     detail: "Form element",                  snippet: 'form action="$1" method="${2:post}">\n\t$0\n</form>' },
  { label: "input",    detail: "Input field (self-closing)",    snippet: 'input type="${1:text}" name="$2"/>' },
  { label: "label",    detail: "Form label",                    snippet: 'label for="$1">$2</label>' },
  { label: "select",   detail: "Dropdown list",                 snippet: 'select name="$1">\n\t$0\n</select>' },
  { label: "option",   detail: "Dropdown option",               snippet: 'option value="$1">$2</option>' },
  { label: "textarea", detail: "Multi-line text input",         snippet: 'textarea name="$1">$2</textarea>' },
  { label: "button",   detail: "Button element",                snippet: 'button type="${1:button}">$2</button>' },
  // Style / script
  { label: "style",    detail: "Inline CSS styles",             snippet: 'style type="text/css">\n\t$0\n</style>' },
  { label: "script",   detail: "Inline JavaScript",             snippet: 'script type="text/javascript">\n\t$0\n</script>' },
  { label: "link",     detail: "External stylesheet (self-closing)", snippet: 'link rel="stylesheet" href="$1"/>' },
  { label: "meta",     detail: "Metadata (self-closing)",       snippet: 'meta name="$1" content="$2"/>' },
  // Document structure
  { label: "html",     detail: "HTML root element",             snippet: 'html>\n\t$0\n</html>'      },
  { label: "head",     detail: "Document head",                 snippet: 'head>\n\t$0\n</head>'      },
  { label: "body",     detail: "Document body",                 snippet: 'body>\n\t$0\n</body>'      },
  { label: "title",    detail: "Document title",                snippet: 'title>$1</title>'          },
]

let completionRegistered = false
let foldingProviderRegistered = false

function registerXsltCompletions(monaco: Monaco) {
  if (completionRegistered) return
  completionRegistered = true

  if (!foldingProviderRegistered) {
    foldingProviderRegistered = true

    monaco.languages.registerFoldingRangeProvider('xml', {
      provideFoldingRanges(model: MonacoEditor.ITextModel) {
        const lineCount = model.getLineCount()
        const ranges: MonacoLanguages.FoldingRange[] = []
        const stack: { tag: string; line: number }[] = []

        for (let i = 1; i <= lineCount; i++) {
          const line = model.getLineContent(i)
          const trimmed = line.trim()

          // XML comments: <!-- ... --> (multi-line)
          if (trimmed.startsWith('<!--') && !trimmed.endsWith('-->')) {
            stack.push({ tag: '!--', line: i })
            continue
          }
          if (trimmed.endsWith('-->') && stack.length > 0 && stack[stack.length - 1].tag === '!--') {
            const start = stack.pop()!.line
            if (i - start > 0) ranges.push({ start, end: i })
            continue
          }
          if (trimmed.startsWith('<!--') || trimmed.startsWith('<?') || trimmed.startsWith('?>')) continue

          // Closing tag: </tag>
          const closeMatch = trimmed.match(/^<\/([a-zA-Z][a-zA-Z0-9:._-]*)/)
          if (closeMatch) {
            const tag = closeMatch[1]
            for (let j = stack.length - 1; j >= 0; j--) {
              if (stack[j].tag === tag) {
                const start = stack[j].line
                if (i - start > 0) ranges.push({ start, end: i - 1 })
                stack.splice(j, 1)
                break
              }
            }
            continue
          }

          // Opening tag (skip self-closing and same-line close)
          const openMatch = trimmed.match(/^<([a-zA-Z][a-zA-Z0-9:._-]*)/)
          if (openMatch) {
            const tag = openMatch[1]
            const afterTag = trimmed.slice(openMatch[0].length)
            const isSelfClosing = afterTag.includes('/>') ||
              trimmed.includes(`</${tag}>`)
            if (!isSelfClosing) {
              stack.push({ tag, line: i })
            }
          }
        }

        return ranges
      },
    })
  }

  const CompletionItemKind = monaco.languages.CompletionItemKind
  const CompletionItemInsertTextRule = monaco.languages.CompletionItemInsertTextRule

  monaco.languages.registerCompletionItemProvider("xml", {
    triggerCharacters: ["<", ":"],

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
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

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
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

      // Only show XSLT/HTML elements when user is typing a tag (after "<")
      const isTypingTag = /<\s*\w*:?\w*$/.test(textBefore)
      if (isTypingTag) {
        // XSLT elements (xsl:*)
        for (const el of xsltElements) {
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

        // HTML elements — use wordRange (replaces only the word, not the "<")
        const htmlWordRange = {
          startLineNumber: position.lineNumber,
          endLineNumber: position.lineNumber,
          startColumn: wordInfo.startColumn,
          endColumn: hasTrailingBracket ? position.column + 1 : position.column,
        }
        for (const el of htmlElements) {
          if (typedPrefix && !el.label.startsWith(typedPrefix) && !el.label.includes(typedPrefix)) continue

          suggestions.push({
            label: el.label,
            kind: CompletionItemKind.Snippet,
            detail: el.detail,
            insertText: el.snippet,
            insertTextRules: CompletionItemInsertTextRule.InsertAsSnippet,
            range: htmlWordRange,
            filterText: el.label,
            sortText: `1_${el.label}`,
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

export default function XsltEditor({ value, onChange, onEditorReady, onRequestImageInsert, errors, options }: Props) {

  const monacoRef = useRef<Monaco | null>(null)
  const editorRef = useRef<MonacoEditor.IStandaloneCodeEditor | null>(null)

  function handleBeforeMount(monaco: Monaco) {
    monacoRef.current = monaco
    registerXsltCompletions(monaco)
  }

  function handleMount(editor: MonacoEditor.IStandaloneCodeEditor) {
    editorRef.current = editor

    // Context menu: Resim Ekle
    editor.addAction({
      id: 'xslt-insert-image',
      label: 'Resim Ekle',
      contextMenuGroupId: 'modification',
      contextMenuOrder: 1,
      run(ed: MonacoEditor.ICodeEditor) {
        const pos = ed.getPosition()
        if (pos && onRequestImageInsert) {
          onRequestImageInsert(pos.lineNumber)
        }
      },
    })

    // Auto-close XML tags: <td> → <td>|</td>
    const SELF_CLOSING = new Set([
      'area','base','br','col','embed','hr','img','input','link','meta',
      'param','source','track','wbr',
    ])

    editor.onDidChangeModelContent((event: MonacoEditor.IModelContentChangedEvent) => {
      const change = event.changes[0]
      if (!change || change.text !== '>') return

      const monaco = monacoRef.current
      const model = editor.getModel()
      const position = editor.getPosition()
      if (!model || !position || !monaco) return

      // Get text from line start to cursor to extract opening tag
      const lineContent = model.getLineContent(position.lineNumber)
      const textUpToCursor = lineContent.substring(0, position.column - 1)

      // Match last opening tag: handles namespaces (xsl:for-each) and attributes
      const tagMatch = textUpToCursor.match(/<([a-zA-Z][a-zA-Z0-9:._-]*)(?:\s[^>]*)?$/)
      if (!tagMatch) return

      const tagName = tagMatch[1]

      // Skip self-closing tags, processing instructions and already self-closed tags
      if (SELF_CLOSING.has(tagName.toLowerCase())) return
      if (textUpToCursor.trimEnd().endsWith('/')) return  // user typed />
      if (tagName.startsWith('?') || tagName.startsWith('!')) return

      const closingTag = `</${tagName}>`
      const insertAt = { lineNumber: position.lineNumber, column: position.column }

      editor.executeEdits('auto-close-tag', [{
        range: new monaco.Range(insertAt.lineNumber, insertAt.column, insertAt.lineNumber, insertAt.column),
        text: closingTag,
      }])

      // Place cursor between opening and closing tags
      editor.setPosition(insertAt)
    })

    function toggleComment() {
      const monaco = monacoRef.current
      const model = editor.getModel()
      const selection = editor.getSelection()
      if (!model || !selection || !monaco) return

      const start = selection.startLineNumber
      const end   = selection.endLineNumber

      const lineNums = Array.from({ length: end - start + 1 }, (_, i) => start + i)
      const allCommented = lineNums.every(n => /^\s*<!--.*-->\s*$/.test(model.getLineContent(n)))

      const edits = lineNums.map((lineNo) => {
        const content = model.getLineContent(lineNo)
        const text = allCommented
          ? content.replace(/^(\s*)<!--\s?/, '$1').replace(/\s?-->\s*$/, '')
          : `<!-- ${content} -->`
        return {
          range: new monaco.Range(lineNo, 1, lineNo, content.length + 1),
          text,
        }
      })

      editor.executeEdits('toggle-comment', edits)
      editor.focus()
    }

    editor.addAction({
      id: 'xslt-toggle-comment',
      label: 'Yorum Satırı Ekle / Kaldır',
      contextMenuGroupId: 'modification',
      contextMenuOrder: 2,
      keybindings: [monacoRef.current!.KeyMod.CtrlCmd | monacoRef.current!.KeyMod.Shift | monacoRef.current!.KeyCode.KeyC],
      run: toggleComment,
    })

    editor.addAction({
      id: 'xslt-format-document',
      label: 'Belgeyi Biçimlendir',
      contextMenuGroupId: 'modification',
      contextMenuOrder: 3,
      keybindings: [
        monacoRef.current!.KeyMod.Shift |
        monacoRef.current!.KeyMod.Alt |
        monacoRef.current!.KeyCode.KeyF,
      ],
      run(ed) {
        const model = ed.getModel()
        if (!model) return
        try {
          const formatted = xmlFormatter(model.getValue(), {
            indentation: '  ',
            collapseContent: true,
            lineSeparator: '\n',
            whiteSpaceAtEndOfSelfclosingTag: true,
            forceSelfClosingEmptyTag: true,
          })
          ed.executeEdits('format', [{ range: model.getFullModelRange(), text: formatted }])
        } catch { /* geçersiz XML — sessizce geç */ }
      },
    })

    if (onEditorReady) {
      onEditorReady({
        goTo: (term: string) => {
          const model = editor.getModel()
          if (!model) return

          const matches = model.findMatches(term, false, false, false, null, false)

          if (matches.length > 0) {
            const { startLineNumber, startColumn } = matches[0].range
            editor.revealLineInCenter(startLineNumber)
            editor.setPosition({ lineNumber: startLineNumber, column: startColumn })
            editor.focus()
          }
        },
        insertTextAtLine: (lineNumber: number, text: string) => {
          const monaco = monacoRef.current
          const model = editor.getModel()
          if (!model || !monaco) return

          const lineContent = model.getLineContent(lineNumber)
          const tagCloseIdx = lineContent.indexOf('>')
          const insertCol = tagCloseIdx >= 0 ? tagCloseIdx + 2 : lineContent.length + 1

          editor.executeEdits('insert-image', [{
            range: new monaco.Range(lineNumber, insertCol, lineNumber, insertCol),
            text: '\n' + text,
          }])
          editor.focus()
        },
        toggleComment,
        formatDocument: () => {
          editor.getAction('xslt-format-document')?.run()
        },
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
        formatOnPaste: false,
        autoClosingBrackets: "never",
        folding: true,
        stickyScroll: { enabled: false },
        ...options,
      }}
    />
  )
}
