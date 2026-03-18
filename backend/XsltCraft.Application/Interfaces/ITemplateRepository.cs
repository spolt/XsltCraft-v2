using System;
using System.Collections.Generic;
using System.Text;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Interfaces;

public interface ITemplateRepository
{
    IEnumerable<Template> GetAll();

    Task<Template?> GetById(string id);
}