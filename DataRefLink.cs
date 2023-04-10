using System;
using System.Collections.Generic;
using System.Text;

namespace UnionFlightXPReader
{
    class DataRefLink
    {
        private static object lockElement = new object();
        private static int current_id = 0;


        public int Id { get; set; }
        public string DataRef { get; set; }
        public int Frequency { get; set; }

        public DataReader dataReader;

        public DataRefLink(DataReader dataReader, string dataRef, int frequency = 5)
        {
            lock (lockElement)
            {
                Id = ++current_id;
            }

            this.dataReader = dataReader;
            DataRef = dataRef;
            Frequency = frequency;
        }
    }


    class CharDataRefLink : DataRefLink
    {
        public int CharIndex { get; }

        public CharDataRefLink(DataReader dataReader, string dataRef, int charIndex, int frequency = 5) : base(dataReader,
            dataRef, frequency)
        {
            CharIndex = charIndex;
        }
    }
}