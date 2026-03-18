namespace XsltCraft.Domain.Entities
{
    public class Slot
    {
        public string Name { get; set; }

        public List<Block> Blocks { get; set; } = new();
    }
}
