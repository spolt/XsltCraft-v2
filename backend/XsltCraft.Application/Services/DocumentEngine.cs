using System;
using System.Collections.Generic;
using System.Text;
using XsltCraft.Application.Interfaces;

namespace XsltCraft.Infrastructure.FileSystem
{
    public class DocumentEngine : IDocumentEngine
    {
        private readonly ILayoutLoader _layoutLoader;
        private readonly IBlockRenderEngine _blockRenderer;

        public DocumentEngine(
            ILayoutLoader layoutLoader,
            IBlockRenderEngine blockRenderer)
        {
            _layoutLoader = layoutLoader;
            _blockRenderer = blockRenderer;
        }

        public string Generate(string layoutName, string xmlData)
        {
            var layout = _layoutLoader.Load(layoutName);

            return _blockRenderer.Render(layout, xmlData);
        }
    }
}
