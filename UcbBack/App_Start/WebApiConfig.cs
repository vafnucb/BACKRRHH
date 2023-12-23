using System.Web.Http;
using System.Web.Http.Cors;

namespace UcbBack
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services
            // Configure Web API to use only bearer token authentication.
            // config.SuppressDefaultHostAuthentication();
            // config.Filters.Add(new HostAuthenticationFilter(OAuthDefaults.AuthenticationType));

            // Configure CORS
            var cors = new EnableCorsAttribute(
                origins: "http://192.168.18.75:8020", // Puedes ajustar esto según tu necesidad
                headers: "*",
                methods: "*");
            config.EnableCors(cors);

            // Configure multipart for Excel
            config.Formatters.XmlFormatter.SupportedMediaTypes.Add(new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/form-data"));

            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}
