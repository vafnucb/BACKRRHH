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
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using DocumentFormat.OpenXml.Office2013.Drawing.Chart;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Ajax.Utilities;
using Newtonsoft.Json;
using UcbBack.Logic.B1;
using UcbBack.Logic.Mail;
using UcbBack.Models.Auth;
using UcbBack.Models.Not_Mapped;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using UcbBack.Models.Not_Mapped.ViewMoldes;
namespace UcbBack.Controllers
{
    public class PlanillaInfromacionController : ApiController
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
        public PlanillaInfromacionController()
        {
            _context = new ApplicationDbContext();
            validator = new ValidatePerson(_context);
            auth = new ValidateAuth();
            activeDirectory = new ADClass();
        }
        //Funcion donde adquirimos la tabla de busqueda grupal
        public IHttpActionResult Get()
        {
            string query = "select p.\"Id\",p.CUNI,p.\"Document\",lc.\"FullName\",lc.\"Positions\",lc.\"Dependency\"," +
                           " case when (lc.\"Active\"=false and lc.\"EndDate\"<current_date) " +
                           "then 'Inactivo' else 'Activo' end as \"Status\", lc.\"BranchesId\"" +
                           ",lc.\"Branches\" " +
                           "from " + CustomSchema.Schema + ".\"People\" p " +
                           "inner join " + CustomSchema.Schema + ".\"LASTCONTRACTS\" lc " +
                           "on p.cuni = lc.cuni " +
                           " order by \"FullName\";";

            var rawResult = _context.Database.SqlQuery<ContractDetailViewModel>(query).Select(x => new
            {
                x.Id,
                x.CUNI,
                x.Document,
                x.FullName,
                x.Dependency,
                x.DependencyCod,
                x.Branches,
                x.Positions,
                x.Dedication,
                x.Linkage,
                x.Status,
                x.BranchesId
            }).AsQueryable();

            var user = auth.getUser(Request);

            var result = auth.filerByRegional(rawResult, user).ToList().Select(x => new
            {
                x.Id,
                x.CUNI,
                x.Document,
                x.FullName,
                x.Positions,
                x.Dependency,
                x.Branches,
                Estado = x.Status
            }).ToList();

            return Ok(result);
        }
        //Funcion de regreso de person data por contractid
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Planillas/PlanillasAlMes/{mes}/{gestion}")]
        public HttpResponseMessage PlanillasAlMes(int mes, int gestion)
        {
            
            string query = "CALL " + CustomSchema.Schema + ".\"PLANILLA_OVT\" ("+mes+","+gestion+");";

            var excelContent = _context.Database.SqlQuery<PlanillaAlMes>(query).ToList();

            //--------------------------------------------------------Generación del excel------------------------------------------------------------------------
            //Para las columnas del excel
            string[] header = new string[]
                    {
                        "SEDE",
                        "DOCUMENTO DE IDENTIDAD",
                        "APELLIDOS Y NOMBRES",
                        "PAIS DE NACIONALIDAD",
                        "FECHA DE NACIMIENTO",
                        "SEXO",
                        "OCUPACION QUE DESEMPEÑA",
                        "FECHA DE INGRESO",
                        "HORAS PAGADAS",
                        "DIAS PAGADOS",
                        "HABER BASICO",
                        "BONO ANTIGUEDAD",
                        "OTROS INGRESOS",
                        "INGRESOS POR DOCENCIA",
                        "INGRESOS POR OTRAS ACTIVIDADES ACADEMICAS",
                        "REINTEGRO",
                        "TOTAL GANADO",
                        "APORTE A AFP",
                        "RC IVA",
                        "DESCUENTOS",
                        "TOTAL DESCUENTOS",
                        "LIQUIDO PAGABLE",
                    };
            var workbook = new XLWorkbook();

            //Se agrega la hoja de excel
            var ws = workbook.Worksheets.Add("Planilla");
            /*var range = workbook.Worksheets.Range("A1:B2");
            range.Value = "Merged A1:B2";
            range.Merge();
            range.Style.Alignment.Vertical = AlignmentVerticalValues.Top;*/
            // Título

            // Rango hoja excel
            //1,1: es la posicion inicial; 2,header.Length: es el alto y el ancho
            var rngTable = ws.Range(1, 1, 1, header.Length);

            //Bordes para las columnas
            var columns = ws.Range(1, 1, 1 + excelContent.Count, header.Length);
            columns.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            columns.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;


            //Para juntar celdas de la cabecera
            

            //auxiliar: desde qué línea ponemos los nombres de columna
            var headerPos = 1;

            //Ciclo para asignar los nombres a las columnas y darles formato
            for (int i = 0; i < header.Length; i++)
            {
                ws.Column(i + 1).Width = 13;
                ws.Cell(headerPos, i + 1).Value = header[i];
                ws.Cell(headerPos, i + 1).Style.Alignment.WrapText = true;
                ws.Cell(headerPos, i + 1).Style.Font.Bold = true;
                ws.Cell(headerPos, i + 1).Style.Fill.BackgroundColor = XLColor.GreenYellow;
            }

            //Aquí hago el attachment del query a mi hoja de de excel
            ws.Cell(2, 1).Value = excelContent.AsEnumerable();

            //Ajustar contenidos
            ws.Columns().AdjustToContents();

            //Carga el objeto de la respuesta
            HttpResponseMessage response = new HttpResponseMessage();
            //Array de bytes
            var ms = new MemoryStream();
            workbook.SaveAs(ms);
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StreamContent(ms);
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
            response.Content.Headers.ContentDisposition.FileName = "Planilla-Consolidada-"+ mes +"-"+ gestion+".xlsx";
            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Content.Headers.ContentLength = ms.Length;
            //La posicion para el comienzo del stream
            ms.Seek(0, SeekOrigin.Begin);

            return response;

        }

        //Listado de novedades
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Planilla/Personas")]
        public IHttpActionResult getPeople()
        {
            string query = "select dp.cuni, f.\"FullName\"" +
                           "\r\nfrom "+CustomSchema.Schema+".\"Dist_Payroll\" dp" +
                           "\r\ninner join " + CustomSchema.Schema + ".\"FullName\" f on dp.cuni = f.cuni" +
                           "\r\nwhere dp.\"DistFileId\" in (SELECT a.\"Id\" from " + CustomSchema.Schema + ".\"Dist_File\" a" +
                           "\r\nINNER JOIN " + CustomSchema.Schema + ".\"Dist_Process\" b" +
                           "\r\nON a.\"DistProcessId\"=b.\"Id\"\r\nwhere a.\"State\" = 'UPLOADED' and b.\"State\" = 'INSAP')" +
                           "\r\ngroup by dp.cuni, f.\"FullName\" order by  f.\"FullName\"";

            var rawResult = _context.Database.SqlQuery<PeopleAux>(query).ToList();

            return Ok(rawResult);
        }
    }
}
