import Editor from "@monaco-editor/react"

type Props = {
value: string
onChange: (value:string)=>void
}

export default function XmlEditor({value,onChange}:Props){

return(

<Editor
height="100%"
defaultLanguage="xml"
value={value}
theme="vs-dark"
onChange={(v)=>onChange(v || "")}
/>

)

}