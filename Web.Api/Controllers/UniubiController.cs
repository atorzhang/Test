using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Web.Api.Controllers
{
    [Route("api")]
    [ApiController]
    public class UniubiController : ControllerBase
    {
        
        [HttpPost(nameof(UniubiEventCallBack))]
        public string UniubiEventCallBack()
        {
            StreamReader sr = new StreamReader(Request.Body, Encoding.UTF8);
            string strData = sr.ReadToEndAsync().Result;

            //byte[] bs = new byte[Request.Body.Length];
            //Request.Body.Read(bs, 0, (int)Request.Body.Length);
            //var ss = Encoding.UTF8.GetString(bs, 0, bs.Length);

            //var deviceKey = Request.Form["deviceKey"].ToString();
            //var time = Request.Form["time"].ToString();
            //var ip = Request.Form["ip"].ToString();
            //var @event = Request.Form["event"].ToString();

            return System.Text.Json.JsonSerializer.Serialize(new { ttsModContent= "欢迎光临", displayModContent= "你好我的主人", isOpenRelay = true});
        }
    }


    public class ub
    {
        public string deviceKey { get; set; }
        public string time { get; set; }
        public string ip { get; set; }
        public string @event { get; set; }

    }

}
