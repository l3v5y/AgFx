namespace AgFx.Test.TestModels
{
    public class TestLoadContext : LoadContext
    {
        public TestLoadContext(int intCtor)
            : this(intCtor.ToString())
        {
        }

        public TestLoadContext(string str)
            : base(typeof(TestLoadContext).Name + ":" + str)
        {
        }

        public int Option { get; set; }

        protected override string GenerateKey()
        {
            return string.Format("{0}_{1}", Identity, Option);
        }
    }
}