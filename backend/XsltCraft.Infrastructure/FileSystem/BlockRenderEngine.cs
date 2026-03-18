using System;
using System.Collections.Generic;
using System.Text;
using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Infrastructure.FileSystem
{
    public class BlockRenderEngine : IBlockRenderEngine
    {
        private readonly IRenderEngine _renderEngine;

        public BlockRenderEngine(IRenderEngine renderEngine)
        {
            _renderEngine = renderEngine;
        }

        public string Render(Layout layout, string xmlData)
        {
            var result = new StringBuilder();

            foreach (var slot in layout.Slots)
            {
                foreach (var block in slot.Blocks.OrderBy(b => b.Order))
                {
                    var html = _renderEngine.Render(block.TemplatePath, xmlData);
                    result.Append(html);
                }
            }

            return result.ToString();
        }
    }
}
