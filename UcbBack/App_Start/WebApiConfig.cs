using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Cors;
using Microsoft.Owin.Security.OAuth;
using Newtonsoft.Json.Serialization;

namespace UcbBack
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services
            // Configure Web API to use only bearer token authentication.
            //config.SuppressDefaultHostAuthentication();
            //config.Filters.Add(new HostAuthenticationFilter(OAuthDefaults.AuthenticationType));
            //configure multipart for excel
            config.Formatters.XmlFormatter.SupportedMediaTypes.Add(new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/form-data"));
            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            // Descomenta la llamada a la función EnableCrossSiteRequests para habilitar CORS
            EnableCrossSiteRequests(config);
        }

        private static void EnableCrossSiteRequests(HttpConfiguration config)
        {
            var cors = new EnableCorsAttribute(
                origins: "http://localhost:8000",
                headers: "*",
                methods: "*");
            cors.ExposedHeaders.Add("Content-Disposition");
            config.EnableCors(cors);
        }
    }
}