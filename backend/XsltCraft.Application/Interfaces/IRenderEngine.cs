using System;
using System.Collections.Generic;
using System.Text;

namespace XsltCraft.Application.Interfaces;

public interface IRenderEngine
{
    string Render(string xml, string xslt);
}