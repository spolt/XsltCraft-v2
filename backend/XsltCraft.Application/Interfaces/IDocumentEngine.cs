using System;
using System.Collections.Generic;
using System.Text;

namespace XsltCraft.Application.Interfaces
{
    public interface IDocumentEngine
    {
        string Generate(string layoutName, string xmlData);
    }
}
