﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnionFlightXplaneReader.DataReader;

namespace UnionFlightXplaneReader
{
    public class XPReader
    {
        private UdpClient server;
        private UdpClient client;
        private Task serverTask;
        private Task observerTask;
        private const int CheckInterval_ms = 1000;
        
        private CancellationTokenSource cancelationTokenSource;

        private static Dictionary<int, DataRefLink> dataRefLinksDictionary = DataRefLinks.GetDataRefLinksDictionary();


        private XPReader()
        {
            IPEndPoint XPlaneEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 49000);
            client = new UdpClient();
            client.Connect(XPlaneEP.Address, XPlaneEP.Port);
            server = new UdpClient((IPEndPoint) client.Client.LocalEndPoint);
            cancelationTokenSource = new CancellationTokenSource();
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

        public void run()
        {
            var token = cancelationTokenSource.Token;


            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var response = await server.ReceiveAsync().ConfigureAwait(false);
                    var buffer = response.Buffer;

                    updateDataReader(buffer);
                }

                server.Close();
            }, token);

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    requestDataRefs();

                    await Task.Delay(CheckInterval_ms).ConfigureAwait(false);
                }
            }, token);
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
                            var charDataRef = (CharDataRefLink) dataRefLink;
                            var charIndex = charDataRef.CharIndex;
                            var stringDataReader = (StringDataReader) charDataRef.dataReader;
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

                client.Send(dg.Get(), dg.Len);
            }
        }
    }
}