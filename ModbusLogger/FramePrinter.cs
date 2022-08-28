using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ModbusLogger
{

    class FramePrinter
    {
        static int _maxframelen;
        static int headerlen = 1 + 1 + 2 + 2 + 1;//SlaveAddr+FuncCode+StartAddr+NumRegs+ByteCount
        static int taillen = 2;//CRC16

        StreamWriter outputFileStream;
        string docPath;
        private const string FILE_NAME = "hmislog.txt";

        public string outstr;

        static int _debugprint = 0;
        static int _search_buf_len = 0;
        public FramePrinter(int maxframelen, int debugprint)
        {
            _debugprint = debugprint;
            _maxframelen = maxframelen;
            _search_buf_len = maxframelen*2;
            docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        public void init()
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
            catch (Exception ex)
            {
                throw ex;
            }

        }


        public void finalize()
        {
            outputFileStream.Flush();
            outputFileStream.Close();
        }

        string[] hex = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C", "D", "E", "F" };
        public void printRequest(byte[] _framebuf, int startidx, int _framelen, int readidx, int next_readidx, int writeidx)
        {
            outstr = "Req: ";
            if (_debugprint >= 2)
            {
                outstr += "(" + startidx.ToString() + "," + next_readidx.ToString();
                outstr += "," + _framelen.ToString();
                outstr += "," + writeidx.ToString() + ") ";
            }
            //outputFileStream.Write(BitConverter.ToString(_framebuf, 0, _framelen));
            int idx = 0;
            outstr += hex[(_framebuf[idx] >> 4) & 0xF] + hex[_framebuf[idx] & 0xF];//Slave Address

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

                if (bytecount < _maxframelen - (headerlen+taillen))
                {
                    for (int i = 0; i < bytecount; i++)
                    {
                        outstr += "-";
                        outstr += hex[(_framebuf[++idx] >> 4) & 0xF] + hex[_framebuf[idx] & 0xF];//CRC Lo
                    }
                }

            }

            outstr += "-";
            int readcrc = (int)_framebuf[idx + 1] + (((int)_framebuf[idx + 2])<<8);
            outstr += hex[(_framebuf[++idx] >> 4) & 0xF] + hex[_framebuf[idx] & 0xF];//CRC Lo
            outstr += hex[(_framebuf[++idx] >> 4) & 0xF] + hex[_framebuf[idx] & 0xF];//CRC Hi

            outputFileStream.WriteLine(outstr);
        }

        public void printSearchIdxes(int readidx, int writeidx)
        {
            outstr = "( ReadIdx=" + readidx.ToString() + ", WriteIdx=" + writeidx.ToString() + " )";
            outputFileStream.WriteLine(outstr);
        }
        public void printSearchLengths(int searchlen, int checklen, int framelen)
        {
            outputFileStream.Write("(" + searchlen.ToString() + "," + checklen.ToString() + "," + framelen.ToString() + ")");
        }

        public void printsearchframe(byte[] searchbuf, int startidx, int endidx, int writeidx)
        {
            outstr = "(" + startidx.ToString() + "," + endidx.ToString();
            outstr += "," + writeidx.ToString() + ") "; 
            //outputFileStream.Write(BitConverter.ToString(_framebuf, 0, _framelen));
            if (startidx <= endidx)
                for (int idx = startidx; idx < endidx; idx++)
                    outstr += hex[(searchbuf[idx] >> 4) & 0xF] + hex[searchbuf[idx] & 0xF] + " ";
            else
            {
                for (int idx = startidx; idx < _search_buf_len; idx++)
                    outstr += hex[(searchbuf[idx] >> 4) & 0xF] + hex[searchbuf[idx] & 0xF] + " ";
                for (int idx = 0; idx < endidx; idx++)
                    outstr += hex[(searchbuf[idx] >> 4) & 0xF] + hex[searchbuf[idx] & 0xF] + " ";
            }
            outputFileStream.WriteLine(outstr);
        }

        public void printResponse(byte[] searchbuf, int startidx, int endidx, int writeidx)
        {
            outstr = "RspR: ";
            if (_debugprint >= 2)
            {
                outstr += "(" + startidx.ToString() + "," + endidx.ToString();
                outstr += "," + (endidx - startidx).ToString();
                outstr += "," + writeidx.ToString() + ") ";
            }
            //outputFileStream.Write(BitConverter.ToString(_framebuf, 0, _framelen));
            if (startidx <= endidx)
                for (int idx = startidx; idx < endidx; idx++)
                    outstr += hex[(searchbuf[idx] >> 4) & 0xF] + hex[searchbuf[idx] & 0xF] + " ";
            else
            {
                for (int idx = startidx; idx < _search_buf_len; idx++)
                    outstr += hex[(searchbuf[idx] >> 4) & 0xF] + hex[searchbuf[idx] & 0xF] + " ";
                for (int idx = 0; idx < endidx; idx++)
                    outstr += hex[(searchbuf[idx] >> 4) & 0xF] + hex[searchbuf[idx] & 0xF] + " ";
            }
            outputFileStream.WriteLine(outstr);
        }

        public void printResponse(byte[] sendbuf, int framelen)
        {
            outstr = "RspM: ";
            //outputFileStream.Write(BitConverter.ToString(sendbuf, 0, framelen));
            for (int idx = 0; idx < framelen; idx++)
                outstr += hex[(sendbuf[idx] >> 4) & 0xF] + hex[sendbuf[idx] & 0xF] + " ";
            outputFileStream.WriteLine(outstr);
        }
    }
}
