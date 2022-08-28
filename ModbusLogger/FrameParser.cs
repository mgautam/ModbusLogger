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
        public FramePrinter _frame_printer;
        FrameResponder _frameresponder;

        public bool stopcmd;

        int _minframelen;
        int _maxframelen;
        byte[] _frame;
                
        int search_buf_readidx = 0;
        int search_buf_writeidx = 0;
        static int search_buf_len;
        byte[] _search_buffer;

        static int _debugprint = 0;
        public FrameParser(SerialShare ss, FramePrinter fp, FrameResponder fr, int minflen, int maxflen, int debugprint)
        {
            _minframelen = minflen;
            _maxframelen = maxflen;
            _frame = new byte[_maxframelen];
            search_buf_len = 2 * _maxframelen;
            _search_buffer = new byte[search_buf_len];

            _serialshare = ss;
            _debugprint = debugprint;
            _frame_printer = fp;
            _frameresponder = fr;
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
                    if (frame_wocrc_len > (_maxframelen - 2)) return -1;//Check length errors
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

            if (_debugprint >= 3)
            {
                _frame_printer.printSearchLengths(searchlen, checklen, frame_len);
                _frame_printer.printsearchframe(_search_buffer, startidx, get_searchlim_idx(startidx , frame_len), search_buf_writeidx);
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
            try
            {
                _frame_printer.init();
            }
            catch (Exception)
            {
                _serialshare.SetState(-1);
                return;
            }

            byte[] _buffer = new byte[_minframelen];
            while (!stopcmd)
            {
                if (_serialshare.mainbufreadidx != _serialshare.mainbufwriteidx) {
                    int lencopied = _serialshare.pop(ref _buffer, _minframelen);
                    copy_to_searchbuf(_buffer, lencopied);
                    //_frame_printer.printSearchIdxes(search_buf_readidx, search_buf_writeidx);

                    int searchlen = 0;
                    //bool reader_below_writer_before = false;
                    while(!stopcmd) // find all valid frames in search buffer
                    {
                        if (search_buf_readidx <= search_buf_writeidx) searchlen = search_buf_writeidx - search_buf_readidx;
                        else
                            searchlen = search_buf_writeidx + search_buf_len - search_buf_readidx;
                        if (searchlen < _minframelen) break;

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
                            if (framelen >= _minframelen)
                            {
                                // found valid frame
                                _frame_printer.printResponse(_search_buffer, search_buf_readidx, startidx, search_buf_writeidx);
                                next_readidx = get_searchlim_idx(startidx, framelen);//update next readidx
                                _frame_printer.printRequest(_frame, startidx, framelen, search_buf_readidx, next_readidx, search_buf_writeidx);
                                _frameresponder.send(_frame);
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

            _frame_printer.finalize();
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
