using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ImageRecognizerNamespace;
using Newtonsoft.Json;

namespace ViewModel
{
    public class ImageSerealization
    {
        public string Pixels { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public List<ObjectBox> ObjectBoxes { get; set; }
        public string Filename { get; set; }

        public ImageSerealization((Image<Rgb24> image, List<ObjectBox> objects, string filename) result)
        {
            Filename = result.filename;
            if (result.objects == null || result.objects.Count() == 0)
            {
                Pixels = string.Empty;
                Height = 0;
                Width = 0;
                ObjectBoxes = new List<ObjectBox>();
            }
            else
            {
                Image<Rgb24> image =result.image;
                byte[] bytePixels = new byte[image.Width * image.Height * Unsafe.SizeOf<Rgb24>()];
                image.CopyPixelDataTo(bytePixels);
                Pixels = Convert.ToBase64String(bytePixels);
                Height = image.Height;
                Width = image.Width;
                if (Height <= 0 || Width <= 0)
                {
                    throw new Exception($"Height: {Height}, Width: {Width}");
                }
                ObjectBoxes = result.objects;
            }
        }

    }
    public class HistoryStorage
    {
        private string Path { get; set; }
        private List<ImageSerealization> Images;

        public int Count
        {
            get => Images.Count;
        }

        public HistoryStorage(string path = "History.json")
        {
            Path = path;
            Images = new List<ImageSerealization>();
        }

        public void Erase()
        {
            Images.Clear();
            if (File.Exists(Path))
                File.Delete(Path);
        }

        public void Load()
        {
            if (!File.Exists(Path))
                return;
            var images = JsonConvert.DeserializeObject<List<ImageSerealization>>(File.ReadAllText(Path));
            if (images != null)
                Images = images;
            else
                Images = new List<ImageSerealization>();
        }

        public void Save()
        {
            string tmpPath = Path + ".tmp";
            string serialized = JsonConvert.SerializeObject(Images, Formatting.Indented);
            using (StreamWriter writer = new StreamWriter(tmpPath))
            {
                writer.WriteLine(serialized);
            }
            if (File.Exists(tmpPath))
            {
                File.Delete(Path);
                File.Copy(tmpPath, Path);
                if (File.Exists(Path))
                    File.Delete(tmpPath);
            }
        }

        public void AddElement(ImageSerealization image)
        {
            if(!Images.Contains(image))
                Images.Add(image);
        }

        public IEnumerable<ImageSerealization> GetImagePresentations()
        {
            return Images.Select(x => x);
        }
    }
}
