using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace UcbBack.Controllers
{
    public class OptionsController : ApiController
    {
        [HttpOptions]
        public HttpResponseMessage Options()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With, id, token");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE");
            return response;
        }
    }
}