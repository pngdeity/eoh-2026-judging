namespace ContestJudging.Core.Entities
{
    public class Relation
    {
        public Category Category { get; }
        public Entry EntryA { get; }
        public Operator Operator { get; }
        public Entry EntryB { get; }

        public Relation(Category category, Entry entryA, Operator @operator, Entry entryB)
        {
            Category = category;
            EntryA = entryA;
            Operator = @operator;
            EntryB = entryB;
        }
    }
}
