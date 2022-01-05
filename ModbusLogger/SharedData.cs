using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusLogger
{
    class SerialShare
    {
        private readonly Object _Lock = new Object();
        int _State;
        string statusmsg;

        public int mainbuflen = 1000;
        byte[] _mainbuffer;
        public int mainbufwriteidx = 0;
        public int mainbufreadidx = 0;

        public SerialShare()
        {
            mainbufwriteidx = 0;
            _State = 0;
            _mainbuffer = new byte[mainbuflen];
        }

        public Int32 GetState()
        {
            lock (_Lock)
            {
                return _State;
            }
        }

        public void UpdateState(int su)
        {
            lock (_Lock)
            {
                _State += su;
            }
        }

        public void SetState(int s)
        {
            lock (_Lock)
            {
                _State = s;
            }
        }

        public void push(byte[] _buffer, int buflen)
        {
            lock (_Lock)
            {
                if (mainbufwriteidx + buflen >= mainbuflen) mainbufwriteidx = 0;
                _buffer.CopyTo(_mainbuffer, mainbufwriteidx);
                mainbufwriteidx += buflen;
                _State += buflen;
            }
        }

        public void pop(ref byte[] _buffer, int buflen)
        {
            lock (_Lock)
            {
                if (mainbufreadidx + buflen >= mainbuflen) mainbufreadidx = 0;
                for (int i = 0; i < buflen; i++)
                    _buffer[i] = _mainbuffer[mainbufreadidx + i];
                mainbufreadidx += buflen;
            }
        }
    }
}
