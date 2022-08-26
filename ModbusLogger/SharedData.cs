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
                for (int i = 0; i < buflen; i++)
                {
                    if (mainbufwriteidx >= mainbuflen) mainbufwriteidx = 0;
                    _mainbuffer[mainbufwriteidx] = _buffer[i];
                    mainbufwriteidx++;
                    if (mainbufwriteidx >= mainbuflen) mainbufwriteidx = 0;
                }
                _State += buflen;
            }
        }

        public int pop(ref byte[] _buffer, int buflen)
        {
            int poplen = 0;
            poplen = mainbufwriteidx - mainbufreadidx;
            if (mainbufreadidx > mainbufwriteidx)
                poplen += mainbuflen;

            if (poplen > buflen)
                poplen = buflen;

            lock (_Lock)
            {
                for (int i = 0; i < poplen; i++)
                {
                    if (mainbufreadidx >= mainbuflen) mainbufreadidx = 0;
                    _buffer[i] = _mainbuffer[mainbufreadidx];
                    mainbufreadidx++;
                    if (mainbufreadidx >= mainbuflen) mainbufreadidx = 0;
                }
            }

            return poplen;
        }
    }
}
