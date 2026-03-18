namespace XsltCraft.Domain.Entities
{
    public class Layout
    {
        public string Name { get; set; }

        public List<Slot> Slots { get; set; } = new();
    }
}
