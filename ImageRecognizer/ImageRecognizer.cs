using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.Fonts;
using SixLabors.ImageSharp; // Из одноимённого пакета NuGet
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ImageRecognizerNamespace
{
    public class ImageRecognizer
    {
        public static async Task<ImageRecognizer> Create()
        {
            Task task = SetupONNXFileAsync();
            await task;
            return new ImageRecognizer();
        }

        public static string[] labels = new string[]
        {
            "aeroplane", "bicycle", "bird", "boat", "bottle",
            "bus", "car", "cat", "chair", "cow",
            "diningtable", "dog", "horse", "motorbike", "person",
            "pottedplant", "sheep", "sofa", "train", "tvmonitor"
        };
        //public static int MainAsync(string filename)
        public static async Task<List<ObjectBox>> FindAsync(string filename, CancellationTokenSource cts = null)
        {
            await SetupONNXFileAsync();

            if (!File.Exists(filename + ".jpg"))
            {
                throw new Exception("File doesn't exist");
            }
            ////////////////////////////////////////////////////////////////////////////////////////////

            using var image = Image.Load<Rgb24>(filename + ".jpg");

            // Размер изображения
            const int TargetSize = 416;

            // Изменяем размер изображения до 416 x 416
            var resized = image.Clone(x =>
            {
                x.Resize(new ResizeOptions
                {
                    Size = new Size(TargetSize, TargetSize),
                    Mode = ResizeMode.Pad // Дополнить изображение до указанного размера с сохранением пропорций
                });
            });

            // Перевод пикселов в тензор и нормализация
            var input = new DenseTensor<float>(new[] { 1, 3, TargetSize, TargetSize });
            resized.ProcessPixelRows(pa =>
            {
                for (int y = 0; y < TargetSize; y++)
                {
                    if (cts.Token.IsCancellationRequested)
                        return;

                    Span<Rgb24> pixelSpan = pa.GetRowSpan(y);
                    for (int x = 0; x < TargetSize; x++)
                    {
                        input[0, 0, y, x] = pixelSpan[x].R;
                        input[0, 1, y, x] = pixelSpan[x].G;
                        input[0, 2, y, x] = pixelSpan[x].B;
                    }
                }
            });

            // Подготавливаем входные данные нейросети. Имя input задано в файле модели
            var inputs = new List<NamedOnnxValue>
            {
               NamedOnnxValue.CreateFromTensor("image", input),
            };

            // Вычисляем предсказание нейросетью
            using var session = new InferenceSession("tinyyolov2-8.onnx");
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs);

            // Получаем результаты
            var outputs = results.First().AsTensor<float>();

            const int CellCount = 13; // 13x13 ячеек
            const int BoxCount = 5; // 5 прямоугольников в каждой ячейке
            const int ClassCount = 20; // 20 классов

            var anchors = new (double, double)[]
            {
               (1.08, 1.19),
               (3.42, 4.41),
               (6.63, 11.38),
               (9.42, 5.11),
               (16.62, 10.52)
            };

            int cellSize = TargetSize / CellCount;

            var boundingBoxes = resized.Clone();

            List<ObjectBox> objects = new();

            for (var row = 0; row < CellCount; row++)
            {
                if (cts.Token.IsCancellationRequested)
                    break;

                for (var col = 0; col < CellCount; col++)
                {
                    for (var box = 0; box < BoxCount; box++)
                    {
                        var rawX = outputs[0, (5 + ClassCount) * box, row, col];
                        var rawY = outputs[0, (5 + ClassCount) * box + 1, row, col];

                        var rawW = outputs[0, (5 + ClassCount) * box + 2, row, col];
                        var rawH = outputs[0, (5 + ClassCount) * box + 3, row, col];

                        var x = (float)((col + Sigmoid(rawX)) * cellSize);
                        var y = (float)((row + Sigmoid(rawY)) * cellSize);

                        var w = (float)(Math.Exp(rawW) * anchors[box].Item1 * cellSize);
                        var h = (float)(Math.Exp(rawH) * anchors[box].Item2 * cellSize);

                        var conf = Sigmoid(outputs[0, (5 + ClassCount) * box + 4, row, col]);

                        if (conf > 0.5)
                        {
                            var classes = Enumerable
                            .Range(0, ClassCount)
                            .Select(i => outputs[0, (5 + ClassCount) * box + 5 + i, row, col])
                            .ToArray();
                            objects.Add(new ObjectBox(x - w / 2, y - h / 2, x + w / 2, y + h / 2, conf, IndexOfMax(Softmax(classes))));

                        }

                        if (conf > 0.01)
                        {
                            boundingBoxes.Mutate(ctx =>
                            {
                                ctx.DrawPolygon(Pens.Solid(Color.Green, 1),
                                    new PointF[] {
                                        new PointF(x - w / 2, y - h / 2),
                                        new PointF(x + w / 2, y - h / 2),
                                        new PointF(x + w / 2, y + h / 2),
                                        new PointF(x - w / 2, y + h / 2)
                                    });
                            });
                        }
                    }
                }
            }

            if (cts.Token.IsCancellationRequested)
                return objects;

            boundingBoxes.Save("tmp/" + "boundingboxes.jpg");

            var annotated = resized.Clone();
            await AnnotateAsync(annotated, objects);

            await annotated.SaveAsJpegAsync("tmp/" + "annotated.jpg");

            // Убираем дубликаты
            for (int i = 0; i < objects.Count; i++)
            {
                var o1 = objects[i];
                for (int j = i + 1; j < objects.Count;)
                {
                    var o2 = objects[j];
                    Console.WriteLine($"IoU({i},{j})={o1.IoU(o2)}");
                    if (o1.Class == o2.Class && o1.IoU(o2) > 0.6)
                    {
                        if (o1.Confidence < o2.Confidence)
                        {
                            objects[i] = o1 = objects[j];
                        }
                        objects.RemoveAt(j);
                    }
                    else
                    {
                        j++;
                    }
                }
            }

            var final = resized.Clone();
            await AnnotateAsync(final, objects);
            await final.SaveAsJpegAsync("final.jpg");
            return objects;
        }
        private static async Task SetupONNXFileAsync() // загрузка весов нейросети
        {
            using (var client = new WebClient())
            {
                if (File.Exists("tinyyolov2-8.onnx"))
                    return;
                else
                    Write("Downloading model");
                int attempts = 0;
                while (true && (attempts < 10))
                {
                    try
                    {
                        await client.DownloadFileTaskAsync("https://storage.yandexcloud.net/dotnet4/tinyyolov2-8.onnx", "tinyyolov2-8.onnx");
                    }
                    catch (Exception ex)
                    {
                        Thread.Sleep(5000);
                        attempts++;
                        continue;
                    }
                    break;
                }
                if (attempts < 10)
                    return;
                else 
                    throw new Exception("ONNX File loading error!");
            }
        }
        public static void SetupONNXFile() // загрузка весов нейросети
        {
            using (var client = new WebClient())
            {
                int attempts = 0;
                while (true && (attempts < 10))
                {
                    if (File.Exists("tinyyolov2-8.onnx"))
                        return;

                    try
                    {
                        client.DownloadFile("https://storage.yandexcloud.net/dotnet4/tinyyolov2-8.onnx", "tinyyolov2-8.onnx");
                    }
                    catch (Exception ex)
                    {
                        Thread.Sleep(5000);
                        attempts++;
                    }
                }
                if (attempts < 10)
                    return;
                else
                    throw new Exception("ONNX File loading error!");
            }
        }
        private static int IndexOfMax(float[] values)
        {
            int idx = 0;
            for (int i = 1; i < values.Length; i++)
                if (values[i] > values[idx])
                    idx = i;
            return idx;
        }
        private static float Sigmoid(float value)
        {
            var e = (float)Math.Exp(value);
            return e / (1.0f + e);
        }
        public static float[] Softmax(float[] values)
        {
            var exps = values.Select(v => Math.Exp(v));
            var sum = exps.Sum();
            return exps.Select(e => (float)(e / sum)).ToArray();
        }

        public static async Task AnnotateAsync(Image<Rgb24> target, IEnumerable<ObjectBox> objects)
        {
            foreach (var objbox in objects)
            {
                await Task.Run(() =>
                {
                    target.Mutate(ctx =>
                    {
                        ctx.DrawPolygon(Pens.Solid(Color.Blue, 2), new PointF[] {
                           new PointF((float)objbox.XMin, (float)objbox.YMin),
                           new PointF((float)objbox.XMin, (float)objbox.YMax),
                           new PointF((float)objbox.XMax, (float)objbox.YMax),
                           new PointF((float)objbox.XMax, (float)objbox.YMin)}
                           );

                        ctx.DrawText($"{labels[objbox.Class]}",
                            SystemFonts.Families.First().CreateFont(16),
                            Color.Blue, new PointF((float)objbox.XMin, (float)objbox.YMax));
                    });
                });
            }
        }
        public static Image<Rgb24> Annotate(Image<Rgb24> target, IEnumerable<ObjectBox> objects)
        {
            foreach (var objbox in objects)
            {
                target.Mutate(ctx =>
                {
                    ctx.DrawPolygon(Pens.Solid(Color.Blue, 2), new PointF[] {
                        new PointF((float)objbox.XMin, (float)objbox.YMin),
                        new PointF((float)objbox.XMin, (float)objbox.YMax),
                        new PointF((float)objbox.XMax, (float)objbox.YMax),
                        new PointF((float)objbox.XMax, (float)objbox.YMin)}
                        );

                    ctx.DrawText($"{labels[objbox.Class]}",
                        SystemFonts.Families.First().CreateFont(16),
                        Color.Blue, new PointF((float)objbox.XMin, (float)objbox.YMax));
                });
            }
            return target;
        }
        private static void Write(string s)
        {
            //Console.WriteLine(s);
            throw new Exception(s);
        }
        static public ObservableCollection<Image<Rgb24>> GetImages(ObservableCollection<string> file_names)
        {
            ObservableCollection<Image<Rgb24>> list = new();
            foreach (string file_name in file_names)
                list.Add(Image.Load<Rgb24>(file_name));
            return list;
        }
    }

    public record ObjectBox(double XMin, double YMin, double XMax, double YMax, double Confidence, int Class)
    {
        public double IoU(ObjectBox b2) =>
            (Math.Min(XMax, b2.XMax) - Math.Max(XMin, b2.XMin)) * (Math.Min(YMax, b2.YMax) - Math.Max(YMin, b2.YMin)) /
            ((Math.Max(XMax, b2.XMax) - Math.Min(XMin, b2.XMin)) * (Math.Max(YMax, b2.YMax) - Math.Min(YMin, b2.YMin)));
    }

}
