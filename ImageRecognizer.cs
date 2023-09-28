using System;
using System.Net;
using SixLabors.ImageSharp; // Из одноимённого пакета NuGet
using SixLabors.ImageSharp.PixelFormats;
using System.Linq;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.OnnxRuntime;
using System.Collections.Generic;
using SixLabors.Fonts;
using System.IO;
//using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.CompilerServices;

namespace ImageRecognizerNamespace
{
    public class ImageRecognizer
    {
        const int Canceled = -1;

        static CancellationTokenSource cts = new CancellationTokenSource();
        struct BoundingInfo
        {
            string filename;
            string name;
            double X;
            double Y;
            double W;
            double H;
        }

        public static string[] labels = new string[]
        {
            "aeroplane", "bicycle", "bird", "boat", "bottle",
            "bus", "car", "cat", "chair", "cow",
            "diningtable", "dog", "horse", "motorbike", "person",
            "pottedplant", "sheep", "sofa", "train", "tvmonitor"
        };
       public static string dir = "../../../images/";
        //public static int MainAsync(string filename)
        public static async Task MainAsync(string filename)
        {
            //Thread Setup= new Thread(SetupONNXFile);
            //Setup.Start();
            await Task.WhenAll( SetupONNXFileAsync());

            if (!File.Exists(dir + filename + ".jpg"))
            {
                throw new Exception("File doesn't exist");
            }
            ////////////////////////////////////////////////////////////////////////////////////////////

            //using var image = Image.Load<Rgb24>(args.FirstOrDefault() ?? dir + filename + ".jpg");
            using var image = Image.Load<Rgb24>(dir + filename + ".jpg");
            
            //int imageWidth = image.Width;
            //int imageHeight = image.Height;

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

            //foreach(var d in outputs.Dimensions)
            //    Console.WriteLine(d);

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

            List<ObjectBox> objects = new ();

            var csvPath = dir + "results.csv"; //Path.Combine(Environment.CurrentDirectory, $"something.csv");
            var streamWriter = new StreamWriter(csvPath);
            var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture);
            //csvWriter.Context.RegisterClassMap<BoundingInfo>();
            csvWriter.WriteField("filename");
            csvWriter.WriteField("class");
            csvWriter.WriteField("X");
            csvWriter.WriteField("Y");
            csvWriter.WriteField("W");
            csvWriter.WriteField("H");
            csvWriter.NextRecord();

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

                            csvWriter.WriteField("|");
                            csvWriter.WriteField(labels[IndexOfMax(Softmax(classes))]);
                            csvWriter.WriteField(x.ToString());
                            csvWriter.WriteField(y.ToString());
                            csvWriter.WriteField(w.ToString());
                            csvWriter.WriteField(h.ToString());
                            csvWriter.NextRecord();
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
            
            streamWriter.Close();

            boundingBoxes.Save(dir + "tmp/" + "boundingboxes.jpg");

            var annotated = resized.Clone();
            await AnnotateAsync(annotated, objects);

            annotated.SaveAsJpeg(dir + "tmp/" + "annotated.jpg");

            // Убираем дубликаты
            for(int i = 0;i<objects.Count;i++)
            {
                var o1 = objects[i];
                for(int j = i + 1;j<objects.Count;)
                {
                    var o2 = objects[j];
                    Console.WriteLine($"IoU({i},{j})={o1.IoU(o2)}");
                    if(o1.Class == o2.Class && o1.IoU(o2) > 0.6) 
                    {
                        if(o1.Confidence < o2.Confidence)
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
            final.SaveAsJpeg(dir+"final.jpg");
        }
        public static async Task SetupONNXFileAsync() // загрузка весов нейросети
        {
            if (File.Exists("tinyyolov2-8.onnx"))
            {
                return;
            }
            using (var client = new WebClient())
            {
                bool complete = false;
                while (!complete) { 
                    try
                    {
                        client.DownloadFile("https://storage.yandexcloud.net/dotnet4/tinyyolov2-8.onnx", "tinyyolov2-8.onnx");
                    }
                    catch (Exception e)
                    {
                        complete = true;
                    }
                }
            }
            return;
        }
        public static void SetupONNXFile() // загрузка весов нейросети
        {
            if (File.Exists("tinyyolov2-8.onnx"))
            {
                return;
            }

            using (var client = new WebClient())
            {
                while (true)
                {
                    try
                    {
                        client.DownloadFile("https://storage.yandexcloud.net/dotnet4/tinyyolov2-8.onnx", "tinyyolov2-8.onnx");
                    }
                    catch (Exception e)
                    {
                        continue;
                    }
                    break;
                }
            }
            return;
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

        private static async Task AnnotateAsync(Image<Rgb24> target, IEnumerable<ObjectBox> objects)
        {
            int i = 0;
            foreach (var objbox in objects)
            {
                target.Mutate(ctx =>
                {
                    ctx.DrawPolygon(Pens.Solid(Color.Blue, 2),new PointF[] {
                           new PointF((float)objbox.XMin, (float)objbox.YMin),
                           new PointF((float)objbox.XMin, (float)objbox.YMax),
                           new PointF((float)objbox.XMax, (float)objbox.YMax),
                           new PointF((float)objbox.XMax, (float)objbox.YMin)}
                       );

                    ctx.DrawText($"{labels[objbox.Class]}",
                        SystemFonts.Families.First().CreateFont(16),
                        Color.Blue,new PointF((float)objbox.XMin, (float)objbox.YMax));
                });
                await target.SaveAsJpegAsync(dir + "tmp/" + objbox.Class.ToString() + i.ToString() +  ".jpg");
                i++;
            }
        }
    }

    public record ObjectBox(double XMin, double YMin, double XMax, double YMax, double Confidence, int Class) 
    {
        public double IoU(ObjectBox b2) =>
            (Math.Min(XMax, b2.XMax) - Math.Max(XMin, b2.XMin)) * (Math.Min(YMax, b2.YMax) - Math.Max(YMin, b2.YMin)) /
            ((Math.Max(XMax, b2.XMax) - Math.Min(XMin, b2.XMin)) * (Math.Max(YMax, b2.YMax) - Math.Min(YMin, b2.YMin)));
    }

}
