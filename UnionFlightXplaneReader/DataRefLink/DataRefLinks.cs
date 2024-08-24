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
            // sim/time/paused
            // 
            new DataRefLink
            (
                XPReader.Instance.connection,
                "sim/network/misc/connection_handshake"
            ),
            new DataRefLink
            (
                AircraftReader.Instance.IndicatedAirspeedReader,
                "sim/flightmodel/position/indicated_airspeed"
            ),
            new DataRefLink
            (
                AircraftReader.Instance.TailNumberReader,
                "sim/aircraft/view/acf_tailnum"
            ),
            new DataRefLink
            (
                AircraftReader.Instance.NameReader,
                "sim/aircraft/view/acf_ui_name"
            ),
            new DataRefLink
            (
                AircraftReader.Instance.Groundspeed,
                "sim/flightmodel/position/groundspeed"
            ),
            new DataRefLink
            (
                AircraftReader.Instance.TrueAirSpeed,
                "sim/flightmodel/position/true_airspeed"
            ),
            new DataRefLink
            (
                AircraftReader.Instance.Heading,
                "sim/flightmodel/position/mag_psi"
            ),
            new DataRefLink
            (
                AircraftReader.Instance.Altitude,
                "sim/flightmodel/position/elevation"
            ),
            new DataRefLink
            (
                AircraftReader.Instance.HeightAgl,
                "sim/flightmodel/position/y_agl"
            ),
            new DataRefLink
            (
                AircraftReader.Instance.VerticalSpeed,
                "sim/flightmodel/position/vh_ind_fpm"
            ),
            new DataRefLink
            (
                AircraftReader.Instance.Latitude,
                "sim/flightmodel/position/latitude"
            ),
            new DataRefLink
            (
                AircraftReader.Instance.Longitude,
                "sim/flightmodel/position/longitude"
            ),
            new DataRefLink
            (
                SimulatorReader.Instance.XplaneInternalVersion,
                "sim/version/xplane_internal_version"
            ),
            new DataRefLink
            (
                SimulatorReader.Instance.XplaneSimSpeed,
                "sim/time/sim_speed_actual"
            ),
            new DataRefLink
            (
                SimulatorReader.Instance.IsSimPaused,
                "sim/time/paused"
            )
        };


        private static Dictionary<int, DataRefLink> dataRefLinksDictionary = new();

        static DataRefLinks()
        {
            UsedDataRefLinks.ForEach(dataRef =>
                {
                    var dataReader = dataRef.dataReader;
                    if (dataReader.type == typeof(string))
                    {
                        var stringDataReader = (StringDataReader) dataReader;

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