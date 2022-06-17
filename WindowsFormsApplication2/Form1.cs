using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;
using System.IO;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Xna.Framework.Audio;
using NAudio.Wave;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

/*
 * puerto de recepcion de imagen 55900
 * puerto de recepcion de audio 55901
 */

namespace WindowsFormsApplication2
{
    public partial class Form1 : Form
    {
        private Thread recepcion_imagen_th, recepcion_audio_th, audio_;
        private int puerto_imagen=55900, puerto_audio=55901;
        private bool ExistenDispositivosImagen = false , ExistenDispostivosAudio;
        private FilterInfoCollection DispositivosDeVideo;
        private VideoCaptureDevice FuenteDeVideo = null;
        private Socket socket_de_envio_imagen = new Socket(AddressFamily.InterNetwork, SocketType.Dgram,ProtocolType.Udp);
        private Socket socket_de_recepcion_imagen = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        private Socket socket_de_envio_audio = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        private Socket socket_de_recepcion_audio = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        //private IPAddress address_destino = IPAddress.Parse("192.168.2.*");
        WinSound.Repeater repeaterOne = new WinSound.Repeater();
        IPAddress address_local ;
        private IPAddress address_remota;
        EndPoint ip_local= null;
        UdpClient listener = null, listener1=null;
        private IPEndPoint ipendpoint;
        private EndPoint ip_remota ;
        private byte[] envio_buffer_imagen; //128 para hacer pruebas
        private byte[] recepcion_buffer_imagen;//128 para hacer pruebas
        public byte[] envio_buffer_audio;
        private byte[] recepcion_buffer_audio;
        public WinSound.Recorder audio_recorder = new WinSound.Recorder();
        public WinSound.Player audio_play = new WinSound.Player();
        List<WaveInCapabilities> sources = null;
        WaveRecorder bu ;
        private int buffersize_audio = 8192;
        private int samplepersecond = 44100;
        private int bufferCount = 8;
        private int bitperSample = 16;
        private int channel = 2;
        private String audioIn,audioOut;
        WaveIn sourceStream = null;

        DirectSoundOut Out = null;
        
        public Form1()
        {
            InitializeComponent();
            BuscarDispositivos();
            audio_recorder.DataRecorded += new WinSound.Recorder.DelegateDataRecorded(OnDataRecorded);
            audio_play.Open("Realtek", 8000, 16, 2, 8);
            // audio_recorder.RecordingStopped += new WinSound.Recorder.DelegateStopped(OnRecordingStopped);
            //Out.Play();
        }

        //Called, when datas are incoming
        private void OnDataRecorded(Byte[] data)
        {
            envio_buffer_audio = data;
            //Console.WriteLine(envio_buffer_audio.ToString());
        }

        //Called, when recording has stopped
        private void OnRecordingStopped()
        {
           
        }

        public void BuscarDispositivos()
        {
            //Imagen
            DispositivosDeVideo = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (DispositivosDeVideo.Count == 0)
                ExistenDispositivosImagen = false;
            else
            {
                ExistenDispositivosImagen = true;
                CargarDispositivos(DispositivosDeVideo);
            }
            //Sonido
            sources = new List<NAudio.Wave.WaveInCapabilities>();
            if (WaveIn.DeviceCount == 0)
                ExistenDispostivosAudio = false;
            else
                ExistenDispostivosAudio = true;
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                sources.Add(NAudio.Wave.WaveIn.GetCapabilities(i));
                comboBox2.Items.Add(sources[i].ProductName);
            }
            comboBox2.Text = comboBox2.Items[0].ToString();
        }

        public void CargarDispositivos(FilterInfoCollection Dispositivos)
        {
            for (int i = 0; i < Dispositivos.Count; i++)
                 comboBox1.Items.Add(Dispositivos[i].Name.ToString());
            comboBox1.SelectedIndex = 0;
        }

        
        public void TerminarFuenteDeVideo()
        {                
            if (!(FuenteDeVideo == null))
                if (FuenteDeVideo.IsRunning)
                { FuenteDeVideo.SignalToStop();
                    imagen_this.Image = null;
                    FuenteDeVideo = null;
                }
        }

        private void video_NuevoFrame(object sender, NewFrameEventArgs eventArgs)
        {
            BinaryFormatter bin = new BinaryFormatter();
            MemoryStream aux = new MemoryStream() ;
            Bitmap Imagen_My = (Bitmap)eventArgs.Frame.Clone();
            Imagen_My.Save(aux,ImageFormat.Jpeg);
            imagen_this.Image = Imagen_My;
            envia_imagen(aux);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button2.Enabled = true;
            address_remota = IPAddress.Parse(IpRemota.Text);
            if (ExistenDispositivosImagen && ExistenDispostivosAudio)
            {
                FuenteDeVideo = new VideoCaptureDevice(DispositivosDeVideo[comboBox1.SelectedIndex].MonikerString);
                FuenteDeVideo.NewFrame += new NewFrameEventHandler(video_NuevoFrame);
                FuenteDeVideo.Start();
                escucha_audio();
                comboBox1.Enabled = false;
                audio_ = new Thread(new ThreadStart(escucha_audio));
                recepcion_imagen_th = new Thread(new ThreadStart(escucha_imagen));
                recepcion_imagen_th.Start();
                audioIn = comboBox2.SelectedItem.ToString();
                audioOut = comboBox4.SelectedItem.ToString();
                recepcion_audio_th = new Thread(new ThreadStart(reproducir_audio));
                recepcion_audio_th.Start();
                audio_.Start();
            }
            else
            {
                if(ExistenDispositivosImagen == false)
                    MessageBox.Show("Error: No se encuentra ningun dispositivo de Video.");
                else if (ExistenDispostivosAudio == false)
                    MessageBox.Show("Error: No se encuentra ningun dispositivo de Aideo.");
                else
                    MessageBox.Show("Error: No se encuentra ningun dispositivo.");
            }
            button1.Enabled = false;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            String strHostName = Dns.GetHostName();
            address_local = (Dns.Resolve(strHostName)).AddressList[0];
            MessageBox.Show(address_local.ToString());
            comboBox4.DataSource = WinSound.WinSound.GetPlaybackNames();
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            TerminarFuenteDeVideo();
            //if (socket_de_recepcion_imagen.IsBound)
            //{
            if(recepcion_imagen_th != null)
                //if(!recepcion_imagen_th.IsAlive)
                    recepcion_imagen_th.Abort();
            if(socket_de_recepcion_imagen!=null)
                //if(socket_de_recepcion_imagen.IsBound)
                    socket_de_recepcion_imagen.Close();
            if(listener!=null)
                listener.Close();
            if(audio_!=null)
                        audio_.Abort();

            
            //}
        }

        private void button2_Click(object sender, EventArgs e)
        {
            button1.Enabled = true;
            button2.Enabled = false;
            comboBox1.Enabled = true;
            TerminarFuenteDeVideo();
            audio_.Abort();
            imagen_other.Image = null;
            recepcion_imagen_th.Abort();
            socket_de_recepcion_imagen.Close();
            listener.Close();
            audio_recorder.Stop();
        }

        private void escucha_imagen()
        {
            ipendpoint = new IPEndPoint(IPAddress.Any, puerto_imagen);
            ip_remota = new IPEndPoint(address_remota,puerto_imagen);
            listener = new UdpClient(ipendpoint);
            //socket_de_recepcion_imagen.Bind(ip_local);
            while(true)
            {
                recepcion_buffer_imagen = listener.Receive(ref ipendpoint);
                ImageConverter converImage = new ImageConverter();
                Image imagen_recivida = (Image)converImage.ConvertFrom(recepcion_buffer_imagen);
                imagen_other.Image = imagen_recivida;
            }
        }

        private void escucha_audio()
        {
            //while(true)
            //{
            //    string destino=null;
            //    int inicio=0, conteo=0;
            //    sourceStream = new WaveIn();
            //    //int numedeviceAudio = comboBox2.SelectedIndex;
            //    sourceStream = new WaveIn();
            //    sourceStream.DeviceNumber = 0;
            //    WaveInProvider waveIn = new WaveInProvider(sourceStream);
            //    sourceStream.StartRecording();
            //    //Thread.Sleep(1000);
            //    //sourceStream.StopRecording();
            //    //waveIn.Read(envio_buffer_imagen,inicio,conteo);
            //    DirectSoundOut waveout = new DirectSoundOut();
            //    waveout.Init(waveIn);
            //    //waveout.Volume = 1000;
            //    waveout.Play();
            //Thread.Sleep(1000);
            //    //waveout.Stop(=);
            //    //bu = new WaveRecorder(waveIn,destino);
            //}
            audio_recorder.Start("Line 1 (Virtual Audio Cable)", 8000, 16, 2, 8, 1024);
        }

        private void enviar_audio()
        {

        }
        private void reproducir_audio()
        {
            byte[] data = envio_buffer_audio;
            bool isOpen = audio_play.Opened;
            audio_play.PlayData(data, false);
            Thread.Sleep(1000);
            envio_buffer_audio = null;
            //audio_play.Stop();

        }

        private void recepcion_audio()
        {
            ipendpoint = new IPEndPoint(IPAddress.Any, puerto_audio);
            ip_remota = new IPEndPoint(address_remota, puerto_audio);
            listener1 = new UdpClient(ipendpoint);
            //socket_de_recepcion_imagen.Bind(ip_local);
            while (true)
            {
                recepcion_buffer_audio = listener1.Receive(ref ipendpoint);
                //recepcion de audio 
            }
        }

        private void envia_imagen(MemoryStream enviar)
        {
            ip_remota = new IPEndPoint(address_remota,puerto_imagen);
            envio_buffer_imagen = enviar.ToArray();
            socket_de_envio_imagen.Connect(ip_remota);
            socket_de_envio_imagen.SendTo(envio_buffer_imagen,ip_remota);
        }

    }
}