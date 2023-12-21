using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UcbBack.Models;
using System.Data.Entity;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Wordprocessing;
using Newtonsoft.Json.Linq;
using UcbBack.Logic;
using System;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using DocumentFormat.OpenXml.Packaging;
using Newtonsoft.Json.Linq;
using UcbBack.Logic.ExcelFiles;
using UcbBack.Models;
using System.Net.Http.Headers;
using System.Security.AccessControl;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNet.Identity;
using Sap.Data.Hana;
using UcbBack.Logic;
using UcbBack.Logic.B1;
using UcbBack.Models.Dist;
using UcbBack.Models.Not_Mapped;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using UcbBack.Models.Not_Mapped.ViewMoldes;

namespace UcbBack.Controllers
{
    public class DistProcessController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidateAuth auth;

        public DistProcessController()
        {
            _context = new ApplicationDbContext();
            auth = new ValidateAuth();
        }
        [HttpGet]
        [Route("api/DistProcess/Files/{id}")]
        public IHttpActionResult GetProcesses(int id)
        {
            var data = _context.Database.SqlQuery<Dist_ProcessViewModel>("SELECT * " +
                                                                "from " + CustomSchema.Schema + ".\"Dist_File\" a" +
                                                                "\r\nINNER JOIN " + CustomSchema.Schema + ".\"Dist_Process\" b ON a.\"DistProcessId\"=b.\"Id\" " +
                                                                "\r\ninner JOIN " + CustomSchema.Schema + ".\"Dist_FileType\" f ON a.\"DistFileTypeId\"=f.\"Id\" " +
                                                                "\r\nwhere b.\"Id\"=" + id + " and a.\"State\" = 'UPLOADED';").ToList();

            return Ok(data);
        }
        [HttpGet]
        [Route("api/DistProcess/{id}")]
        public IHttpActionResult GetProcess(int id)
        {
            var data = _context.Database.SqlQuery<Dist_ProcessViewModel>("SELECT dp.*, br.\"Abr\" \"Branches\" " +
                                                                         "from " + CustomSchema.Schema + ".\"Dist_Process\" dp " +
                                                                         "inner join " + CustomSchema.Schema + ".\"Branches\" br on br.\"Id\" = dp.\"BranchesId\" " +
                                                                         "\r\nwhere dp.\"Id\"=" + id).ToList();

            return Ok(data);
        }
        [HttpGet]
        [Route("api/GetInterregional/")]
        public IHttpActionResult GetInterregional()
        {
            var data = _context.Database.SqlQuery<Dist_InterregionalViewModel>("select di.\"Id\",\tdi.\"UploadedDate\",\tdi.\"BranchesId\", b.\"Abr\" \"Destino\", di.\"segmentoOrigen\", b2.\"Abr\" \"Origen\",\tdi.\"mes\",\tdi.\"gestion\",\tdi.\"State\",\tdi.\"TransNumber\",\tu.\"UserPrincipalName\" \"User\", p.\"FullName\", ojdt.\"TransId\"" +
                                                                               "\r\nfrom " + CustomSchema.Schema + ".\"Dist_Interregional\" di " +
                                                                               "\r\ninner join " + CustomSchema.Schema + ".\"User\" u on u.\"Id\" = di.\"User\" " +
                                                                               "\r\ninner join " + CustomSchema.Schema + ".\"FullName\" p on u.\"PeopleId\" = p.\"PeopleId\"" +
                                                                               "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" b on di.\"BranchesId\" = b.\"Id\"" +
                                                                               "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" b2 on di.\"segmentoOrigen\" = b2.\"Id\"" +
                                                                               "\r\nleft join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".ojdt on ojdt.\"BatchNum\" = di.\"TransNumber\"" +
                                                                               "\r\norder by \"gestion\" desc, \"mes\" desc, b.\"Abr\"").ToList();
            var user = auth.getUser(Request);

            var res = auth.filerByRegional(data.AsQueryable(), user);

            return Ok(res);
        }
    }
}