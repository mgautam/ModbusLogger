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
using System.ComponentModel;

namespace ModbusLogger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static int __debugprint__ = 1;
        static int __mymbaddr__ = 6;

        static int __minframelen__ = 8;
        static int __maxframelen__ = 256;

        SerialShare _serialshare;
        SerialInterface _serialinterface;
        FrameParser _frameparser;
        FramePrinter _frame_printer;
        FrameResponder _frameresponder;
        BackgroundWorker bgWorker;
        bool stopcmd = true;

        public MainWindow()
        {
            InitializeComponent();
            _serialshare = new SerialShare();
            _serialinterface = new SerialInterface(_serialshare);
            _frame_printer = new FramePrinter(__maxframelen__, __debugprint__);
            _frameresponder = new FrameResponder(_serialinterface, _frame_printer, __mymbaddr__, __debugprint__);
            _frameparser = new FrameParser(_serialshare, _frame_printer, _frameresponder, __minframelen__, __maxframelen__, __debugprint__);

            bgWorker = new BackgroundWorker();
            bgWorker.WorkerReportsProgress = true;
            bgWorker.WorkerSupportsCancellation = true;
            bgWorker.DoWork += new DoWorkEventHandler(bgWorker_DoWork);
            bgWorker.ProgressChanged += new ProgressChangedEventHandler(bgWorker_ProgressChanged);
        }

        private void bgWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            GetState_Click(new object(), new RoutedEventArgs());
        }

        private void bgWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;

            int i = 0;
            while (!stopcmd)
            {
                // Perform a time consuming operation and report progress.
                System.Threading.Thread.Sleep(500);
                worker.ReportProgress(i * 10);
                if (++i >= 10) i = 0;
            }
        }

        private void StartRead_Click(object sender, RoutedEventArgs e)
        {
            _serialinterface.stopcmd = false;
            Thread t1 = new Thread(new ThreadStart(_serialinterface.readloop));
            t1.Start();
            _frameparser.stopcmd = false;
            Thread t2 = new Thread(new ThreadStart(_frameparser.processloop));
            t2.Start();

            stopcmd = false;
            if (bgWorker.IsBusy != true)
                bgWorker.RunWorkerAsync();
        }

        private void GetState_Click(object sender, RoutedEventArgs e)
        {
            statetxt.Text = _serialshare.GetState().ToString();
            readlentxt.Text = _serialshare.mainbufreadidx.ToString();
            outstrtxt.Text = _frameparser._frame_printer.outstr;
        }

        private void StopRead_Click(object sender, RoutedEventArgs e)
        {
            if (bgWorker.WorkerSupportsCancellation == true)
                bgWorker.CancelAsync();
            stopcmd = true;
            _serialinterface.stopcmd = true;
            _frameparser.stopcmd = true;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            stopcmd = true;
            _serialinterface.stopcmd = true;
            _frameparser.stopcmd = true;
        }
    }
}
