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
        private Func<float, float>? convertFunction;

        public FloatDataReader(Func<float, float> convertFunction = null) : base(typeof(float))
        {
            this.convertFunction = convertFunction;
        }

        public override void UpdateValue(dynamic value)
        {
            if (convertFunction != null)
            {

                float convertedValue = convertFunction((float)value);
                base.UpdateValue(convertedValue);

                return;
            }

            base.UpdateValue((float?)value);
        }
    }
    internal class DoubleDataReader : DataReader
    {

        private Func<double, double>? convertFunction;

        public DoubleDataReader(Func<double, double> convertFunction = null) : base(typeof(double))
        {
            this.convertFunction = convertFunction;
        }

        public override void UpdateValue(dynamic value)
        {
            if (convertFunction != null)
            {

                double convertedValue = convertFunction((double)value);
                base.UpdateValue(convertedValue);

                return;
            }

            base.UpdateValue((double?)value);
        }
    }

    internal class IntDataReader : DataReader
    {
        public IntDataReader() : base(typeof(int))
        {

        }
    }
}