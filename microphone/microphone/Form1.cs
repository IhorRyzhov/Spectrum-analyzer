using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

using NAudio.Wave; // installed with nuget
using NAudio.CoreAudioApi;
using System.Numerics;
using ZedGraph;

//https://blogs.msdn.microsoft.com/rucoding4fun/2009/11/27/net-o/

namespace microphone
{
    public partial class Form1 : Form
    {
        public WaveIn wi;
        public BufferedWaveProvider bwp;
        public Int32 envelopeMax;

        private int RATE = 44100; // sample rate of the sound card
        private int BUFFERSIZE = (int) Math.Pow(2,11); // must be a multiple of 2
        int frameSize;
        byte []frames;        

        // convert it to int32 manually (and a double for scottplot)
        const int SAMPLE_RESOLUTION = 16;
        const int BYTES_PER_POINT = SAMPLE_RESOLUTION / 8;

        Int32[] vals;
        double[] Ys, Xs, Ys2, Xs2;
        double[,]spectrs;

        GraphPane pane1;
        GraphPane pane2;

        string path;
        bool visible = false;

        bool[] ch = { false, false, false, false, false };

        public Form1()
        {
            InitializeComponent();

            SaveSpectr.Enabled = false;
            radioButton3.Checked = true;

            frameSize = BUFFERSIZE;
            frames = new byte[frameSize];
            vals = new Int32[frames.Length / BYTES_PER_POINT];
            Ys = new double[frames.Length / BYTES_PER_POINT];
            Xs = new double[frames.Length / BYTES_PER_POINT];
            Ys2 = new double[frames.Length / BYTES_PER_POINT];
            Xs2 = new double[frames.Length / BYTES_PER_POINT];
            spectrs = new double [5, frames.Length / BYTES_PER_POINT];
       
            WaveIn wi = new WaveIn();
            wi.WaveFormat = new NAudio.Wave.WaveFormat(RATE, 1);
            wi.BufferMilliseconds = (int)((double)BUFFERSIZE / (double)RATE * 1000.0);
            wi.DataAvailable += new EventHandler<WaveInEventArgs>(wi_DataAvailable);
            bwp = new BufferedWaveProvider(wi.WaveFormat);
            bwp.BufferLength = BUFFERSIZE * 2;
            bwp.DiscardOnBufferOverflow = true;
            wi.StartRecording();

            pane1 = zedGraphControl1.GraphPane;
            pane1.XAxis.Title.Text = "Время";
            pane1.YAxis.Title.Text = "Амплитуда";
            pane1.Title.Text = "Сигнал во временной области";           
            pane1.XAxis.Scale.Min = 0; // Устанавливаем интересующий нас интервал по оси X
            pane1.XAxis.Scale.Max = Xs.Length;//            
            pane1.YAxis.Scale.Min = - Math.Pow(2, 16);// Устанавливаем интересующий нас интервал по оси Y
            pane1.YAxis.Scale.Max = Math.Pow(2, 16) - 1;//
            pane1.CurveList.Clear();
            zedGraphControl1.AxisChange();
            zedGraphControl1.Invalidate();


            pane2 = zedGraphControl2.GraphPane;
            pane2.XAxis.Title.Text = "Частота";
            pane2.YAxis.Title.Text = "Амплитуда";
            pane2.Title.Text = "Спектр";
            pane2.XAxis.Scale.Min = 0; // Устанавливаем интересующий нас интервал по оси X
            pane2.XAxis.Scale.Max = 22050;//Xs2.Length / 2;      
            pane2.YAxis.Scale.Min = 0;// Устанавливаем интересующий нас интервал по оси Y
            pane2.YAxis.Scale.Max = 3000;//
            pane2.CurveList.Clear();
            zedGraphControl2.AxisChange();
            zedGraphControl2.Invalidate();

            checkBox1.Enabled = false;
            checkBox2.Enabled = false;
            checkBox3.Enabled = false;
            checkBox4.Enabled = false;
            checkBox5.Enabled = false;

            timer1.Enabled = true;
        }

        // adds data to the audio recording buffer
        private void wi_DataAvailable(object sender, WaveInEventArgs e)
        {
            bwp.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        private void UpdateAudioGraph()
        {           
            bwp.Read(frames, 0, frameSize);
            if (frames.Length == 0) return;
            if (frames[frameSize-2] == 0) return;
            
            timer1.Enabled = false;           

            for (int i = 0; i < vals.Length; i++)
            {
                // bit shift the byte buffer into the right variable format
                byte hByte = frames[i * 2 + 1];
                byte lByte = frames[i * 2 + 0];
                vals[i] = (int)(short)((hByte << 8) | lByte);
                Xs[i] = i;
                Ys[i] = vals[i];
                Xs2[i] = i * 43.0664;
            } 

            if(radioButton1.Checked == true) // окно Блэкмана
            {
                for (int i = 0; i < vals.Length; i++)
                {
                    Ys[i] = Ys[i] * (0.92 - 0.5 * Math.Cos((2 * Math.PI * i) / (vals.Length - 1)) + 0.08 * Math.Cos((4 * Math.PI * i) / (vals.Length - 1)));
                }
            }

            if (radioButton2.Checked == true) // окно Хэмминга
            {
                for (int i = 0; i < vals.Length; i++)
                {
                    Ys[i] = Ys[i] * (0.53836 - 0.46164 * Math.Cos((2 * Math.PI * i) / (vals.Length - 1)));
                }
            }

            if (radioButton4.Checked == true) // окно Хэннинга
            {
                for (int i = 0; i < vals.Length; i++)
                {
                    Ys[i] = Ys[i] * (0.5 * (1 - Math.Cos((2 * Math.PI * i) / (vals.Length - 1)))); 
                }
            }

            Ys2 = FFT(Ys);
            Application.DoEvents();
           
            pane1.CurveList.Clear();
            pane2.CurveList.Clear();

            if (visible == true)
            {
                LineItem myCurve1 = pane1.AddCurve("", Xs, Ys, Color.Brown, SymbolType.None);
                LineItem myCurve2 = pane2.AddCurve("", Xs2, Ys2, Color.Brown, SymbolType.None);
                textBox1.Text = Convert.ToString(mainFrequency(Ys2));
            }
            else
            {
                textBox1.Text = "";
            }             
            
            if(checkBox1.Checked == true)
            {
                double []data = new double[frames.Length / BYTES_PER_POINT];
                for(int i = 0; i < data.Length; i++)
                {
                    data[i] = spectrs[0, i];
                }
                LineItem sp0 = pane2.AddCurve("", Xs2, data, Color.Green, SymbolType.None);
            }

            if (checkBox2.Checked == true)
            {
                double[] data = new double[frames.Length / BYTES_PER_POINT];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = spectrs[1, i];
                }
                LineItem sp1 = pane2.AddCurve("", Xs2, data, Color.Orange, SymbolType.None);
            }

            if (checkBox3.Checked == true)
            {
                double[] data = new double[frames.Length / BYTES_PER_POINT];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = spectrs[2, i];
                }
                LineItem sp2 = pane2.AddCurve("", Xs2, data, Color.Blue, SymbolType.None);
            }

            if (checkBox4.Checked == true)
            {
                double[] data = new double[frames.Length / BYTES_PER_POINT];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = spectrs[3, i];
                }
                LineItem sp3 = pane2.AddCurve("", Xs2, data, Color.Red, SymbolType.None);
            }

            if (checkBox5.Checked == true)
            {
                double[] data = new double[frames.Length / BYTES_PER_POINT];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = spectrs[4, i];
                }
                LineItem sp4 = pane2.AddCurve("", Xs2, data, Color.Purple, SymbolType.None);
            }

            zedGraphControl1.AxisChange();
            zedGraphControl1.Invalidate();

            zedGraphControl2.AxisChange();
            zedGraphControl2.Invalidate();            

            timer1.Enabled = true;       
        }

        private double[] FFT(double[] data)
        {
            double[] fft = new double[data.Length];
            Complex[] fftComplex = new Complex[data.Length]; 
            for (int i = 0; i < data.Length; i++)
            {
                fftComplex[i] = new Complex(data[i], 0.0); 
            }
            Accord.Math.FourierTransform.FFT(fftComplex, Accord.Math.FourierTransform.Direction.Forward);
            for (int i = 0; i < data.Length; i++)
            {
                fft[i] = fftComplex[i].Magnitude; 
            }
            return fft;
        }

        private UInt32 mainFrequency(double[] data)
        {
            double max = 0;
            UInt32 index = 0;
            for(UInt32 i = 0; i < data.Length / 2; i++)
            {
                if(data[i] > max)
                {
                    max = data[i];
                    index = i;
                }
            }

            max = index;
            max *= 43.0664;

            return (UInt32)max;
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            visible = false;            
        }

        private void Path_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog DirDialog = new FolderBrowserDialog();
            DirDialog.Description = "Выбор директории";

            if (DirDialog.ShowDialog() == DialogResult.OK)
            {
                path = DirDialog.SelectedPath;
                SaveSpectr.Enabled = true;
            }
        }

        private void Save_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < 5; i++)
            {
                if(ch[i] == false)
                {
                    ch[i] = true;
                    BinaryWriter BW = new BinaryWriter(File.Open(path + @"\spectr" + Convert.ToString(i) + @".spctr", FileMode.Create));
                    for (int j = 0; j < Ys2.Length; j++)
                    {
                        spectrs[i, j] = Ys2[j];
                        BW.Write(spectrs[i,j]);
                    }
                    
                    BW.Close();
                    break;
                }
            }

            if (ch[0] == true)
            {
                checkBox1.Enabled = true;
            }
            if (ch[1] == true)
            {
                checkBox2.Enabled = true;
            }
            if (ch[2] == true)
            {
                checkBox3.Enabled = true;
            }
            if (ch[3] == true)
            {
                checkBox4.Enabled = true;
            }
            if (ch[4] == true)
            {
                checkBox5.Enabled = true;
            }
        }

        private void Delete_Click(object sender, EventArgs e)
        {
            if(checkBox1.Checked == true)
            {
                ch[0] = false;
                checkBox1.CheckState = CheckState.Unchecked;
                checkBox1.Enabled = false;
            }
            if (checkBox2.Checked == true)
            {
                ch[1] = false;
                checkBox2.CheckState = CheckState.Unchecked;
                checkBox2.Enabled = false;
            }
            if (checkBox3.Checked == true)
            {
                ch[2] = false;
                checkBox3.CheckState = CheckState.Unchecked;
                checkBox3.Enabled = false;
            }
            if (checkBox4.Checked == true)
            {
                ch[3] = false;
                checkBox4.CheckState = CheckState.Unchecked;
                checkBox4.Enabled = false;
            }
            if (checkBox5.Checked == true)
            {
                ch[4] = false;
                checkBox5.CheckState = CheckState.Unchecked;
                checkBox5.Enabled = false;
            }            
        }

        private void Open_Click(object sender, EventArgs e)
        {
            OpenFileDialog DirDialog = new OpenFileDialog();
            DirDialog.Filter = "Spectr files | *.spctr";
            if (DirDialog.ShowDialog() == DialogResult.OK)
            {
                string filename = DirDialog.FileName;
                BinaryReader file = new BinaryReader(File.Open(filename, FileMode.Open));

                for (int i = 0; i < 5; i++)
                {
                    if (ch[i] == false)
                    {
                        ch[i] = true;
                        for (int j = 0; j < Ys2.Length; j++)
                        {
                            spectrs[i, j] = file.ReadDouble();
                        }
                        break;
                    }
                }

                if (ch[0] == true)
                {
                    checkBox1.Enabled = true;
                }
                if (ch[1] == true)
                {
                    checkBox2.Enabled = true;
                }
                if (ch[2] == true)
                {
                    checkBox3.Enabled = true;
                }
                if (ch[3] == true)
                {
                    checkBox4.Enabled = true;
                }
                if (ch[4] == true)
                {
                    checkBox5.Enabled = true;
                }

                file.Close();
            }
            else
            {
                return;
            }
            
        }

        private void Start_Click(object sender, EventArgs e)
        {
            visible = true;        
        }
        

        private void timer1_Tick(object sender, EventArgs e)
        {
            UpdateAudioGraph();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }
        
    }
}
