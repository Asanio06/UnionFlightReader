using System;
using System.Collections.Generic;
using System.Text;
using UnionFlightXplaneReader;
using UnionFlightXplaneReader.DataReader;

namespace UnionFlightXplaneReader
{
    internal class DataRefLinks
    {

        public static List<DataRefLink> UsedDataRefLinks = new()
        {
            new DataRefLink
            (
                FlightReader.Instance.aircraftReader.IndicatedAirspeedReader,
                "sim/flightmodel/position/indicated_airspeed"
            ),
            new DataRefLink
            (
                FlightReader.Instance.aircraftReader.TailNumberReader,
                "sim/aircraft/view/acf_tailnum"
            ),
            new DataRefLink
            (
                FlightReader.Instance.aircraftReader.NameReader,
                "sim/aircraft/view/acf_ui_name"
            ),
        };


        private static Dictionary<int, DataRefLink> dataRefLinksDictionary = new();

        static DataRefLinks()
        {
            UsedDataRefLinks.ForEach(dataRef =>
                {
                    var dataReader = dataRef.dataReader;
                    if (dataReader.type == typeof(string))
                    {
                        var stringDataReader = (StringDataReader)dataReader;

                        for (int charIndex = 0; charIndex < stringDataReader.Length; charIndex++)
                        {
                            var charDataRef = new CharDataRefLink(dataReader, $"{dataRef.DataRef}[{charIndex}]",
                                charIndex);
                            dataRefLinksDictionary.Add(charDataRef.Id, charDataRef);
                        }
                    }
                    else
                    {
                        dataRefLinksDictionary.Add(dataRef.Id, dataRef);
                    }
                }
            );

        }

        public static Dictionary<int, DataRefLink> GetDataRefLinksDictionary()
        {
            return dataRefLinksDictionary;
        }

    }
}
