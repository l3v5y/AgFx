namespace AgFx.Test.TestModels
{
    public class VariLoadContext : LoadContext
    {
        public VariLoadContext(object id)
            : base(id)
        {
        }

        public int Foo { get; set; }
    }
}