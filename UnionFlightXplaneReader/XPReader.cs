using FlightDataClass;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnionFlight;
using UnionFlight.FlightData;
using UnionFlightXplaneReader.DataReader;
using UnionFlightXplaneReader.FlightData;

namespace UnionFlightXplaneReader
{
    public class XPReader : IFlightReader
    {
        private UdpClient server;
        private UdpClient client;

        private const int CheckInterval_ms = 5000;

        private CancellationTokenSource cancelationTokenSource;

        private static Dictionary<int, DataRefLink> dataRefLinksDictionary = DataRefLinks.GetDataRefLinksDictionary();

        internal IntDataReader connection = new IntDataReader();

        private const ushort XpPort = 49000;

        public IFlight flight => Flight.Instance;
        public IAircraft aircraft => Aircraft.Instance;
        public ISim simulator => Simulator.Instance;


        private XPReader()
        {
            IPEndPoint XPlaneEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), XpPort);
            client = new UdpClient();
            client.Connect(XPlaneEP.Address, XPlaneEP.Port);
            server = new UdpClient((IPEndPoint)client.Client.LocalEndPoint);
            cancelationTokenSource = new CancellationTokenSource();
        }


        public bool IsSimLaunched()
        {
            return System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners()
                .Any(p => p.Port == XpPort);
        }


        public void run()
        {
            var token = cancelationTokenSource.Token;


            Task.Factory.StartNew(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var response = await server.ReceiveAsync().ConfigureAwait(false);
                    var buffer = response.Buffer;

                    updateDataReader(buffer);
                }

                server.Close();
            }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            Task.Factory.StartNew(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    requestDataRefs();

                    await Task.Delay(CheckInterval_ms).ConfigureAwait(false);
                }
            }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }


        private void updateDataReader(byte[] buffer)
        {
            var pos = 0;
            var header = Encoding.UTF8.GetString(buffer, pos, 4);
            pos += 5; // Including tailing 0

            if (header == "RREF") // Ignore other messages
            {
                while (pos < buffer.Length)
                {
                    var id = BitConverter.ToInt32(buffer, pos);
                    pos += 4;


                    var dataRefLink = dataRefLinksDictionary[id];


                    try
                    {
                        var value = BitConverter.ToSingle(buffer, pos);

                        Type valueType = dataRefLink.dataReader.type;

                        if (valueType == typeof(string))
                        {
                            var charDataRef = (CharDataRefLink)dataRefLink;
                            var charIndex = charDataRef.CharIndex;
                            var stringDataReader = (StringDataReader)charDataRef.dataReader;
                            var character = Convert.ToChar(Convert.ToInt32(value));


                            stringDataReader.UpdateValue(charIndex, character);
                        }
                        else
                        {
                            dataRefLink.dataReader.UpdateValue(value);
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

        private void requestDataRefs()
        {
            foreach (var (id, dataRefLink) in dataRefLinksDictionary)
            {

                var dg = new XPDatagram();
                dg.Add("RREF");
                dg.Add(dataRefLink.Frequency);
                dg.Add(id);
                dg.Add(dataRefLink.DataRef);
                dg.FillTo(413);

                try
                {
                    var a = client.Send(dg.Get(), dg.Len);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }


        public void stop()
        {
            client.Close();
            server.Close();

            cancelationTokenSource.Dispose();
        }


        public static XPReader Instance
        {
            get { return Nested.instance; }
        }


        private class Nested
        {
            // Explicit static constructor to tell C# compiler
            // not to mark type as beforefieldinit
            static Nested()
            {
            }

            internal static readonly XPReader instance = new();
        }
    }
}