import { Layout, Model } from "flexlayout-react"
import { useState, useEffect, useRef } from "react"

import TemplateList from "../components/TemplateList"
import { getTemplateFile, validateXslt, transformPreview } from "../api/templateApi"
import XsltEditor from "../components/Xslteditor"
import type { XsltError } from "../components/Xslteditor"
import PreviewPanel from "../components/PreviewPanel"
import Toolbar from "../components/Toolbar"

const json = {

global:{ tabEnableClose:false },

layout:{
type:"row",
children:[

{
type:"tabset",
weight:20,
children:[
{
type:"tab",
name:"Templates",
component:"templates"
}
]
},

{
type:"tabset",
weight:40,
children:[
{
type:"tab",
name:"XSLT Editor",
component:"xslt"
}
]
},

{
type:"tabset",
weight:40,
children:[
{
type:"tab",
name:"Preview",
component:"preview"
}
]
}

]
}

}

const model = Model.fromJson(json)

export default function Playground() {

const [xslt,setXslt] = useState("<xsl:stylesheet version='1.0'></xsl:stylesheet>")
const [xml,setXml] = useState("")
const [preview,setPreview] = useState("")
const [xmlStatus,setXmlStatus] = useState<"empty"|"loaded"|"invalid">("empty")
const [xsltStatus,setXsltStatus] = useState<"valid"|"invalid"|"checking">("valid")
const [xsltErrors,setXsltErrors] = useState<XsltError[]>([])
const [xsltErrorTooltip,setXsltErrorTooltip] = useState("")
const editorGoToRef = useRef<((term: string) => void) | null>(null)
const transformDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)
const validateDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)

async function handleFileSelect(templateId: string, fileName: string) {
  const data = await getTemplateFile(templateId, fileName)
  setXslt(data.content)
}

function factory(node:any){

const component = node.getComponent()

if(component === "templates"){
return <TemplateList onFileSelect={handleFileSelect}/>
}

if(component === "xslt"){
return <XsltEditor
  value={xslt}
  onChange={setXslt}
  onEditorReady={(fns) => { editorGoToRef.current = fns.goTo }}
  errors={xsltErrors}
/>
}

if(component === "preview"){
return <PreviewPanel
  html={preview}
  onTextSelect={(text: string) => editorGoToRef.current?.(text)}
/>
}

}

async function runTransform(xmlInput: string, xsltInput: string) {
  if (!xmlInput || !xsltInput) return

  const result = await transformPreview(xmlInput, xsltInput)

  if (result.ok) {
    setPreview(result.html)
  } else {
    // Update XSLT status from transform error too
    setXsltStatus("invalid")
    setXsltErrorTooltip(result.error)
    if (result.line) {
      setXsltErrors([{ message: result.error, line: result.line, column: result.column }])
    }

    // Show error in preview panel
    setPreview(`<div style="padding:24px;font-family:system-ui">
      <div style="color:#f87171;font-weight:600;font-size:15px;margin-bottom:8px">⚠ XSLT Transform Hatası</div>
      <pre style="color:#fca5a5;background:#1e1e1e;padding:12px;border-radius:6px;font-size:13px;white-space:pre-wrap;border:1px solid #3d3e4a">${result.error}</pre>
      ${result.line ? `<div style="color:#9d9da6;font-size:12px;margin-top:8px">Satır: ${result.line}, Sütun: ${result.column}</div>` : ""}
    </div>`)
  }
}

function handleXmlUpload(e: any) {
  const file = e.target.files[0]
  if (!file) return

  const reader = new FileReader()
  reader.onload = (event: any) => {
    const xmlContent = event.target.result as string

    // Validate XML
    const parser = new DOMParser()
    const doc = parser.parseFromString(xmlContent, "application/xml")
    const parseError = doc.querySelector("parsererror")

    if (parseError) {
      setXmlStatus("invalid")
      return
    }

    setXml(xmlContent)
    setXmlStatus("loaded")
    runTransform(xmlContent, xslt)
  }
  reader.readAsText(file)
}

function injectImageIntoDivId(divId: string, imgTag: string) {
  return xslt
    .replace(
      new RegExp(`<div\\s+id="${divId}"[^>]*>[\\s\\S]*?<\\/div>`, "g"),
      `<div id="${divId}">${imgTag}</div>`
    )
    .replace(
      new RegExp(`<div\\s+id="${divId}"\\s*\\/>`, "g"),
      `<div id="${divId}">${imgTag}</div>`
    )
}

function handleLogoUpload(base64: string, mimeType: string, width: number, height: number) {
  const imgTag = `<img src="data:${mimeType};base64,${base64}" width="${width}" height="${height}"/>`
  setXslt(injectImageIntoDivId("companyLogo", imgTag))
}

function handleSignatureUpload(base64: string, mimeType: string, width: number, height: number) {
  const imgTag = `<img src="data:${mimeType};base64,${base64}" width="${width}" height="${height}"/>`
  setXslt(injectImageIntoDivId("companyKase", imgTag))
}

function handleSave() {
  const blob = new Blob([xslt], { type: "application/xml" })
  const url = URL.createObjectURL(blob)
  const a = document.createElement("a")
  a.href = url
  a.download = "template.xslt"
  a.click()
  URL.revokeObjectURL(url)
}

// Debounced transform on XSLT change
useEffect(() => {
  if (!xml) return

  if (transformDebounceRef.current) clearTimeout(transformDebounceRef.current)

  transformDebounceRef.current = setTimeout(() => {
    runTransform(xml, xslt)
  }, 400)

  return () => {
    if (transformDebounceRef.current) clearTimeout(transformDebounceRef.current)
  }
}, [xslt])

// Debounced XSLT validation on change
useEffect(() => {
  if (validateDebounceRef.current) clearTimeout(validateDebounceRef.current)

  setXsltStatus("checking")

  validateDebounceRef.current = setTimeout(async () => {
    try {
      const result = await validateXslt(xslt)

      if (result.valid) {
        setXsltStatus("valid")
        setXsltErrors([])
        setXsltErrorTooltip("")
      } else {
        setXsltStatus("invalid")
        setXsltErrors([{
          message: result.error,
          line: result.line,
          column: result.column,
        }])
        setXsltErrorTooltip(result.error)
      }
    } catch {
      // Network error — don't block the user
      setXsltStatus("valid")
      setXsltErrors([])
      setXsltErrorTooltip("")
    }
  }, 500)

  return () => {
    if (validateDebounceRef.current) clearTimeout(validateDebounceRef.current)
  }
}, [xslt])

return (

<div style={{height:"100vh", background:"var(--xc-bg-darkest)"}}>

<Toolbar
  onXmlUpload={handleXmlUpload}
  onLogoUpload={handleLogoUpload}
  onSignatureUpload={handleSignatureUpload}
  onSave={handleSave}
  xmlStatus={xmlStatus}
  xsltStatus={xsltStatus}
  xsltErrorTooltip={xsltErrorTooltip}
/>

<div
style={{
height:"calc(100vh - 48px)",
position:"relative"
}}
>

<Layout
model={model}
factory={factory}
/>

</div>

</div>

)



}
