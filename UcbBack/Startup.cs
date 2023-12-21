using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.WebPages.Scope;
using Microsoft.Owin;
using Microsoft.Owin.Host.SystemWeb;
using Owin;
using UcbBack.Logic;
using UcbBack.Models;
using UcbBack.Models.Auth;

[assembly: OwinStartup(typeof(UcbBack.Startup))]

namespace UcbBack
{
    public partial class Startup
    {

        public void Configuration(IAppBuilder app)
        {
            bool debugmode = true;
            app.Use(async (environment, next) =>
                {
                    long logId=0;
                    AccessLogs log=null;
                    ApplicationDbContext _context = new ApplicationDbContext();
                    var req = environment.Request;
                    string endpoint = environment.Request.Path.ToString();
                    Uri uri = req.Uri;
                    var seg = uri.Segments;

                    string verb = environment.Request.Method;
                    int userid = 0;
                    Int32.TryParse(environment.Request.Headers.Get("id"),out userid);
                    string token = environment.Request.Headers.Get("token");

                    ValidateAuth validator = new ValidateAuth();
                    int resourceid = 0;
                    //tiene resourseid
                    if (Int32.TryParse(seg[seg.Length-1], out resourceid))
                    {
                        endpoint = "";
                        for (int i = 0; i < seg.Length-1; i++)
                        {
                            endpoint += seg[i];
                        }
                    }

                    bool sup = validator.shallYouPass(userid, token, endpoint, verb, out log);

                    if (!debugmode && !sup)
                    {
                        environment.Response.StatusCode = 401;
                        environment.Response.Body = new MemoryStream();

                        var newBody = new MemoryStream();
                        newBody.Seek(0, SeekOrigin.Begin);
                        var newContent = new StreamReader(newBody).ReadToEnd();

                        newContent += "You shall no pass.";

                        environment.Response.Body = newBody;
                        environment.Response.Write(newContent);
                        //log = _context.AccessLogses.FirstOrDefault(x => x.Id == logId);
                        if (log != null)
                        {
                            log.ResponseCode = environment.Response.StatusCode.ToString();
                            _context.AccessLogses.AddOrUpdate(log);
                            _context.SaveChanges();
                        }
                    }
                    else
                    {
                        await next();
                        //log = _context.AccessLogses.FirstOrDefault(x => x.Id == logId);
                        if (log != null)
                        {
                            log.ResponseCode = environment.Response.StatusCode.ToString();
                            _context.AccessLogses.AddOrUpdate(log);
                            _context.SaveChanges();
                        }
                    }
                    
                }
            );
            //app.UseStaticFiles();
            ConfigureAuth(app);
        }
    }
}
