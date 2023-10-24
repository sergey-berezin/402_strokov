using CsvHelper;
using System.Globalization;
using ImageRecognizerNamespace;

public class ConsolePart
{
    static CancellationTokenSource cts = new CancellationTokenSource();

    static async Task Main(string[] args)
    {
        Console.WriteLine("Enter The file name:");
        string filename = Console.ReadLine();
        List<ObjectBox> objects;
        try
        {
            objects = await ImageRecognizer.FindAsync(filename, cts);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return;
        }
        await WriteCSVAsync(objects);

        return;
    }

    private static async Task WriteCSVAsync(List<ObjectBox> objects)
    {
        var csvPath = "results.csv"; //Path.Combine(Environment.CurrentDirectory, $"something.csv");
        using (var streamWriter = new StreamWriter(csvPath))
        {
            using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
            {
                //csvWriter.Context.RegisterClassMap<BoundingInfo>();
                csvWriter.WriteField("filename");
                csvWriter.WriteField("class");
                csvWriter.WriteField("X");
                csvWriter.WriteField("Y");
                csvWriter.WriteField("W");
                csvWriter.WriteField("H");
                csvWriter.NextRecord();
                foreach (var obj in objects)
                {
                    csvWriter.WriteField(ImageRecognizer.labels[obj.Class] + ".jpg");
                    csvWriter.WriteField(ImageRecognizer.labels[obj.Class]);
                    csvWriter.WriteField(obj.XMin.ToString());
                    csvWriter.WriteField(obj.YMin.ToString());
                    csvWriter.WriteField((obj.XMax - obj.XMin).ToString());
                    csvWriter.WriteField((obj.YMax - obj.YMin).ToString());
                    csvWriter.NextRecord();
                }
            }
        }
    }
}
