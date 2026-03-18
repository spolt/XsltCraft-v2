using System;
using System.Collections.Generic;
using System.Text;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Interfaces
{
    public interface ILayoutLoader
    {
        Layout Load(string layoutName);
    }
}
