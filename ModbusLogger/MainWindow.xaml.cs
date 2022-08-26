using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;

namespace ModbusLogger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SerialShare _serialshare;
        SerialReader _serialreader;
        FrameParser _frameparser;

        public MainWindow()
        {
            InitializeComponent();
            _serialshare = new SerialShare();
            _serialreader = new SerialReader(_serialshare);
            _frameparser = new FrameParser(_serialshare);
        }

        private void StartRead_Click(object sender, RoutedEventArgs e)
        {
            _serialreader.stopcmd = false;
            Thread t1 = new Thread(new ThreadStart(_serialreader.readloop));
            t1.Start();
            _frameparser.stopcmd = false;
            Thread t2 = new Thread(new ThreadStart(_frameparser.processloop));
            t2.Start();
        }

        private void GetState_Click(object sender, RoutedEventArgs e)
        {
            statetxt.Text = _serialshare.GetState().ToString();
            readlentxt.Text = _serialshare.mainbufreadidx.ToString();
            outstrtxt.Text = _frameparser.outstr;
        }

        private void StopRead_Click(object sender, RoutedEventArgs e)
        {
            _serialreader.stopcmd = true;
            _frameparser.stopcmd = true;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _serialreader.stopcmd = true;
            _frameparser.stopcmd = true;
        }
    }
}
