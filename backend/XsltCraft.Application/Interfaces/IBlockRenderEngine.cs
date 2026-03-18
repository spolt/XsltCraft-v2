using System;
using System.Collections.Generic;
using System.Text;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Interfaces
{
    public interface IBlockRenderEngine
    {
        string Render(Layout layout, string xmlData);
    }
}
