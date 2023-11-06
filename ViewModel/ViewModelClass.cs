using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageRecognizerNamespace;

namespace ViewModel
{
    public interface IUIServices
    {
        void OpenFilesDialog(object sender, RoutedEventArgs e);
        string[]? GetFileNames();
    }
    public class ImageViewModel
    {
        public BitmapSource SelectedImage
        {
            get
            {
                Image<Rgb24> image = ImageRecognizer.Annotate(Image, Objects);
                byte[] pixels = new byte[image.Width * image.Height * Unsafe.SizeOf<Rgb24>()];
                image.CopyPixelDataTo(pixels);

                return BitmapFrame.Create(image.Width, image.Height, 96, 96, PixelFormats.Rgb24, null, pixels, 3 * image.Width);
            }
        }

        Image<Rgb24> Image { get; set; }
        public BitmapImage Bitmap { get; set; }
        public int ObjectCount { get; set; }
        public string FileName { get; set; }
        List<ObjectBox> Objects { get; set; }
        public ImageViewModel(Image<Rgb24> image, int objectCount, string path, List<ObjectBox> objects)
        {
            Uri uri = new Uri(path, UriKind.RelativeOrAbsolute);
            Bitmap = new BitmapImage(uri);
            FileName = Path.GetFileName(path);
            ObjectCount = objectCount;

            Image = image;
            Objects = objects;
        }
    }
    //public class DetectedImageViewModel
    //{
    //    private IEnumerable<ObjectBox> BBoxes { get; set; }
    //    private Image<Rgb24> OriginalImage { get; }
    //    private WeakReference SelectedImageRef { get; set; }

    //    public BitmapSource SelectedImage
    //    {
    //        get
    //        {
    //            var selectedImage = SelectedImageRef.Target;
    //            if (selectedImage == null)
    //            {
    //                var mainImage = ImageToBitmapSource(ImageRecognizer.Annotate(OriginalImage, BBoxes));
    //                SelectedImageRef = new WeakReference(mainImage);
    //                return mainImage;
    //            }
    //            else
    //            {
    //                return (BitmapSource)selectedImage;
    //            }
    //        }
    //    }
    //    public BitmapSource Image { get; }
    //    public string Class { get; set; }
    //    public double Confidence { get; set; }

    //    public DetectedImageView(SegmentedObject segmentedObject)
    //    {
    //        BBoxes = segmentedObject.bbox;
    //        OriginalImage = segmentedObject.OriginalImage;
    //        SelectedImageRef = new WeakReference(null);
    //        Image = ImageToBitmapSource(segmentedObject.BoundingBox);
    //        Class = segmentedObject.Class;
    //        Confidence = segmentedObject.Confidence;
    //    }

    //    private BitmapSource ImageToBitmapSource(Image<Rgb24> image)
    //    {
    //        byte[] pixels = new byte[image.Width * image.Height * Unsafe.SizeOf<Rgb24>()];
    //        image.CopyPixelDataTo(pixels);

    //        return BitmapFrame.Create(image.Width, image.Height, 96, 96, PixelFormats.Rgb24, null, pixels, 3 * image.Width);
    //    }
    //}
    public class ViewModelClass : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] String propertyName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public CancellationTokenSource cts { get; set; }

        public ImageRecognizer? imageRecognizer { get; set; }
        public ObservableCollection<string> FileNames { get; set; }
        public ObservableCollection<Image<Rgb24>> Images { get; set; }
        public ObservableCollection<string> Paths { get; set; }
        public ICommand LoadCommand { get; private set; }
        public ICommand RunCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public ICommand ErrorCommand { get; private set; }

        public ObservableCollection<ImageViewModel> imageViews { get; set; }

        public string ErrorVisibility { get; set; }
        public string ErrorMessage { get; set; }

        private readonly IUIServices uiServices;

        public async Task Detect(object arg)
        {
            try
            {
                is_detecting = true;
                cts = new CancellationTokenSource();

                if (imageRecognizer == null)
                    imageRecognizer = await ImageRecognizer.Create();
                List<Task<(Image<Rgb24>, List<ObjectBox>, string)>> tasks = new();

                foreach (var p in Images.Zip(Paths))
                    tasks.Add(DetectAsync(p.First, p.Second));

                async Task<(Image<Rgb24>, List<ObjectBox>, string)> DetectAsync(Image<Rgb24> image, string path)
                {
                    var t = (image, await ImageRecognizer.FindAsync(path, cts));
                    return (t.Item1, t.Item2, path);
                }

                while (tasks.Count > 0)
                {
                    var task = await Task.WhenAny(tasks);
                    var result = task.Result;
                    tasks.Remove(task);
                    imageViews.Add(new ImageViewModel(result.Item1, result.Item2.Count, result.Item3, result.Item2));
                    RaisePropertyChanged(nameof(imageViews));
                }
            }
            catch (Exception ex)
            {
                ReportError(ex.Message);
            }
            finally
            {
                is_detecting = false;
            }

        }
        private bool is_detecting = false;

        public void ReportError(string errorMassage)
        {
            ErrorMessage = errorMassage;
            RaisePropertyChanged(nameof(ErrorMessage));
            ErrorVisibility = "Visible";
            RaisePropertyChanged(nameof(ErrorVisibility));
        }

        public void HideError(object arg)
        {
            ErrorVisibility = "Collapsed";
            RaisePropertyChanged(nameof(ErrorVisibility));
        }

        public void Stop(object arg)
        {
            cts.Cancel();
        }
        public void LoadImages(object arg)
        {
            try
            {
                string[]? files = uiServices.GetFileNames();
                if (files != null)
                {
                    Paths = new ObservableCollection<string>(files);
                    //RaisePropertyChanged(nameof(Filenames));
                    Images = ImageRecognizer.GetImages(Paths);
                }

            }
            catch (Exception ex)
            {
                ReportError(ex.Message);
            }
        }
        public ViewModelClass(IUIServices uiServices)
        {
            ErrorVisibility = "Collapsed";
            imageViews = new();
            imageRecognizer = null;
            LoadCommand = new RelayCommand(LoadImages, _ => !is_detecting);
            RunCommand = new AsyncRelayCommand(Detect);
            StopCommand = new RelayCommand(Stop, _ => is_detecting);
            ErrorCommand = new RelayCommand(HideError);
            this.uiServices = uiServices;
        }
    }
}