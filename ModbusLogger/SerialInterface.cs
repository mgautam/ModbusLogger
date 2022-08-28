using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Ports;

namespace ModbusLogger
{
    class SerialInterface
    {
        SerialShare _serialshare;
        SerialPort _serialPort;

        int buflen = 8;
        byte[] _buffer;

        public bool stopcmd;

        public SerialInterface(SerialShare ss)
        {
            _serialshare = ss;
            _buffer = new byte[buflen];
        }

        public void readloop()
        {
            try
            {             
                _serialPort = new SerialPort("COM6", 115200, Parity.Even, 8, StopBits.One);
                _serialPort.Open();
            } catch(Exception)
            {
                _serialshare.SetState(-1);
                return;
            }
            _serialshare.SetState(0);

            int numbytesread;
            while (!stopcmd)
            {
                try
                {
                    if (_serialPort.BytesToRead >= buflen)
                    {
                        numbytesread = _serialPort.Read(_buffer, 0, buflen);
                        _serialshare.push(_buffer, numbytesread);
                    }
                }
                catch (TimeoutException) { }
            }
            _serialPort.Close();
        }

        public void send(byte[] txbuf, int buflen)
        {
            try
            {
                _serialPort.Write(txbuf, 0, buflen);
            }
            catch (TimeoutException) { }
        }
    }
}
