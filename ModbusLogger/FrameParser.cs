using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ModbusLogger
{
    class FrameParser
    {
        SerialShare _serialshare;

        StreamWriter outputFileStream;
        string docPath;
        private const string FILE_NAME = "hmislog.txt";

        public bool stopcmd;
        static int buflen = 8;
        static byte maxframelen = 100;
        char[] charbuf = new char[maxframelen * 2];

        public FrameParser(SerialShare ss)
        {
            _serialshare = ss;
            docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        public void processloop()
        {
            /*if (File.Exists(Path.Combine(docPath, FILE_NAME)))
            {
                Console.WriteLine($"{FILE_NAME} already exists!");
                _serialshare.mainbufreadidx = -1;
                return;
            }*/

            try
            {
                if (File.Exists(Path.Combine(docPath, FILE_NAME)))
                    File.Delete(Path.Combine(docPath, FILE_NAME));
                outputFileStream = new StreamWriter(Path.Combine(docPath, FILE_NAME));
            }
            catch (Exception)
            {
                _serialshare.SetState(-1);
                return;
            }

            byte[] _buffer = new byte[buflen];
            byte slaveaddr = 0x05;
            bool slaveaddrmatched = false;
            byte[] functioncodes = new byte[] { 1, 2, 3, 4, 5, 6, 15, 16 };
            bool functioncodematched = false;
            byte framelen = 0;
            byte[] _frame = new byte[maxframelen];
            bool isNewFrame = false;
            while (!stopcmd)
            {
                if (_serialshare.mainbufreadidx != _serialshare.mainbufwriteidx) {
                    _serialshare.pop(ref _buffer, buflen);

                    for (int i = 0; i < buflen; i++)
                    {
                        if (!slaveaddrmatched && !functioncodematched) {
                            if (_buffer[i] == slaveaddr)
                            {
                                framelen = 0;
                                _frame[framelen++] = _buffer[i];
                                slaveaddrmatched = true;
                            }
                        }
                        else if (slaveaddrmatched && !functioncodematched)
                        {
                            for (int j = 0; j < functioncodes.Length; j++)
                            {
                                if (_buffer[i] == functioncodes[j]) {
                                    _frame[framelen++] = _buffer[i];
                                    functioncodematched = true;
                                }
                            }
                            if (!functioncodematched)
                                slaveaddrmatched = false;
                        }
                        else if (slaveaddrmatched && functioncodematched)
                        {
                            _frame[framelen++] = _buffer[i];
                            switch (_frame[1])
                            {
                                case 0x03:
                                case 0x04:
                                    if (framelen == 8)
                                    {
                                        functioncodematched = false;
                                        slaveaddrmatched = false;
                                        isNewFrame = true;
                                    }
                                    break;

                                case 0x10:
                                    if (framelen >= 7)
                                    {
                                        if (_frame[6] > maxframelen - 9)
                                        {
                                            functioncodematched = false;
                                            slaveaddrmatched = false;
                                            isNewFrame = false;
                                        }
                                        else if (framelen == 7 + _frame[6] + 2)
                                        {
                                            functioncodematched = false;
                                            slaveaddrmatched = false;
                                            isNewFrame = true;
                                        }
                                    }
                                    break;

                                default:                                
                                    functioncodematched = false;
                                    slaveaddrmatched = false;
                                    isNewFrame = false;
                                    break;
                            }
                        }

                        if (isNewFrame)
                        {
                            processframe(_frame, framelen);
                               isNewFrame = false;
                        }
                    }
                }
                else System.Threading.Thread.Sleep(100);
            }

            outputFileStream.Flush();
            outputFileStream.Close();
        }

        string[] hex = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C", "D", "E", "F" };
        public void processframe(byte[] _framebuf, int _framelen)
        {
            //outputFileStream.Write(BitConverter.ToString(_framebuf, 0, _framelen));
            int idx = 0;
            string outstr = hex[(_framebuf[idx] >> 4) & 0xF] + hex[_framebuf[idx] & 0xF];//Slave Address

            outstr += "-";
            outstr += hex[(_framebuf[++idx] >> 4) & 0xF] + hex[_framebuf[idx] & 0xF];//Function Code

            outstr += "-";
            outstr += (40001 + (((int)_framebuf[++idx]) << 8) + _framebuf[++idx]).ToString();//Starting Register Address

            outstr += "-";
            outstr += ((((int)_framebuf[++idx]) << 8) + _framebuf[++idx]).ToString();//Quantity of Registers

            if( _framebuf[1] == 0x10)
            {
                outstr += "-";
                byte bytecount = _framebuf[++idx];
                outstr += bytecount.ToString();//ByteCount

                if (bytecount < maxframelen - 9)
                {
                    for (int i = 0; i < bytecount; i++)
                    {
                        outstr += "-";
                        outstr += hex[(_framebuf[++idx] >> 4) & 0xF] + hex[_framebuf[idx] & 0xF];//CRC Lo
                    }
                }

            }

            outstr += "-";
            int crc = ModRTU_CRC(_framebuf, idx+1);
            outstr += hex[(crc >> 4) & 0xF] + hex[crc & 0xF];//CRC Lo
            outstr += hex[(crc >> 12) & 0xF] + hex[(crc >> 8) & 0xF];//CRC Hi

            outstr += "-";
            outstr += hex[(_framebuf[++idx] >> 4) & 0xF] + hex[_framebuf[idx] & 0xF];//CRC Lo
            outstr += hex[(_framebuf[++idx] >> 4) & 0xF] + hex[_framebuf[idx] & 0xF];//CRC Hi

            outputFileStream.WriteLine(outstr);
        }

        UInt16 ModRTU_CRC(byte[] buf, int len)
        {
            UInt16 crc = 0xFFFF;

            for (int pos = 0; pos < len; pos++)
            {
                crc ^= (UInt16)buf[pos];          // XOR byte into least sig. byte of crc

                for (int i = 8; i != 0; i--)
                {    // Loop over each bit
                    if ((crc & 0x0001) != 0)
                    {      // If the LSB is set
                        crc >>= 1;                    // Shift right and XOR 0xA001
                        crc ^= 0xA001;
                    }
                    else                            // Else LSB is not set
                        crc >>= 1;                    // Just shift right
                }
            }
            // Note, this number has low and high bytes swapped, so use it accordingly (or swap bytes)
            return crc;
        }
    }
}
