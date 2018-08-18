using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace NumberPlateDetect
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private Mat[] _templateNumeric = new Mat[10]
        {
            new Mat(@"0.png", ImreadModes.GrayScale),
            new Mat(@"1.png", ImreadModes.GrayScale),
            new Mat(@"2.png", ImreadModes.GrayScale),
            new Mat(@"3.png", ImreadModes.GrayScale),
            new Mat(@"4.png", ImreadModes.GrayScale),
            new Mat(@"5.png", ImreadModes.GrayScale),
            new Mat(@"6.png", ImreadModes.GrayScale),
            new Mat(@"7.png", ImreadModes.GrayScale),
            new Mat(@"8.png", ImreadModes.GrayScale),
            new Mat(@"9.png", ImreadModes.GrayScale),
            //new Mat(@"hyphen.png", ImreadModes.GrayScale),
            //new Mat(@"dot.png", ImreadModes.GrayScale),
        };

        private Scalar[] _colors = new Scalar[10]
        {
            new Scalar(255, 0, 0),
            new Scalar(0, 255, 0),
            new Scalar(0, 0, 255),
            new Scalar(255, 255, 0),
            new Scalar(255, 0, 255),
            new Scalar(0, 255, 255),
            new Scalar(127, 0, 0),
            new Scalar(0, 127, 0),
            new Scalar(0, 0, 127),
            new Scalar(127, 127, 0),
            //new Scalar(0, 127, 127),
            //new Scalar(127, 127, 127),
        };

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    LoadImage(files[0]);
                }));
            }
        }

        private void LoadImage(string filePath)
        {
            Mat src_img = new Mat(filePath, ImreadModes.GrayScale);
            Mat src_img2 = new Mat(filePath);

            Parallel.For(0, _templateNumeric.Length, i =>
            {
                Detect(ref src_img, ref src_img2, i);
            });

            //for (int i = 0; i < _templateNumeric.Length; i++)
            //{
            //    Detect(ref src_img, ref src_img2, i);
            //}

            BitmapImage img = new BitmapImage();
            img.BeginInit();
            img.StreamSource = src_img2.ToMemoryStream();
            img.EndInit();
            Image_View.Source = img;

            src_img.Dispose();
            src_img2.Dispose();
        }

        private void Detect(ref Mat src_img, ref Mat src_img2, int index)
        {
            Mat dst_img = new Mat();

            List<float> Xpoint = new List<float>();
            List<float> Ypoint = new List<float>();

            //空Matに全座標の比較データ（配列）を格納
            Cv2.MatchTemplate(src_img, _templateNumeric[index], dst_img, TemplateMatchModes.CCoeffNormed);

            //比較データ(配列)のうち、しきい値0.7以下を排除(0)にする
            Cv2.Threshold(dst_img, dst_img, 0.70, 1.0, ThresholdTypes.Tozero);

            //0以上の座標データをXpoint Ypointに格納する
            for (int x = 0; x < dst_img.Rows; x++)
            {
                for (int y = 0; y < dst_img.Cols; y++)
                {
                    if (dst_img.At<float>(x, y) > 0)
                    {
                        Xpoint.Add(y);
                        Ypoint.Add(x);
                    }

                }
            }

            dst_img.Dispose();

            List<OpenCvSharp.Rect> rectangles = new List<OpenCvSharp.Rect>();
            for (int i = 0; i < Xpoint.Count; i++)
            {
                OpenCvSharp.Rect rect = new OpenCvSharp.Rect(new OpenCvSharp.Point(Xpoint[i], Ypoint[i]), _templateNumeric[index].Size());
                bool isIntersect = false;

                foreach (OpenCvSharp.Rect r in rectangles)
                {
                    if (isIntersect = r.IntersectsWith(rect)) break;
                };

                if (isIntersect) continue;

                rectangles.Add(rect);

                Cv2.Rectangle(src_img2, rect, _colors[index],2);

                Cv2.PutText(src_img2, index.ToString(), rect.Location, HersheyFonts.HersheySimplex, 1, _colors[index],2);
            }
        }
    }
}
