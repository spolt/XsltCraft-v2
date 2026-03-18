using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Infrastructure.FileSystem
{
    public class FileSystemLayoutLoader : ILayoutLoader
    {
        private readonly string _layoutPath = "Templates/Layouts";

        public Layout Load(string layoutName)
        {
            var file = Path.Combine(_layoutPath, $"{layoutName}.layout.json");

            if (!File.Exists(file))
                throw new Exception($"Layout not found: {layoutName}");

            var json = File.ReadAllText(file);

            return JsonSerializer.Deserialize<Layout>(json)!;
        }
    }
}
