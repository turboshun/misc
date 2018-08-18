using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GuideDogDetect
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private Thread _recvThread;
        private Thread _playThread;
        private Thread _judgmentThread;

        private IplImage _iplImage = null;
        private IplImage _iplImage2 = null;

        private object lk1 = new object();
        private object lk2 = new object();

        public MainWindow()
        {
            InitializeComponent();

            _judgmentThread = new Thread(JudgmentThread);
            _judgmentThread.Start();

            _playThread = new Thread(PlayThread);
            _playThread.Start();

            _recvThread = new Thread(RecvThread);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _recvThread.Abort();
            _playThread.Abort();
            _judgmentThread.Abort();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                if (_recvThread.IsAlive)
                {
                    _recvThread.Abort();
                }
                _recvThread = new Thread(RecvThread);
                _recvThread.Start(files[0]);
            }
        }

        private void RecvThread(object o)
        {
            using (var capture = new CvCapture((string)o))
            {
                Stopwatch sw = new Stopwatch();

                sw.Start();

                while (capture.GrabFrame() > 0)
                {
                    var image = capture.RetrieveFrame();
                    if (image == null) break;

                    lock (lk1)
                    {
                        _iplImage = image.Clone();
                    }

                    lock (lk2)
                    {
                        //if (image.Width > 640)
                        //{
                        //    double ratio = image.Width / 640;
                        //    _iplImage2 = new IplImage((int)(image.Width * ratio), (int)(image.Height * ratio), BitDepth.U8, 3);
                        //}
                        //else
                        //{
                        //    _iplImage2 = image.EmptyClone();
                        //}
                        //image.Resize(_iplImage2);
                        _iplImage2 = image.Clone();
                    }

                    image.Dispose();

                    sw.Stop();

                    Thread.Sleep(Math.Max((int)(1000 / capture.Fps) - (int)sw.ElapsedMilliseconds, 1));

                    sw.Restart();
                }

                sw.Stop();
            }
        }

        private void PlayThread()
        {
            while (true)
            {
                if (_iplImage == null)
                {
                    Thread.Sleep(10);
                    continue;
                }

                byte[] buf = null;
                lock (lk1)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        JpegBitmapEncoder jbe = new JpegBitmapEncoder();

                        jbe.Frames.Add(BitmapFrame.Create(_iplImage.ToBitmap().ToBitmapSource()));
                        jbe.Save(ms);

                        buf = ms.GetBuffer();
                    }

                    _iplImage = null;
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    try
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = new MemoryStream(buf);
                        bitmap.EndInit();
                        bitmap.Freeze();
                        Image_Photo.Source = bitmap;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }));
            }
        }

        private void JudgmentThread()
        {
            byte[] image = null;

            while (true)
            {
                if (_iplImage2 == null)
                {
                    Thread.Sleep(10);
                    continue;
                }

                lock (lk2)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        JpegBitmapEncoder jbe = new JpegBitmapEncoder();

                        jbe.Frames.Add(BitmapFrame.Create(_iplImage2.ToBitmap().ToBitmapSource()));
                        jbe.Save(ms);

                        image = ms.GetBuffer();
                    }

                    _iplImage2 = null;
                }

                try
                {
                    string filePath = Path.GetTempFileName();

                    using (FileStream fs = File.Create(filePath))
                    {
                        fs.Write(image, 0, image.Length);
                    }

                    ProcessStartInfo psInfo = new ProcessStartInfo();
                    psInfo.FileName = @"python"; // 実行するファイル
                    psInfo.Arguments = string.Format("classify_image.py --image_file {0}", filePath);
                    psInfo.CreateNoWindow = true; // コンソール・ウィンドウを開かない
                    psInfo.UseShellExecute = false; // シェル機能を使用しない
                    psInfo.RedirectStandardOutput = true;

                    Process p = Process.Start(psInfo);
                    string output = p.StandardOutput.ReadToEnd(); // 標準出力の読み取り
                    p.WaitForExit();
                    p.Close();

                    bool isDetect
                        = (output.IndexOf("retriever", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        output.IndexOf("guide dog", StringComparison.OrdinalIgnoreCase) >= 0);

                    Dispatcher.Invoke(new Action(delegate
                    {
                        try
                        {
                            BitmapImage bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = new MemoryStream(image);
                            bitmap.EndInit();
                            bitmap.Freeze();
                            Image_Photo2.Source = bitmap;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                        TextBlock_Message.Text = (isDetect ? "盲導犬がいる" : "盲導犬はいない");
                    }));

                    File.Delete(filePath);
                }
                catch { }
            }
        }
    }
}
