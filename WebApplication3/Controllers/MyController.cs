using ImageRecognizerNamespace;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebApplication.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class MyController : Controller
    {
        public class ObjectFrameInfo
        {
            public string Label { get; set; }
            public double Conf { get; set; }
            public double Xmin { get; set; }
            public double Ymin { get; set; }
            public double Xmax { get; set; }
            public double Ymax { get; set; }
            public ObjectFrameInfo()
            {
                Label = "";
                Conf = Xmax = Ymax = Xmin = Ymin = 0;
            }

            public ObjectFrameInfo(ObjectBox obj)
            {
                Label = ImageRecognizer.labels[ obj.Class];
                Conf = obj.Confidence;
                Xmin = obj.XMin;
                Ymin = obj.YMin;
                Xmax = obj.XMax;
                Ymax = obj.YMax;
            }
        }
        public static ObjectFrameInfo[] TempToFrameList(List<ObjectBox> objList)
        {
            var result = new ObjectFrameInfo[objList.Count];
            for (int i = 0; i < objList.Count; i++)
                result[i] = new ObjectFrameInfo(objList[i]);
            return result;
        }

        [HttpPost]
        public async Task<ActionResult<ObjectFrameInfo[]>> postImage([FromBody] string data)
        {
            try
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                byte[] buffer = Convert.FromBase64String(data);
                Image<Rgb24> image;

                //using (MemoryStream ms = new MemoryStream(buffer))
                //{
                image = Image.Load<Rgb24>(buffer);//ms);
                //}

                var imgs = new List<Image<Rgb24>> { image };

                ImageRecognizer imageRecognizer = await ImageRecognizer.Create();
                var run_result = await ImageRecognizer.FindAsyncByImage(image, cts);

                ObjectFrameInfo[] result;

                if (run_result.Count > 0)
                    result = TempToFrameList(run_result);
                else
                    result = new ObjectFrameInfo[0];
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(101, ex.Message);
            }
        }
    }
}

