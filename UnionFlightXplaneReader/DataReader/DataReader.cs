using System;
using System.Collections.Generic;
using System.Text;

namespace UnionFlightXplaneReader.DataReader
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
            Value = value;
        }
    }


    internal class StringDataReader : DataReader
    {
        public ushort Length { get; set; }

        public StringDataReader(ushort length) : base(typeof(string))
        {
            Length = length;
        }

        public void UpdateValue(int index, char character)
        {
            if (index == 0)
            {
                Value = "";
            }

            if (character > 0)
            {
                if (Value.Length <= index)
                    Value = Value.PadRight(index + 1, ' ');

                var current = Value[index];
                if (current != character)
                {
                    Value = Value.Remove(index, 1).Insert(index, character.ToString());
                }
            }
        }
    }

    internal class FloatDataReader : DataReader
    {
        private Func<float, float> convertFunction;

        public FloatDataReader(Func<float,float> convertFunction = null) : base(typeof(float))
        {
            this.convertFunction = convertFunction;

        }

        public override void UpdateValue(dynamic value)
        {
            if (convertFunction != null)
            {

                float convertedValue = convertFunction((float) value);
                base.UpdateValue(convertedValue);

                return;
            }

            base.UpdateValue((float?) value);
        }
    }
    internal class DoubleDataReader : DataReader
    {
        private Func<double, double> convertFunction;

        public DoubleDataReader(Func<double, double> convertFunction = null) : base(typeof(double))
        {
            this.convertFunction = convertFunction;

        }

        public override void UpdateValue(dynamic value)
        {
            if (convertFunction != null)
            {

                double convertedValue = convertFunction((double) value);
                base.UpdateValue(convertedValue);

                return;
            }


            base.UpdateValue((double?) value);
        }
    }

    internal class IntDataReader : DataReader
    {
        public IntDataReader() : base(typeof(int))
        {

        }

        public override void UpdateValue(dynamic value)
        {
            base.UpdateValue((int?) value);
        }
    }
}