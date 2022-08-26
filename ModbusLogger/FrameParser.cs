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
        bool __debugprint__ = false;

        SerialShare _serialshare;

        StreamWriter outputFileStream;
        string docPath;
        private const string FILE_NAME = "hmislog.txt";

        public bool stopcmd;

        static int minframelen = 8;
        static int maxframelen = 256;
        byte[] _frame = new byte[maxframelen];
                
        int search_buf_readidx = 0;
        int search_buf_writeidx = 0;
        static int search_buf_len = 2 * maxframelen;
        byte[] _search_buffer = new byte[search_buf_len];

        public string outstr;

        public FrameParser(SerialShare ss)
        {
            _serialshare = ss;
            docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private void copy_to_searchbuf(byte[] _buffer, int len)
        {
            for (int i = 0; i < len; i++)
            {
                if (search_buf_writeidx >= search_buf_len) search_buf_writeidx = 0;
                _search_buffer[search_buf_writeidx] = _buffer[i];
                search_buf_writeidx++;
                if (search_buf_writeidx >= search_buf_len) search_buf_writeidx = 0;
                //search_buf_writeidx = get_searchlim_idx(search_buf_writeidx, 1);//increment search_buf_writeidx
            }
        }

        private void copy_from_searchbuf(ref byte[] _buffer, int startidx, int len)
        {
            int limidx = startidx; // search buffer limited idx
            for (int i = 0; i < len; i++)
            {
                if (limidx >= search_buf_len) limidx = 0;
                _buffer[i] = _search_buffer[limidx];
                limidx++;
            }
        }

        private int get_searchlim_idx(int startidx, int offset)
        {
            // gets search buffer limited idx for indexing circular buffer
            int searchidx = startidx + offset;
            if (searchidx >= search_buf_len) searchidx -= search_buf_len;
            return searchidx;
        }

        private int checkget_framelength_in_searchbuf(int startidx, int searchlen, int checklen, ref byte[] framebuf)
        {
            // Returns -1 if valid frame could not be found
            // Else returns length of frame
            int frame_len = 0;
            byte frame_wocrc_len = 0;// frame without crc length

            int slaveaddr = _search_buffer[get_searchlim_idx(startidx, 0)];
            int funccode = _search_buffer[get_searchlim_idx(startidx, 1)];
            byte bytecount = 0;


            switch (funccode)
            {
                case 0x03: // Read Multiple Holding Registers
                case 0x04: // Read Input Registers
                    frame_wocrc_len = 6;
                    break;

                case 0x10: // Write Multiple Holding Registers
                    bytecount = _search_buffer[get_searchlim_idx(startidx, 6)];
                    frame_wocrc_len = (byte)(7 + bytecount);
                    if (frame_wocrc_len > (maxframelen - 2)) return -1;//Check length errors
                    break;

                default:
                    frame_wocrc_len = 0;
                    break;
            }

            
            int crcidx = get_searchlim_idx(startidx, frame_wocrc_len);
            int read_crc = (int)_search_buffer[crcidx];// get_searchlim_idx(crcidx, 0)];
            read_crc += (((int)_search_buffer[get_searchlim_idx(crcidx, 1)]) << 8);

            copy_from_searchbuf(ref framebuf, startidx, frame_wocrc_len);
            int calculated_crc = ModRTU_CRC(framebuf, frame_wocrc_len);

            frame_len = (int)frame_wocrc_len + 2; //including crc

            if (__debugprint__)
            {
                outputFileStream.Write("(" + searchlen.ToString() + "," + checklen.ToString() + "," + frame_len.ToString() + ")");
                printsearchframe(startidx, get_searchlim_idx(startidx , frame_len), search_buf_writeidx);
            }

            if ((read_crc == calculated_crc) && (frame_len <= checklen))
            {
                framebuf[frame_wocrc_len] = _search_buffer[crcidx];// get_searchlim_idx(crcidx, 0)];
                framebuf[frame_wocrc_len+1] = _search_buffer[get_searchlim_idx(crcidx, 1)];
                return frame_len;
            }
            else return -1;
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

            byte[] _buffer = new byte[minframelen];
            while (!stopcmd)
            {
                if (_serialshare.mainbufreadidx != _serialshare.mainbufwriteidx) {
                    int lencopied = _serialshare.pop(ref _buffer, minframelen);
                    copy_to_searchbuf(_buffer, lencopied);
                    //printSearchIdxes(search_buf_readidx, search_buf_writeidx);

                    int searchlen = 0;
                    //bool reader_below_writer_before = false;
                    while(!stopcmd) // find all valid frames in search buffer
                    {
                        if (search_buf_readidx <= search_buf_writeidx) searchlen = search_buf_writeidx - search_buf_readidx;
                        else
                            searchlen = search_buf_writeidx + search_buf_len - search_buf_readidx;
                        if (searchlen < minframelen) break;

                        bool validFrameFound = false;
                        int next_readidx = 0;
                        int startidx;
                        int framelen;
                        int checklen;
                        for (int i = 0; i < searchlen; i++) // find first valid frame
                        {
                            startidx = get_searchlim_idx(search_buf_readidx, i);
                            //outputFileStream.WriteLine(startidx);
                            checklen = searchlen - i;
                            framelen = checkget_framelength_in_searchbuf(startidx, searchlen, checklen, ref _frame);
                            if (framelen >= minframelen)
                            {
                                // found valid frame
                                //processframe(_frame, framelen, startidx, search_buf_readidx, search_buf_writeidx);
                                printResponse(search_buf_readidx, startidx, search_buf_writeidx);
                                next_readidx = get_searchlim_idx(startidx, framelen);//update next readidx
                                printRequest(_frame, startidx, framelen, search_buf_readidx, next_readidx, search_buf_writeidx);
                                search_buf_readidx = next_readidx;
                                validFrameFound = true;
                                break;
                            }
                        }

                        if (!validFrameFound)
                        {
                            // Couldn't find even a single valid frame
                            break;
                        }
                        /*if (reader_below_writer_before && (search_buf_readidx >= search_buf_writeidx))
                            break;
                        reader_below_writer_before = (search_buf_readidx < search_buf_writeidx);*/
                    }
                }
                else System.Threading.Thread.Sleep(100);
            }

            outputFileStream.Flush();
            outputFileStream.Close();
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

        string[] hex = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C", "D", "E", "F" };
        public void printRequest(byte[] _framebuf, int startidx, int _framelen, int readidx, int next_readidx, int writeidx)
        {
            outstr = "Req:";
            if (__debugprint__)
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

        public void printsearchframe(int startidx, int endidx, int writeidx)
        {
            outstr = "(" + startidx.ToString() + "," + endidx.ToString();
            outstr += "," + writeidx.ToString() + ") "; 
            //outputFileStream.Write(BitConverter.ToString(_framebuf, 0, _framelen));
            if (startidx <= endidx)
                for (int idx = startidx; idx < endidx; idx++)
                    outstr += hex[(_search_buffer[idx] >> 4) & 0xF] + hex[_search_buffer[idx] & 0xF] + " ";
            else
            {
                for (int idx = startidx; idx < search_buf_len; idx++)
                    outstr += hex[(_search_buffer[idx] >> 4) & 0xF] + hex[_search_buffer[idx] & 0xF] + " ";
                for (int idx = 0; idx < endidx; idx++)
                    outstr += hex[(_search_buffer[idx] >> 4) & 0xF] + hex[_search_buffer[idx] & 0xF] + " ";
            }
            outputFileStream.WriteLine(outstr);
        }

        public void printResponse(int startidx, int endidx, int writeidx)
        {
            outstr = "Rsp:";
            if (__debugprint__)
            {
                outstr += "(" + startidx.ToString() + "," + endidx.ToString();
                outstr += "," + (endidx - startidx).ToString();
                outstr += "," + writeidx.ToString() + ") ";
            }
            //outputFileStream.Write(BitConverter.ToString(_framebuf, 0, _framelen));
            if (startidx <= endidx)
                for (int idx = startidx; idx < endidx; idx++)
                    outstr += hex[(_search_buffer[idx] >> 4) & 0xF] + hex[_search_buffer[idx] & 0xF] + " ";
            else
            {
                for (int idx = startidx; idx < search_buf_len; idx++)
                    outstr += hex[(_search_buffer[idx] >> 4) & 0xF] + hex[_search_buffer[idx] & 0xF] + " ";
                for (int idx = 0; idx < endidx; idx++)
                    outstr += hex[(_search_buffer[idx] >> 4) & 0xF] + hex[_search_buffer[idx] & 0xF] + " ";
            }
            outputFileStream.WriteLine(outstr);
        }
    }
}
