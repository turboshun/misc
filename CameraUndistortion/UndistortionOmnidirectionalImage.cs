using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CameraUndistortion
{
    public class UndistortionOmnidirectionalImage
    {
        private CvPoint _center = CvPoint.Empty;
        public CvPoint Center
        {
            set
            {
                _isUpdate = true;
                _center = value;
            }
        }
        private float _r1 = float.NaN;
        public float R1
        {
            set
            {
                if (value < 0.0f) return;
                _isUpdate = true;
                _r1 = value;
            }
        }
        private float _r2 = float.NaN;
        public float R2
        {
            set
            {
                if (value < 1.0f) return;
                _isUpdate = true;
                _r2 = value;
            }
        }
        private float _offsetTh = 0.0f;
        public float OffsetTh
        {
            set
            {
                _isUpdate = true;
                _offsetTh = value;
            }
        }

        private bool _isFill
        {
            get
            {
                return (_inputSize != CvSize.Empty &&
                    _outputSize != CvSize.Empty &&
                    _center != CvPoint.Empty &&
                    !float.IsNaN(_r1) &&
                    !float.IsNaN(_r2));
            }
        }

        private CvSize _inputSize = CvSize.Empty;
        private CvSize _outputSize = CvSize.Empty;

        private bool _isUpdate = true;


        private Dictionary<int, CvPoint2D32f> _pointDictionary = new Dictionary<int, CvPoint2D32f>();

        public bool Undistortion(IplImage src, out IplImage dst, int panoramaImageWidth, out IplImage guide, int originalImageWidth)
        {
            dst = null;
            guide = null;
            return (Undistortion(src, out dst, panoramaImageWidth) && DrawGuideline(src, out guide, originalImageWidth));
        }

        public bool Undistortion(IplImage src, out IplImage dst, int panoramaImageWidth)
        {
            dst = null;

            if (src == null)
                return false;

            IplImage dstTemp = new IplImage(panoramaImageWidth, (int)Math.Max((panoramaImageWidth / 4 * (_r2 - _r1) / _r2), 1), BitDepth.U8, 3);
            bool isUpdate = false;

            if (isUpdate = (_inputSize != src.Size || _outputSize != dstTemp.Size))
            {
                _inputSize = src.Size;
                _outputSize = dstTemp.Size;
                _pointDictionary.Clear();
            }

            object lockobj = new object();
            ParallelOptions opt = new ParallelOptions()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };
            Parallel.For(0, dstTemp.Height, opt, i =>
            {
                IplImage patch = new IplImage(1, 1, BitDepth.U8, 3);

                for (int x = 0; x < dstTemp.Width; ++x)
                {
                    if (isUpdate || _isUpdate)
                    {
                        lock (lockobj)
                        {
                            _pointDictionary[i * dstTemp.Width + x] = ConvertPolar(x, i);
                        }
                    }

                    Cv.GetRectSubPix(src, patch, _pointDictionary[i * dstTemp.Width + x]);
                    dstTemp.Set2D(dstTemp.Height - i - 1, dstTemp.Width - x - 1, patch.Get2D(0, 0));
                }

                patch.Dispose();
            });

            _isUpdate = false;

            dst = dstTemp.Clone();
            dstTemp.Dispose();

            return true;
        }

        public bool DrawGuideline(IplImage src, out IplImage guide, int originalImageWidth)
        {
            guide = null;

            if (src == null || !_isFill)
                return false;

            guide = new IplImage(originalImageWidth, (int)((double)originalImageWidth / _inputSize.Width * _inputSize.Height), BitDepth.U8, 3);

            IplImage srcTemp = src.Clone();
            double ratio = Math.Min(src.Width, src.Height) / Math.Min(guide.Width, guide.Height);

            Cv.Circle(srcTemp, _center, ((int)ratio * 3), new CvScalar(0, 255, 0), -1);
            Cv.Circle(srcTemp, _center, (int)_r1, new CvScalar(255, 0, 0), ((int)ratio * 2));
            Cv.Circle(srcTemp, _center, (int)_r2, new CvScalar(0, 0, 255), ((int)ratio * 2));

            CvPoint2D32f p1 = ConvertPolar(0, 0);
            CvPoint2D32f p2 = ConvertPolar(0, _outputSize.Height);
            Cv.Line(srcTemp, p1, p2, new CvScalar(0, 255, 0), ((int)ratio * 2));

            srcTemp.Resize(guide, Interpolation.Cubic);
            srcTemp.Dispose();

            return true;
        }

        public CvPoint2D32f ConvertPolar(int x, int y)
        {
            CvPoint2D32f n = new CvPoint2D32f(x / (float)_outputSize.Width, y / (float)_outputSize.Height);
            float rRange = _r2 - _r1;
            float r = _r1 + rRange * (1.0f - n.Y);
            float th = (2.0f * (float)Math.PI) * (1.0f - n.X) + _offsetTh;

            return new CvPoint2D32f(r * (float)Math.Sin(th) + _center.X, r * (float)Math.Cos(th) + _center.Y);
        }
    }
}
