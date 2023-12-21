using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.Design.Serialization;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.UI.WebControls;
using Newtonsoft.Json.Linq;
using UcbBack.Logic;
using UcbBack.Models;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using DocumentFormat.OpenXml.Office2013.Drawing.Chart;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Ajax.Utilities;
using Newtonsoft.Json;
using UcbBack.Logic.B1;
using UcbBack.Logic.Mail;
using UcbBack.Models.Auth;
using UcbBack.Models.Dist;
using UcbBack.Models.Not_Mapped;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using UcbBack.Models.Not_Mapped.ViewMoldes;
namespace UcbBack.Controllers
{
    public class PreliminarController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidatePerson validator;
        private ValidateAuth auth;
        private ADClass activeDirectory;
        public class MyObject
        {
            public string name { get; set; }
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public List<MyObject> children { get; set; }
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? Size { get; set; }
        }
        public PreliminarController()
        {
            _context = new ApplicationDbContext();
            validator = new ValidatePerson(_context);
            auth = new ValidateAuth();
            activeDirectory = new ADClass();
        }
        //Funcion donde adquirimos la tabla de busqueda grupal
        public IHttpActionResult Get(int id)
        {
            string res = "false";
            var pro = _context.DistProcesses.Include(x => x.Branches).FirstOrDefault(x => x.Id == id);
            string query = "select concat(concat(concat(concat(b.\"Abr\", '-'), o.\"segmento\"),'-'), concat(concat(dp.\"mes\", '-'), dp.\"gestion\")) \"Name\", b.\"Abr\" \"RegionalOrigen\", o.\"segmento\" \"Regional\"" +
                          "\r\nfrom " + CustomSchema.Schema + ".\"Dist_OR\" o" +
                          "\r\ninner join " + CustomSchema.Schema + ".\"Dist_File\" df\r\non df.\"Id\" = o.\"DistFileId\"" +
                          "\r\ninner join " + CustomSchema.Schema + ".\"Dist_Process\" dp\r\non dp.\"Id\" = df.\"DistProcessId\"" +
                          "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" b\r\non b.\"Id\" = o.\"segmentoOrigen\"" +
                          "\r\nwhere dp.\"mes\" = " + pro.mes +
                          "\r\nand dp.\"gestion\" = " + pro.gestion +
                          "\r\nand dp.\"State\" = 'INSAP'" +
                          "\r\nand o.\"segmento\" = '" + pro.Branches.Abr + "'" +
                          "\r\ngroup by b.\"Abr\",  o.\"segmento\", dp.\"mes\",dp.\"gestion\";";

            var rawResult = _context.Database.SqlQuery<AuxiliarPre>(query).Select(x => new
            {
                x.Id,
                x.Name,
                x.RegionalOrigen,

            }).ToList();

            if (rawResult.Count > 0)
            {
                res = "true";
            }

            return Ok(res);
        }
        //Funcion de regreso de person data por contractid
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/GetPreliminarFromProcess/{id}")]
        public IHttpActionResult GetPreliminarFromProcess(int id)
        {

            var pro = _context.DistProcesses.Include(x => x.Branches).FirstOrDefault(x => x.Id == id);
            

            var query = "";
                query = "select concat(concat(concat(concat(b.\"Abr\", '-'), o.\"segmento\"),'-'), concat(concat(dp.\"mes\", '-'), dp.\"gestion\")) \"Name\", b.\"Abr\" \"RegionalOrigen\", o.\"segmento\" \"Regional\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"Dist_OR\" o" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Dist_File\" df\r\non df.\"Id\" = o.\"DistFileId\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Dist_Process\" dp\r\non dp.\"Id\" = df.\"DistProcessId\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" b\r\non b.\"Id\" = o.\"segmentoOrigen\"" +
                        "\r\nwhere dp.\"mes\" = " + pro.mes +
                        "\r\nand dp.\"gestion\" = " + pro.gestion +
                        "\r\nand dp.\"State\" = 'INSAP'" +
                        "\r\nand o.\"segmento\" = '" + pro.Branches.Abr + "'" +
                        "\r\ngroup by b.\"Abr\",  o.\"segmento\", dp.\"mes\",dp.\"gestion\";";
                var rawResult = _context.Database.SqlQuery<AuxiliarPre>(query).Select(x => new
                {
                    x.Name,
                    x.RegionalOrigen,
                    x.Regional
                }).ToList();
           return Ok(rawResult);
            
        }


    }
}
