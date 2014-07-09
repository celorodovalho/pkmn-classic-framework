﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PkmnFoundations.Support;
using System.IO;
using PkmnFoundations.Structures;
using PkmnFoundations.Data;

namespace PkmnFoundations.GlobalTerminalService
{
    public class GTServer5 : GTServerBase
    {
        public GTServer5()
            : base(12401, true)
        {
            Initialize();
        }

        public GTServer5(int threads)
            : base(12401, true, threads)
        {
            Initialize();
        }

        private void Initialize()
        {

        }

        protected override byte[] ProcessRequest(byte[] data)
        {
            int length = BitConverter.ToInt32(data, 0);
            AssertHelper.Equals(length, data.Length);

            RequestTypes5 requestType = (RequestTypes5)data[4];
            Console.WriteLine("Handling Generation V {0} request.", requestType);

            MemoryStream response = new MemoryStream();
            response.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0, 4); // placeholder for length
            response.WriteByte((byte)requestType);
            response.WriteByte(Byte6(requestType));

            try
            {
                int pid = BitConverter.ToInt32(data, 8);
                byte version = data[0x0c];
                byte language = data[0x0d];

                switch (requestType)
                {
                    case RequestTypes5.MusicalUpload:
                    {
                        if (data.Length != 0x370)
                        {
                            response.Write(new byte[] { 0x02, 0x00 }, 0, 2);
                            break;
                        }

                        byte[] musicalData = new byte[0x230];
                        Array.Copy(data, 0x140, musicalData, 0, 0x230);
                        MusicalRecord5 record = new MusicalRecord5(pid, 0, musicalData);
                        long serial = DataAbstract.Instance.MusicalUpload5(record);

                        if (serial == 0)
                        {
                            Console.WriteLine("Uploaded musical already in server.");
                            response.Write(new byte[] { 0x02, 0x00 }, 0, 2);
                            break;
                        }

                        Console.WriteLine("Musical uploaded successfully.");
                        response.Write(new byte[] { 0x00, 0x00 }, 0, 2); // result code (0 for OK)
                        response.Write(BitConverter.GetBytes(serial), 0, 8);

                    } break;
                    case RequestTypes5.MusicalSearch:
                    {
                        if (data.Length != 0x14c)
                        {
                            response.Write(new byte[] { 0x02, 0x00 }, 0, 2);
                            break;
                        }

                        // todo: validate or log some of this?
                        ushort species = BitConverter.ToUInt16(data, 0x144);

                        MusicalRecord5[] results = DataAbstract.Instance.MusicalSearch5(species, 5);
                        response.Write(new byte[] { 0x00, 0x00 }, 0, 2); // result code (0 for OK)
                        response.Write(BitConverter.GetBytes(results.Length), 0, 4);

                        foreach (MusicalRecord5 result in results)
                        {
                            response.Write(BitConverter.GetBytes(result.PID), 0, 4);
                            response.Write(BitConverter.GetBytes(result.SerialNumber), 0, 8);
                            response.Write(result.Data, 0, 0x230);
                        }
                        Console.WriteLine("Retrieved {0} dressup results.", results.Length);

                    } break;

                    case RequestTypes5.BattleVideoUpload:
                    {
                        if (data.Length != 0x1ae8)
                        {
                            response.Write(new byte[] { 0x02, 0x00 }, 0, 2);
                            break;
                        }
                        int sigLength = BitConverter.ToInt32(data, 0x19e4);
                        if (sigLength > 0x100 || sigLength < 0x00)
                        {
                            response.Write(new byte[] { 0x02, 0x00 }, 0, 2);
                            break;
                        }

                        byte[] battlevidData = new byte[0x18a4];

                        Array.Copy(data, 0x140, battlevidData, 0, 0x18a4);
                        BattleVideoRecord5 record = new BattleVideoRecord5(pid, 0, battlevidData);
                        byte[] vldtSignature = new byte[sigLength];
                        Array.Copy(data, 0x19e8, vldtSignature, 0, sigLength);
                        // todo: validate signature.

                        long serial = DataAbstract.Instance.BattleVideoUpload5(record);

                        if (serial == 0)
                        {
                            Console.WriteLine("Uploaded battle video already in server.");
                            response.Write(new byte[] { 0x02, 0x00 }, 0, 2);
                            break;
                        }

                        Console.WriteLine("Battle video uploaded successfully.");
                        response.Write(new byte[] { 0x00, 0x00 }, 0, 2); // result code (0 for OK)
                        response.Write(BitConverter.GetBytes(serial), 0, 8);

                    } break;
                    case RequestTypes5.BattleVideoSearch:
                    {
                        if (data.Length != 0x15c)
                        {
                            response.Write(new byte[] { 0x02, 0x00 }, 0, 2);
                            break;
                        }

                        // todo: validate or log some of this?
                        BattleVideoSearchTypes5 type = (BattleVideoSearchTypes5)BitConverter.ToUInt32(data, 0x140);
                        ushort species = BitConverter.ToUInt16(data, 0x144);
                        BattleVideoMetagames5 meta = (BattleVideoMetagames5)data[0x146];
                        // Byte 148 contains a magic number related to the searched metagame.
                        // I don't think there's any need to verify it here.
                        byte country = data[0x14a];
                        byte region = data[0x14b];

                        Console.Write("Searching for ");
                        if (type != BattleVideoSearchTypes5.None)
                            Console.Write("{0}, ", type);
                        if (species != 0xffff)
                            Console.Write("species {0}, ", species);
                        Console.Write("{0}", meta);
                        if (country != 0xff)
                            Console.Write(", country {0}", country);
                        if (region != 0xff)
                            Console.Write(", region {0}", region);
                        Console.WriteLine(".");

                        BattleVideoHeader5[] results = DataAbstract.Instance.BattleVideoSearch5(species, type, meta, country, region, 30);
                        response.Write(new byte[] { 0x00, 0x00 }, 0, 2); // result code (0 for OK)
                        response.Write(BitConverter.GetBytes(results.Length), 0, 4);

                        foreach (BattleVideoHeader5 result in results)
                        {
                            response.Write(BitConverter.GetBytes(result.PID), 0, 4);
                            response.Write(BitConverter.GetBytes(result.SerialNumber), 0, 8);
                            response.Write(result.Data, 0, 0xc4);
                        }
                        Console.WriteLine("Retrieved {0} battle video results.", results.Length);

                    } break;
                    case RequestTypes5.BattleVideoWatch:
                    {
                        if (data.Length != 0x14c)
                        {
                            response.Write(new byte[] { 0x02, 0x00 }, 0, 2);
                            break;
                        }

                        long serial = BitConverter.ToInt64(data, 0x140);
                        BattleVideoRecord5 record = DataAbstract.Instance.BattleVideoGet5(serial);
                        if (record == null)
                        {
                            response.Write(new byte[] { 0x02, 0x00 }, 0, 2);
                            Console.WriteLine("Requested battle video {0} was missing.", BattleVideoHeader4.FormatSerial(serial));
                            break;
                        }

                        response.Write(new byte[] { 0x00, 0x00 }, 0, 2); // result code (0 for OK)
                        response.Write(BitConverter.GetBytes(record.PID), 0, 4);
                        response.Write(BitConverter.GetBytes(record.SerialNumber), 0, 8);
                        response.Write(record.Header.Data, 0, 0xc4);
                        response.Write(record.Data, 0, 0x17e0);
                        Console.WriteLine("Retrieved battle video {0}.", BattleVideoHeader4.FormatSerial(serial));

                    } break;

                    default:
                        response.Write(new byte[] { 0x02, 0x00 }, 0, 2);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                response.Write(new byte[] { 0x02, 0x00 }, 0, 2);
            }

            response.Flush();
            byte[] responseData = response.ToArray();
            WriteLength(responseData);
            return responseData;
        }

        private byte Byte6(RequestTypes5 type)
        {
            switch (type)
            {
                case RequestTypes5.MusicalUpload:
                case RequestTypes5.MusicalSearch:
                    return 0x52;
                case RequestTypes5.BattleVideoUpload:
                case RequestTypes5.BattleVideoSearch:
                case RequestTypes5.BattleVideoWatch:
                    return 0x55;
                default:
                    return 0x00;
            }
        }

        public override string Title
        {
            get 
            {
                return "Generation V Global Terminal";
            }
        }
    }

    internal enum RequestTypes5 : byte
    {
        MusicalUpload = 0x08,
        MusicalSearch = 0x09,

        BattleVideoUpload = 0xf0,
        BattleVideoSearch = 0xf1,
        BattleVideoWatch = 0xf2
    }
}
