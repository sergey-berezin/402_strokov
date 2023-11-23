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
        string[]? OpenFilesDialog();
    }
    public class ImageViewModel
    {
        public BitmapSource SelectedImage
        {
            get
            {
                var selectedImage = SelectedImageRef.Target;
                if (selectedImage == null)
                {
                    var mainImage = ImageToBitmapSource(ImageRecognizer.Annotate(Image, Objects));
                    SelectedImageRef = new WeakReference(mainImage);
                    return mainImage;
                }
                else
                {
                    return (BitmapSource)selectedImage;
                }
            }
        }
        private BitmapSource ImageToBitmapSource(Image<Rgb24> image)
        {
            byte[] pixels = new byte[image.Width * image.Height * Unsafe.SizeOf<Rgb24>()];
            image.CopyPixelDataTo(pixels);

            return BitmapFrame.Create(image.Width, image.Height, 96, 96, PixelFormats.Rgb24, null, pixels, 3 * image.Width);
        }
        Image <Rgb24> Image { get; set; }
        private WeakReference SelectedImageRef { get; set; }
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
            SelectedImageRef = new WeakReference(null);

            Image = image;
            Objects = objects;
        }
    }
    public class ViewModelClass : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] String propertyName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public CancellationTokenSource cts { get; set; }
        public ImageRecognizer? imageRecognizer { get; set; }
        public ObservableCollection<Image<Rgb24>> Images { get; set; }
        public ObservableCollection<string> Paths { get; set; }
        public ICommand LoadCommand { get; private set; }
        public ICommand RunCommand { get; private set; }//start command
        public ICommand StopCommand { get; private set; }
        public ICommand ClearCommand { get; private set; }

        public ObservableCollection<ImageViewModel> imageViews { get; set; }

        private readonly IUIServices uiServices;

        private bool is_detecting = false;
        public async Task Detect(object arg)
        {
            is_detecting = true;
            imageViews.Clear();
            RaisePropertyChanged(nameof(imageViews));
            try
            {
                cts = new CancellationTokenSource();

                if (imageRecognizer == null)
                    imageRecognizer = await ImageRecognizer.Create();
                List<Task<(Image<Rgb24>, List<ObjectBox>, string)>> tasks = new();

                foreach (var p in Images.Zip(Paths))
                    tasks.Add(DetectAsync(p.First, p.Second));

                async Task<(Image<Rgb24>, List<ObjectBox>, string)> DetectAsync(Image<Rgb24> image, string path)
                {
                    return (image, await ImageRecognizer.FindAsync(path, cts), path);
                }

                int previousLength = Storage.Count;
                while (tasks.Count > 0)
                {
                    var task = await Task.WhenAny(tasks);
                    var result = task.Result;
                    tasks.Remove(task);
                    imageViews.Add(new ImageViewModel(result.Item1, result.Item2.Count, result.Item3, result.Item2));
                    Storage.AddElement(new ImageSerealization(result));
                    RaisePropertyChanged(nameof(imageViews));
                }
                if (Storage.Count > previousLength)
                    Storage.Save();
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

        public void ReportError(string errorMassage)
        {
            MessageBox.Show(errorMassage);
        }

        public void Stop(object arg)
        {
            cts.Cancel();
        }
        public void LoadImages(object arg)
        {
            try
            {
                string[]? files = uiServices.OpenFilesDialog();
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

        private HistoryStorage Storage;
        public void OnClearHistory(object arg)
        {
            Storage.Erase();
            imageViews.Clear();
            RaisePropertyChanged(nameof(imageViews));
        }
        public ViewModelClass(IUIServices uiServices)
        {
            imageViews = new();
            imageRecognizer = null;
            Storage = new HistoryStorage(); 
            Storage.Load();
            foreach (var imageSerialization in Storage.GetImagePresentations())
            {
                //такой кривой способ добавить картинки в список
                imageViews.Add(new ImageViewModel(Image.Load<Rgb24>(imageSerialization.Filename), imageSerialization.ObjectBoxes.Count,imageSerialization.Filename,imageSerialization.ObjectBoxes));
                RaisePropertyChanged(nameof(imageViews));
            }


            LoadCommand = new RelayCommand(LoadImages, x => !is_detecting);
            RunCommand = new AsyncRelayCommand(Detect, x => !is_detecting && (Paths != null));
            StopCommand = new RelayCommand(Stop, x => is_detecting);
            ClearCommand = new RelayCommand(OnClearHistory, x => !is_detecting);
            this.uiServices = uiServices;
        }
    }
}