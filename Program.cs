using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnionFlightXPReader
{
    class Program
    {
        private const int CheckInterval_ms = 1000;

        private AircraftReader _aircraftXpReader = AircraftReader.Instance;

        static void Main(string[] args)
        {
            new Program().run();
        }

        void run()
        {
            List<DataRefLink> list = new List<DataRefLink>()
            {
                new DataRefLink
                (
                    _aircraftXpReader.AirspeedDataReader,
                    "sim/flightmodel/position/indicated_airspeed"
                ),
                new DataRefLink
                (
                    _aircraftXpReader.TailNumberDataReader,
                    "sim/aircraft/view/acf_tailnum"
                ),
                new DataRefLink
                (
                    _aircraftXpReader.AircraftNameDataReader,
                    "sim/aircraft/view/acf_ui_name"
                ),
            };

            Dictionary<int, DataRefLink> map = new Dictionary<int, DataRefLink>();

            list.ForEach(dataRef =>
                {
                    var dataReader = dataRef.dataReader;
                    if (dataReader.type == typeof(string))
                    {
                        var stringDataReader = (StringDataReader) dataReader;

                        for (int charIndex = 0; charIndex < stringDataReader.Length; charIndex++)
                        {
                            var charDataRef = new CharDataRefLink(dataReader, $"{dataRef.DataRef}[{charIndex}]",
                                charIndex);
                            map.Add(charDataRef.Id, charDataRef);
                        }
                    }
                    else
                    {
                        map.Add(dataRef.Id, dataRef);
                    }
                }
            );


            UdpClient server;
            UdpClient client;
            Task serverTask;
            Task observerTask;
            IPEndPoint XPlaneEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 49000);
            client = new UdpClient();
            client.Connect(XPlaneEP.Address, XPlaneEP.Port);
            server = new UdpClient((IPEndPoint) client.Client.LocalEndPoint);
            Console.WriteLine("Hello World!");


            CancellationTokenSource ts = new CancellationTokenSource();
            var token = ts.Token;


            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    Console.WriteLine($"-------------------------------------------------------------");
                    Console.WriteLine($"AirspeedDataReader = {_aircraftXpReader.AirspeedDataReader.Value}");
                    Console.WriteLine($"TailNumberDataReader = {_aircraftXpReader.TailNumberDataReader.Value}");
                    Console.WriteLine($"TailNumberDataReader = {_aircraftXpReader.AircraftNameDataReader.Value}");
                    Console.WriteLine($"-------------------------------------------------------------");


                    var response = await server.ReceiveAsync().ConfigureAwait(false);
                    var buffer = response.Buffer;
                    var raw = Encoding.UTF8.GetString(response.Buffer);

                    var pos = 0;
                    var header = Encoding.UTF8.GetString(buffer, pos, 4);
                    pos += 5; // Including tailing 0

                    if (header == "RREF") // Ignore other messages
                    {
                        while (pos < buffer.Length)
                        {
                            var id = BitConverter.ToInt32(buffer, pos);
                            pos += 4;

                            try
                            {
                                var value = BitConverter.ToSingle(buffer, pos);

                                var dataRefLink = map[id];
                                Type valueType = dataRefLink.dataReader.type;

                                if (valueType == typeof(string))
                                {
                                    var charDataRef = (CharDataRefLink) dataRefLink;
                                    var charIndex = charDataRef.CharIndex;
                                    var stringDataReader = (StringDataReader) charDataRef.dataReader;
                                    var character = Convert.ToChar(Convert.ToInt32(value));


                                    stringDataReader.Update(charIndex, character);
                                }
                                else
                                {
                                    map[id].dataReader.Value = value;
                                }

                                pos += 4;
                            }
                            catch (ArgumentException ex)
                            {
                            }
                            catch (Exception ex)
                            {
                                var error = ex.Message;
                            }
                        }
                    }
                }

                server.Close();
            }, token);

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    foreach (var (id, dataRefLink) in map)
                    {
                        var dg = new XPDatagram();
                        dg.Add("RREF");
                        dg.Add(dataRefLink.Frequency);
                        dg.Add(id);
                        dg.Add(dataRefLink.DataRef);
                        dg.FillTo(413);

                        client.Send(dg.Get(), dg.Len);
                    }


                    await Task.Delay(CheckInterval_ms).ConfigureAwait(false);
                }
            }, token);

            Console.ReadKey(true);
        }
    }
}