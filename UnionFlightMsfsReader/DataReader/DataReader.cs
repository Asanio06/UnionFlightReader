namespace UnionFlightMsfsReader
{
    internal abstract class DataReader
    {
        public Type type { get; }
        public dynamic? Value { get; internal set; }


        protected DataReader(Type type)
        {
            this.type = type;
        }

        public virtual void UpdateValue(dynamic value)
        {
            Value = Convert.ChangeType(value, type);
        }
    }


    internal class StringDataReader : DataReader
    {

        public StringDataReader() : base(typeof(string))
        {
        }
    }

    internal class FloatDataReader : DataReader
    {
        public FloatDataReader() : base(typeof(float))
        {
        }
    }
    internal class DoubleDataReader : DataReader
    {

        public DoubleDataReader() : base(typeof(double))
        {
        }
    }

    internal class IntDataReader : DataReader
    {
        public IntDataReader() : base(typeof(int))
        {

        }
    }
}