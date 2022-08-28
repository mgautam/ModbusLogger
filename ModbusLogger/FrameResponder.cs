using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ModbusLogger
{

    class FrameResponder
    {

        SerialInterface _serialif;
        FramePrinter _frameprinter;

        static int _mymbaddr = 5;

        static int _debugprint = 0;
        public FrameResponder(SerialInterface si, FramePrinter fp, int mymbaddr, int debugprint)
        {
            _mymbaddr = mymbaddr;
            _serialif = si;
            _frameprinter = fp;
            _debugprint = debugprint;
        }

        private void memcopy(byte[] src, ref byte[] dest, int length)
        {
            for (int i = 0; i < length; i++)
                dest[i] = src[i];
        }

        public void send(byte[] framebuf)
        {
            // Returns -1 if valid frame could not be found
            // Else returns length of frame
            int frame_len = 0;
            byte frame_wocrc_len = 0;// frame without crc length

            int slaveaddr = framebuf[0];

            /*if (slaveaddr != _mymbaddr)
                return;*/

            int funccode = framebuf[1];
            int startaddr = (((int)framebuf[2]) << 8) + (int)framebuf[3];
            int numregisters = (((int)framebuf[4]) << 8) + (int)framebuf[5];
            byte bytecount = 0;

            byte[] tempbuf = new byte[256];

            switch (funccode)
            {
                case 0x03: // Read Multiple Holding Registers
                case 0x04: // Read Input Registers
                    bytecount = (byte) (2 * numregisters);
                    memcopy(framebuf, ref tempbuf, 2);//SlaveAddr+FuncCode
                    tempbuf[2] = bytecount;
                    switch (40001+startaddr)
                    {
                        case 40166:
                        case 41120:
                        case 40409:
                        case 45658:
                        case 41014:
                        case 45012:
                        case 40008:
                            tempbuf[3] = 00; tempbuf[4] = 00;
                            break;
                        case 45014:
                        case 40102:
                        case 45020:
                            tempbuf[3] = 00; tempbuf[4] = 00;
                            tempbuf[5] = 00; tempbuf[6] = 00;
                            break;
                        case 41025:
                            tempbuf[3] = 0x06; tempbuf[4] = 0x66;
                            break;
                        case 45008:
                            tempbuf[3] = 0x00; tempbuf[4] = 0x02;
                            break;
                        case 45009:
                            tempbuf[3] = 0x00; tempbuf[4] = 0x0D;
                            break;
                        case 40001:
                            tempbuf[3] = 00; tempbuf[4] = 0x00;
                            tempbuf[5] = 00; tempbuf[6] = 00;
                            break;
                        case 41029:
                            tempbuf[3] = 00; tempbuf[4] = 0x14;
                            tempbuf[5] = 00; tempbuf[6] = 0x7D;
                            break;
                        case 41031:
                            tempbuf[3] = 0x04; tempbuf[4] = 0x81;
                            tempbuf[5] = 0x1A; tempbuf[6] = 0xDB;
                            break;
                        case 41008:
                            tempbuf[3] = 0x10; tempbuf[4] = 0xAC;
                            tempbuf[5] = 0x08; tempbuf[6] = 0x1E;
                            break;
                        case 41010:
                            tempbuf[3] = 0x02; tempbuf[4] = 0x0C; tempbuf[5] = 0x0C; tempbuf[6] = 0x35;
                            tempbuf[7] = 0x00; tempbuf[8] = 0x00; tempbuf[9] = 0x03; tempbuf[10] = 0x6B;
                            break;
                        case 41038:
                            tempbuf[3] = 0x2E; tempbuf[4] = 0x38; tempbuf[5] = 0x34; tempbuf[6] = 0x31;
                            tempbuf[7] = 0x30; tempbuf[8] = 0x2E; tempbuf[9] = 0x2E; tempbuf[10] = 0x30;
                            tempbuf[11] = 0x34; tempbuf[12] = 0x31; tempbuf[13] = 0x38; tempbuf[14] = 0x38;
                            tempbuf[15] = 0x47; tempbuf[16] = 0x38; tempbuf[17] = 0x00; tempbuf[18] = 0x00;
                            break;
                        default:
                            for (byte i = 0; i < bytecount; i++)
                                tempbuf[i + 3] = i;
                            break;
                    }
                    frame_wocrc_len = (byte)(3+bytecount);
                    break;

                case 0x10: // Write Multiple Holding Registers
                    memcopy(framebuf, ref tempbuf, 6);//SlaveAddr+FuncCode+StartAddr+NumRegisters
                    frame_wocrc_len = 6;
                    break;

                default:
                    frame_wocrc_len = 0;
                    break;
            }

            int calculated_crc = ModRTU_CRC(tempbuf, frame_wocrc_len);
            tempbuf[frame_wocrc_len] = (byte)(calculated_crc & 0xFF);
            tempbuf[frame_wocrc_len+1] = (byte)((calculated_crc >> 8) & 0xFF);
            frame_len = (int)frame_wocrc_len + 2; //including crc

            if (slaveaddr == _mymbaddr)
                _serialif.send(tempbuf, frame_len);
            if (_debugprint >= 1)
            {
                _frameprinter.printResponse(tempbuf, frame_len);
            }
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
