using OpenCvSharp;
using OpenCvSharp.CPlusPlus;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CameraUndistortion
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private UndistortionOmnidirectionalImage _camera = new UndistortionOmnidirectionalImage();
        private bool _isFirst = true;

        private IplImage _src = null;
        private object lockobj = new object();
        private Thread _recvThread;
        private Thread _renderThread;
        private double _inputFPS = 0;
        private double _outputFPS = 0;

        private int _panoramaImageWidth;
        private int _originalImageWidth;

        public MainWindow()
        {
            InitializeComponent();

            _renderThread = new Thread(RenderThread);
            _renderThread.Start();

            _recvThread = new Thread(RecvThread);
            _recvThread.Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _panoramaImageWidth = (int)Image_Panorama.Width;
            _originalImageWidth = (int)Image_Original.Width;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _recvThread.Abort();
            _renderThread.Abort();
        }

        private void RecvThread()
        {
            //using (var capture = new CvCapture("rtsp://admin:admin@10.2.14.51/video1"))
            {
                Stopwatch sw = new Stopwatch();
                int count = 0;

                sw.Start();
                while (true)
                {
                    // フレーム画像を非同期に取得
                    var image = Cv.LoadImage("test.png");
                    //capture.GrabFrame();
                    //var image = capture.RetrieveFrame();
                    if (image == null) break;

                    lock (lockobj)
                    {
                        if (_src != null)
                        {
                            _src.Dispose();
                        }
                        //_src = new IplImage(500,500, BitDepth.U8, 3);
                        //image.Resize(_src);
                        _src = image.Clone();
                    }
                    image.Dispose();

                    count++;
                    if (sw.ElapsedMilliseconds >= 1000)
                    {
                        sw.Stop();
                        _inputFPS = count * (1000.0 / sw.ElapsedMilliseconds);
                        count = 0;
                        sw.Restart();
                    }

                }
            }
        }

        private void RenderThread()
        {
            Stopwatch sw = new Stopwatch();
            int count = 0;

            sw.Start();

            while (true)
            {
                if (_src == null)
                {
                    Thread.Sleep(10);
                    continue;
                }

                IplImage src = null;

                lock (lockobj)
                {
                    src = _src.Clone();
                    _src.Dispose();
                    _src = null;
                }

                if (_isFirst)
                {
                    _isFirst = false;

                    Dispatcher.Invoke(new Action(delegate
                    {
                        Slider_CenterX.Minimum = 0;
                        Slider_CenterX.Maximum = src.Width;
                        Slider_CenterX.Value = src.Width / 2;

                        Slider_CenterY.Minimum = 0;
                        Slider_CenterY.Maximum = src.Height;
                        Slider_CenterY.Value = src.Height / 2;

                        Slider_R2.Minimum = 1;
                        Slider_R2.Maximum = Math.Min(src.Width, src.Height) / 2;
                        Slider_R2.Value = Math.Min(src.Width, src.Height) / 2;

                        Slider_R1.Minimum = 0;
                        Slider_R1.Maximum = Math.Min(src.Width, src.Height) / 2 - 1;
                        Slider_R1.Value = Math.Min(src.Width, src.Height) / 4;
                    }));
                }

                Dispatcher.Invoke(new Action(delegate
                {
                    _camera.Center = new OpenCvSharp.CPlusPlus.Point(Slider_CenterX.Value, Slider_CenterY.Value);
                    _camera.R1 = (float)Slider_R1.Value;
                    _camera.R2 = (float)Slider_R2.Value;
                    _camera.OffsetTh = (float)(Slider_OffsetTH.Value / 180.0 * Math.PI);
                }));

                _camera.Undistortion(src, out IplImage dst, _panoramaImageWidth, out IplImage guide, _originalImageWidth);

                MemoryStream msx = new MemoryStream();
                dst.ToStream(msx, ".bmp");
                byte[] imageData = ImageCompleted(msx);

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    Grid_SettingArea.IsEnabled = true;

                    try
                    {
                        MemoryStream ms = new MemoryStream();
                        guide.ToStream(ms, ".bmp");
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();

                        Image_Original.Source = bitmap;
                    }
                    catch { }

                    try
                    {

                        MemoryStream ms2 = new MemoryStream(imageData);
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms2;
                        bitmap.EndInit();

                        Image_Panorama.Source = bitmap;
                    }
                    catch { }

                    //try
                    //{
                    //    MemoryStream ms = new MemoryStream();
                    //    dst.ToStream(ms, ".bmp");
                    //    BitmapImage bitmap = new BitmapImage();
                    //    bitmap.BeginInit();
                    //    bitmap.StreamSource = ms;
                    //    bitmap.EndInit();

                    //    Image_Panorama.Source = bitmap;
                    //}
                    //catch { }

                    src.Dispose();
                    guide.Dispose();
                    dst.Dispose();

                    count++;
                    if (sw.ElapsedMilliseconds >= 1000)
                    {
                        sw.Stop();
                        _outputFPS = count * (1000.0 / sw.ElapsedMilliseconds);
                        count = 0;
                        sw.Restart();
                    }

                    Label_InputFPS.Content = _inputFPS.ToString("F2");
                    Label_OutputFPS.Content = _outputFPS.ToString("F2");
                }));
            }
        }

        private byte[] ImageCompleted(MemoryStream ms)
        {
            Mat src = OpenCvSharp.Extensions.BitmapConverter.ToMat(new System.Drawing.Bitmap(ms));
            Mat gray = new Mat();
            CascadeClassifier haarCascade = new CascadeClassifier("./haarcascade_frontalface_default.xml");
            var result = src.Clone();

            Cv2.CvtColor(src, gray, ColorConversion.BgrToGray);

            // 顔検出
            OpenCvSharp.CPlusPlus.Rect[] faces = haarCascade.DetectMultiScale(
                gray);

            // 検出した顔の位置に円を描画
            foreach (OpenCvSharp.CPlusPlus.Rect face in faces)
            {
                var center = new OpenCvSharp.CPlusPlus.Point
                {
                    X = (int)(face.X + face.Width * 0.5),
                    Y = (int)(face.Y + face.Height * 0.5)
                };
                var axes = new OpenCvSharp.CPlusPlus.Size
                {
                    Width = (int)(face.Width * 0.5),
                    Height = (int)(face.Height * 0.5)
                };
                Cv2.Ellipse(result, center, axes, 0, 0, 360, new Scalar(255, 0, 255), 2);
            }

            System.Drawing.Bitmap dstBitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(result);

            byte[] imageData = null;
            using (MemoryStream ms2 = new MemoryStream())
            {
                dstBitmap.Save(ms2, System.Drawing.Imaging.ImageFormat.Jpeg);
                imageData = ms2.GetBuffer();
            }

            return imageData;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Slider slider = sender as Slider;
            slider.Value = (int)slider.Value;

            if (slider == Slider_R1 && Slider_R1.Value >= Slider_R2.Value)
            {
                if (Slider_R2.Value == Slider_R2.Maximum)
                    Slider_R1.Value--;
                else
                    Slider_R2.Value++;
            }
            else if (slider == Slider_R2 && Slider_R1.Value >= Slider_R2.Value)
            {
                if (Slider_R2.Value == 0)
                    Slider_R2.Value = 1;
                else
                    Slider_R1.Value--;
            }
        }
    }
}
