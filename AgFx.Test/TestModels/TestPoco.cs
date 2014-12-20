namespace AgFx.Test.TestModels
{
    [DataLoader(typeof(TestDataLoader))]
    public class TestPoco
    {
        public string Value { get; set; }
    }
}