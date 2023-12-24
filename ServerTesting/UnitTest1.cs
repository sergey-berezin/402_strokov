using Newtonsoft.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using WebApplication;
using System.Net.Http.Json;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
namespace ServerTesting
{
    public class ServerControllerTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> factory;
        readonly HttpClient client;
        const string url = "http://localhost:5278/My";
        const string filePath = "..\\..\\..\\..\\WebApplication3\\test.jpg";
        public ServerControllerTests(WebApplicationFactory<Program> factory)
        {
            this.factory = factory;
            client = factory.CreateClient();
        }
        public StringContent toJson(string str)
        {
            var jsonString = System.Text.Json.JsonSerializer.Serialize(str);
            return new StringContent(jsonString, Encoding.UTF8, "application/json");
        }

        [Fact]
        public async Task OkTest()
        {
            string img = Convert.ToBase64String(File.ReadAllBytes(filePath));
            var response = await client.PostAsync(url, toJson(img));
            Assert.Equal(200, (int)response.StatusCode);
            //var image = Image.Load<Rgb24>(filePath);
            //using (MemoryStream ms = new MemoryStream())
            //{
            //    image.Save(ms, JpegFormat.Instance);
            //    byte[] pixels = ms.ToArray();
            //    var t = await client.PostAsJsonAsync("/My", Convert.ToBase64String(pixels));
            //    Assert.Equal(System.Net.HttpStatusCode.OK, t.StatusCode);
            //}
        }

        [Fact]
        public async Task ErrorTest()
        {
            var response = await client.PostAsync(url, toJson("nothingt"));
            await response.Content.ReadAsStringAsync();
            Assert.Equal(101, (int)response.StatusCode);
        }

        [Fact]
        public async Task ResultTest()
        {
            string img = Convert.ToBase64String(File.ReadAllBytes(filePath));
            var response = await client.PostAsJsonAsync(url, img);//PostAsync(url, toJson(img));
            var answer = await response.Content.ReadAsStringAsync();
            var list = JsonConvert.DeserializeObject<WebApplication.Controllers.MyController.ObjectFrameInfo[]>(await response.Content.ReadAsStringAsync());

            Assert.Equal(200, (int)response.StatusCode);
            Assert.Equal(2, list.Length);
            Assert.Equal("cow", list[0].Label);
            Assert.Equal("cow", list[1].Label);
        }
    }
}