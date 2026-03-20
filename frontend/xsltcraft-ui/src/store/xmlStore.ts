import { create } from 'zustand'
import { v4 as uuidv4 } from 'uuid'

export interface XmlFile {
  id: string
  name: string
  content: string
}

type RightPanelTab = 'properties' | 'xml'

interface XmlStoreState {
  xmlFiles: XmlFile[]
  activeXmlId: string | null
  rightPanelTab: RightPanelTab
  isSelecting: boolean

  addXmlFile: (name: string, content: string) => string
  removeXmlFile: (id: string) => void
  setActiveXml: (id: string | null) => void
  setRightPanelTab: (tab: RightPanelTab) => void
  startXPathSelection: (callback: (xpath: string) => void) => void
  cancelXPathSelection: () => void
  applyXPathSelection: (xpath: string) => void
}

// Module-level ref — avoids stale closure issues inside Zustand
let _xpathCallback: ((xpath: string) => void) | null = null

export const useXmlStore = create<XmlStoreState>((set) => ({
  xmlFiles: [],
  activeXmlId: null,
  rightPanelTab: 'properties',
  isSelecting: false,

  addXmlFile(name, content) {
    const id = uuidv4()
    set((s) => ({
      xmlFiles: [...s.xmlFiles, { id, name, content }],
      activeXmlId: s.activeXmlId ?? id,
    }))
    return id
  },

  removeXmlFile(id) {
    set((s) => {
      const remaining = s.xmlFiles.filter((f) => f.id !== id)
      return {
        xmlFiles: remaining,
        activeXmlId: s.activeXmlId === id ? (remaining[0]?.id ?? null) : s.activeXmlId,
      }
    })
  },

  setActiveXml(id) {
    set({ activeXmlId: id })
  },

  setRightPanelTab(tab) {
    set({ rightPanelTab: tab })
  },

  startXPathSelection(callback) {
    _xpathCallback = callback
    set({ isSelecting: true, rightPanelTab: 'xml' })
  },

  cancelXPathSelection() {
    _xpathCallback = null
    set({ isSelecting: false })
  },

  applyXPathSelection(xpath) {
    _xpathCallback?.(xpath)
    _xpathCallback = null
    set({ isSelecting: false, rightPanelTab: 'properties' })
  },
}))
