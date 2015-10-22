using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Funshot
{
    public class RemoteHandler
    {
        private const string hostname = "http://funshot.lionfree.net/";

        public async Task<bool> IsRemoteConnected()
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(hostname);
            try
            {
                WebResponse res = await req.GetResponseAsync();
                return true;
            }
            catch (WebException)
            {
                Trace.WriteLine(Properties.Resources.FailedConnectRemoteText);
                return false;
            }
        }

        public async Task<string> GetImageUrlAsync(RenderTargetBitmap renderBitmap)
        {
            return await getImageUrlAsync(await postImageHttpWebRequsetAsync(convertImageToByte(renderBitmap)));
        }

        private async Task<HttpWebRequest> postImageHttpWebRequsetAsync(byte[] image)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create($"{hostname}get.php");
            byte[] bs = Encoding.ASCII.GetBytes(@"img=data:image/jpeg;base64," + Convert.ToBase64String(image));
            req.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            req.Method = "POST";
            req.ContentLength = bs.Length;
            Stream reqStream = await req.GetRequestStreamAsync();
            reqStream.Write(bs, 0, bs.Length);
            return req;
        }

        private async Task<string> getImageUrlAsync(HttpWebRequest req)
        {
            WebResponse response = await req.GetResponseAsync();
            string imageAddress = $@"{hostname}" + new StreamReader(response.GetResponseStream()).ReadToEnd();
            return imageAddress;
        }

        private byte[] convertImageToByte(RenderTargetBitmap image)
        {
            BitmapEncoder encoder = new JpegBitmapEncoder();
            MemoryStream ms = new MemoryStream();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(ms);
            return ms.ToArray();
        }
    }
}
