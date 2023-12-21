using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.Design.Serialization;
using System.Configuration;
using System.Linq;
using System.Linq.Expressions;
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
using Newtonsoft.Json;
using UcbBack.Logic.B1;
using UcbBack.Logic.Mail;
using UcbBack.Models.Auth;
using UcbBack.Models.Not_Mapped;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using UcbBack.Models.Not_Mapped.ViewMoldes;
namespace UcbBack.Controllers
{
    public class ListadoController : ApiController
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
        public ListadoController()
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
                           "inner join " + CustomSchema.Schema + ".\"LASTCONTRACTS_PRIORITY\" lc " +
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
        [System.Web.Http.Route("api/Listados/PersonalAtDate/{regional}/{Afecha}/{cabecera}")]
      //  public IHttpActionResult PersonData(string regional, string Afecha, string cabecera)
       public HttpResponseMessage PersonData(string regional, string Afecha, string cabecera)
        {
            string[] columnas = cabecera.Split(',');
            string auxColumnas = "select ";
            for (int i = 0; i < columnas.Length-1; i++)
            {
                auxColumnas = auxColumnas +" \"" + columnas[i] + "\", ";
            }
            auxColumnas = auxColumnas + " \"" + columnas[columnas.Length-1] + "\" ";

            string query = auxColumnas + " from (" +
                "SELECT\r\n\t \"F\".\"Document\" as \"DOCUMENTO\"," +
                           "\r\n\t \"F\".\"FullName\" AS \"NOMBRE_COMPLETO\"," +
                           "\r\n\t \"F\".\"FirstSurName\" AS \"PRIMER_APELLIDO\"," +
                           "\r\n\t \"F\".\"SecondSurName\" as \"SEGUNDO_APELLIDO\"," +
                           "\r\n\t  \"F\".\"Names\" AS \"NOMBRES\"," +
                           "\r\n\t \"F\".\"MariedSurName\" AS \"APELLIDO_DE_CASADA\"," +
                           "\r\n\t \"F\".\"CUNI\" ," +
                           "\r\n\t \"F\".\"DependencyCod\" AS \"COD_DEPENDENCIA\"," +
                           "\r\n\t \"F\".\"Dependency\" AS \"DEPENDENCIA\"," +
                           "\r\n\t \"F\".\"CodOU\" AS \"COD_UO\"," +
                           "\r\n\t \"F\".\"OU\" AS \"UNIDAD_ORGANIZACIONAL\"," +
                           "\r\n\t \"F\".\"Branches\" AS \"SEDE\"," +
                           "\r\n\t \"F\".\"Positions\" AS \"POSICION\"," +
                           "\r\n\t \"F\".\"Dedication\" AS \"DEDICACION\"," +
                           "\r\n\t \"F\".\"Linkage\" AS \"VINCULACION\"," +
                           "\r\n\t TO_VARCHAR (TO_DATE(\"F\".\"StartDate\"), 'DD/MM/YYYY') AS \"FECHA_INICIO\"," +
                           "\r\n\t TO_VARCHAR (TO_DATE(\"F\".\"EndDate\"), 'DD/MM/YYYY') AS \"FECHA_FIN\"," +
                           "\r\n\t TO_VARCHAR (TO_DATE(\"F\".\"BirthDate\"), 'DD/MM/YYYY') AS \"FECHA_NACIMIENTO\"," +
                           "\r\n\t \"F\".\"Edad\" AS \"EDAD\"," +
                           "\r\n\t \"F\".\"TypeDocument\" as \"TIPO_DE_DOCUMENTO\"," +
                           "\r\n\t \"F\".\"Ext\" as \"EXTENSION\"," +
                           "\r\n\t \"F\".\"Gender\" as \"GENERO\"," +
                           "\r\n\t \"F\".\"UcbEmail\" as \"EMAIL_INSTITUCIONAL\"," +
                           "\r\n\t \"F\".\"AI\" as \"INTERINATO\"," +
                           "\r\n\t \"F\".\"PositionDescription\" as \"DESCRIPCION_DEL_CARGO\"," +
                           "\r\n\t case when \"F\".\"PhoneNumber\" is null then ''\r\n\t else " +
                           "\"F\".\"PhoneNumber\"\r\n\t end as \"TELEFONO\"" +
                           "\r\nfrom (select\r\n\t p.\"Document\",\r\n\t case when p.\"TypeDocument\" is null then '' \r\n\t " +
                           "when p.\"TypeDocument\" is not null then p.\"TypeDocument\"\r\n\t end as \"TypeDocument\",\r\n\t case when  p.\"Ext\" is null then '' \r\n\t when  " +
                           "p.\"Ext\" is not null then  p.\"Ext\"\r\n\t end as \"Ext\",\r\n\t case when  p.\"Gender\" is null then '' \r\n\t when p.\"Gender\" is not null then " +
                           "p.\"Gender\"\r\n\t end as \"Gender\",\r\n\t case when  p.\"UcbEmail\" is null then '' \r\n\t when p.\"UcbEmail\" is not null then p.\"UcbEmail\"\r\n\t " +
                           "end as \"UcbEmail\",\r\n\t case \r\n\t when p.\"PhoneNumber\" is not null and p.\"HomePhoneNumber\" is not null then concat(p.\"PhoneNumber\"," +
                           "concat('-',p.\"HomePhoneNumber\"))\r\n\t when p.\"PhoneNumber\" is not null then p.\"PhoneNumber\"\r\n\t when p.\"PhoneNumber\" is null then " +
                           "p.\"HomePhoneNumber\"\r\n\t end as \"PhoneNumber\",\r\n\t p.\"Id\" as \"PeopleId\",\r\n\t "+CustomSchema.Schema+".clean_text( concat(coalesce(p.\"FirstSurName\"," +
                           "\r\n\t''),\r\n\t concat(' ',\r\n\t concat(case when p.\"UseSecondSurName\"=1 \r\n\t\t\t\t\tthen coalesce(p.\"SecondSurName\",\r\n\t'') \r\n\t\t\t\t\telse ''" +
                           " \r\n\t\t\t\t\tend,\r\n\t concat(' ',\r\n\t concat( case when p.\"UseMariedSurName\"=1 \r\n\t\t\t\t\t\t\tthen concat(coalesce(p.\"MariedSurName\",\r\n\t'')," +
                           "\r\n\t' ') \r\n\t\t\t\t\t\t\telse '' \r\n\t\t\t\t\t\t\tend,\r\n\t coalesce(p.\"Names\",\r\n\t'')) ) ) ) ) ) as \"FullName\",\r\n\tp.\"FirstSurName\"," +
                           "\r\n\tcase when p.\"SecondSurName\" is null then '' \r\n\twhen p.\"SecondSurName\" is not null then p.\"SecondSurName\"\r\n\tend as \"SecondSurName\"," +
                           "\r\n\tcase when p.\"MariedSurName\" is null then '' \r\n\twhen p.\"MariedSurName\" is not null then p.\"MariedSurName\"\r\n\tend as \"MariedSurName\"," +
                           "\r\n\tp.\"Names\",\r\n\tp.\"BirthDate\",\r\n\tyear(current_date) - year(p.\"BirthDate\") as \"Edad\",\r\n\t x.* \r\n\tfrom ( select\r\n\t a.\"Id\"," +
                           "\r\n\t a.cuni,\r\n\t c.\"Name\" as \"Dependency\",\r\n\t c.\"Cod\" as \"DependencyCod\",\r\n\t c.\"OrganizationalUnitId\" as \"OUId\",\r\n\t o.\"Cod\" " +
                           "as \"CodOU\",\r\n \t o.\"Name\" as \"OU\",\r\n\t d.\"Abr\" as \"Branches\",\r\n\t d.\"Id\" as \"BranchesId\",\r\n\t b.\"Name\" as \"Positions\",\r\n\t " +
                           "a.\"PositionDescription\",\r\n\t case when  a.\"AI\" = true then 'INTERINO' \r\n\t when a.\"AI\" = false then '' \r\n\t end as \"AI\",\r\n\t a.\"Dedication\"," +
                           "\r\n\t e.\"Value\" as \"Linkage\",\r\n\t a.\"StartDate\",\r\n\t case when TO_VARCHAR(a.\"EndDate\") is null then '' \r\n\t when TO_VARCHAR(a.\"EndDate\") " +
                           "is not null then TO_VARCHAR(a.\"EndDate\")\r\n\t end as \"EndDate\",\r\n\t ROW_NUMBER() OVER ( PARTITION BY cuni \r\n\t\t\torder by \t\r\n\t\t\ta.\"Active\"" +
                           " desc,\r\n\t b.\"LevelId\" asc,\r\n\t c.\"Cod\" desc,\r\n\t (case when a.\"EndDate\" is null\r\n\t\t\t\tthen 1 \r\n\t\t\t\telse 0 \r\n\t\t\t\tend) desc," +
                           "\r\n\t a.\"EndDate\" desc ) AS row_num \r\n\t\tfrom "+CustomSchema.Schema+".\"ContractDetail\" a \r\n\t\tinner join "+CustomSchema.Schema+".\"Position\" b on a.\"PositionsId\" = " +
                           "b.\"Id\" \r\n\t\tinner join "+CustomSchema.Schema+".\"Dependency\" c on a.\"DependencyId\" = c.\"Id\" \r\n\t\tinner join "+CustomSchema.Schema+".\"OrganizationalUnit\" o on " +
                           "c.\"OrganizationalUnitId\" = o.\"Id\" \r\n\t\tinner join "+CustomSchema.Schema+".\"Branches\" d on c.\"BranchesId\" = d.\"Id\" \r\n\t\tinner join " +
                           ""+CustomSchema.Schema+".\"TableOfTables\"e on a.\"Linkage\"= e.\"Id\" \r\n\t\twhere d.\"Id\" " +
                           "in ( "+regional+")\r\n\t\t" +
                           "and ((a.\"EndDate\" is null and year(a.\"StartDate\")*100+month(a.\"StartDate\")<=year(TO_DATE('"+Afecha+"'))*100+month(TO_DATE('"+Afecha+"')))\r" +
                           "\n\t\tor year(TO_DATE('"+Afecha+"'))*100+month(TO_DATE('"+Afecha+"')) between year(a.\"StartDate\")*100+month(a.\"StartDate\") and year(a.\"EndDate\")*100+month(a.\"EndDate\"))) x" +
                           " \r\n\tinner join "+CustomSchema.Schema+".\"People\" p on x.cuni = p.cuni \r\n\twhere row_num = 1 \r\n\t) f \r\n\t\r\norder By f.\"Branches\", f.\"FullName\")";

           var aux = _context.Database.SqlQuery<PeopleAtDate>(query).ToList();

            
           var columnList = cabecera.Split(',');
           var excelContent = from c in aux
                   select
                   (
                       (
                           from col in columnList
                           select c.GetType().GetProperty(col).GetValue(c, null).ToString()
                       ).ToArray()
               );

           //var excelContent = aux.AsEnumerable().Select(DynamicSelectGenerator<PeopleAtDate>(cabecera)).AsQueryable();
           
            //--------------------------------------------------------Generación del excel------------------------------------------------------------------------
            //Para las columnas del excel
            /*
            string[] header = new string[]
                    {
                        "DOCUMENTO",
                        "NOMBRE COMPLETO",
                        "PRIMER APELLIDO",
                        "SEGUNDO APELLIDO",
                        "NOMBRES",
                        "APELLIDO DE CASADA",
                        "CUNI",
                        "COD DEPENDENCIA",
                        "DEPENDENCIA",
                        "COD UO",
                        "UNIDAD ORGANIZACIONAL",
                        "REGIONAL",
                        "POSICION",
                        "DEDICACION",
                        "VINCULACION",
                        "FECHA INICIO",
                        "FECHA FIN",
                        "FECHA NACIMIENTO",
                        "EDAD",
                        "TIPO DE DOCUMENTO",
                        "EXTENSION",
                        "GENERO",
                        "EMAIL INSTITUCIONAL",
                        "INTERINATO",
                        "DESCRIPCION DEL CARGO",
                        "TELEFONO"

                    };*/
            string[] header = cabecera.Split(',');
            
            var workbook = new XLWorkbook();

            //Se agrega la hoja de excel
            var ws = workbook.Worksheets.Add("PERSONAL");
            /*var range = workbook.Worksheets.Range("A1:B2");
            range.Value = "Merged A1:B2";
            range.Merge();
            range.Style.Alignment.Vertical = AlignmentVerticalValues.Top;*/
            // Título

            // Rango hoja excel
            //1,1: es la posicion inicial; 2,header.Length: es el alto y el ancho
            var rngTable = ws.Range(1, 1, 1, header.Length);

            //Bordes para las columnas
            var columns = ws.Range(1, 1, 1 + excelContent.Count(), header.Length);
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
            string[] fecha = Afecha.Split('-');
            DateTime date = DateTime.Parse(Afecha);
            //Array de bytes
            var ms = new MemoryStream();
            workbook.SaveAs(ms);
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StreamContent(ms);
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
            response.Content.Headers.ContentDisposition.FileName = "Personal-A-"+ date.Month +"-"+ date.Year+".xlsx";
            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Content.Headers.ContentLength = ms.Length;
            //La posicion para el comienzo del stream
            ms.Seek(0, SeekOrigin.Begin);
             return response;
           // return Ok(excelContent);
        }

        //Listado de novedades
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Listados/Novedades/{mes}/{gestion}")]
        public HttpResponseMessage Novedades(int mes, int gestion)
        {

            var thismonth = new DateTime(gestion, mes, 1);
            var last = thismonth.AddMonths(-1);

            string query = "CALL " + CustomSchema.Schema + ".\"COMPARATIVO_ABM\" ('" + thismonth.Year + "-" + thismonth.Month + "-" + "28', '" + last.Year + "-" + last.Month + "-" + "28')";

            var excelContent = _context.Database.SqlQuery<PeopleDifference>(query).ToList();

            //--------------------------------------------------------Generación del excel------------------------------------------------------------------------
            //Para las columnas del excel
            string[] header = new string[]
                    {
                        "NOMBRE_COMPLETO_ACTUAL",
                        "NOMBRE_COMPLETO",
                        "CUNI",
                        "COD_DEPENDENCIA",
                        "DEPENDENCIA",
                        "COD_DEPENDENCIA_ANTERIOR",
                        "DEPENDENCIA_ANTERIOR",
                        "SEDE",
                        "SEDE_ANTERIOR",
                        "POSICION",
                        "POSICION_ANTERIOR",
                        "DEDICACION",
                        "DEDICACION_ANTERIOR",
                        "VINCULACION",
                        "VINCULACION_ANTERIOR",
                        "DESCRIPCION_DEL_CARGO",
                        "DESCRIPCION_DEL_CARGO_ANTERIOR",
                        "FECHA_INICIO",
                        "FECHA_FIN",
                        "CAUSA_BAJA_MOVILIDAD",
                        "CAUSA_BAJA_MOVILIDAD_ANTERIOR",
                        "OBSERVACION"

                    };
            var workbook = new XLWorkbook();

            //Se agrega la hoja de excel
            var ws = workbook.Worksheets.Add("NOVEDADES");
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
            response.Content.Headers.ContentDisposition.FileName = "Novedades-A-" + mes + "-" + gestion + ".xlsx";
            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Content.Headers.ContentLength = ms.Length;
            //La posicion para el comienzo del stream
            ms.Seek(0, SeekOrigin.Begin);

            return response;

        }

        //Listado de novedades
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Listados/CantPersonal/{mes}/{gestion}")]
        public IHttpActionResult CantPersonal(int mes, int gestion)
        {
            string query = "CALL " + CustomSchema.Schema + ".\"PLANILLA_OVT_CONTADOR\" (" + mes + "," + gestion + ");";

            var rawResult = _context.Database.SqlQuery<AuxiliarContOVT>(query).ToList();

            return Ok(rawResult);
        }
        // Listado de Fechas de ingreso
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/FechasIngreso")]
        public IHttpActionResult FechasIngreso()
        {
            string query = "SELECT fn.\"FullName\",a.\"CUNI\", cd.\"StartDate\", cd.\"BranchesId\"" +
                           "\r\nFROM " + CustomSchema.Schema + ".\"Antiguedad\" a" +
                           "\r\ninner join " + CustomSchema.Schema + ".\"ContractDetail\" cd on cd.\"Id\" = a.\"ContractDetailId\"" +
                           "\r\ninner join " + CustomSchema.Schema + ".\"FullName\" fn on fn.cuni = a.cuni" +
                           "\r\norder by fn.\"FullName\" ";

            var rawResult = _context.Database.SqlQuery<FechaIngresoAux>(query).Select(x => new
            {
                StartDate = x.StartDate.ToString("dd MMM yyyy", new CultureInfo("es-ES")),
                x.CUNI,
                x.FullName,
                x.BranchesId
            }).AsQueryable();
            return Ok(rawResult);
        }

        // Listado de Fechas de ingreso
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/GetFechasByCUNI/{cuni}")]
        public IHttpActionResult GetFechasByCUNI(string cuni)
        {
            string query = "SELECT fn.\"FullName\", cd.\"StartDate\", cd.\"Id\"" +
                           "\r\nFROM " + CustomSchema.Schema + ".\"ContractDetail\" cd" +
                           "\r\ninner join " + CustomSchema.Schema + ".\"FullName\" fn on fn.cuni = cd.cuni" +
                           "\r\nwhere cd.cuni = '" + cuni + "'" +
                           "\r\norder by cd.\"StartDate\" desc";

            var rawResult = _context.Database.SqlQuery<FechaIngresoAux>(query).Select(x => new
            {
                x.Id,
                StartDate = x.StartDate.ToString("dd MMM yyyy", new CultureInfo("es-ES")),
                x.CUNI
            }).AsQueryable();
            return Ok(rawResult);
        }

        // Listado de Fechas de ingreso
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/GetFullNameByCUNI/{cuni}")]
        public string GetFullNameByCUNI(string cuni)
        {
            string query = "SELECT fn.\"FullName\"" +
                           "\r\nFROM " + CustomSchema.Schema + ".\"FullName\" fn" +
                           "\r\nwhere fn.cuni = '" + cuni + "'";

            var rawResult = _context.Database.SqlQuery<FechaIngresoAux>(query).Select(x => new
            {
                x.FullName
            }).FirstOrDefault();
            return rawResult.FullName;
        }
        [System.Web.Http.HttpPut]
        [System.Web.Http.Route("api/UpdateFechaIngreso/{cuni}/{id}")]
        public IHttpActionResult UpdateFechaIngreso(string cuni, int id)
        {
            
            var positionInDB = _context.Antiguedades.FirstOrDefault(d => d.CUNI == cuni && d.Activo == true);
            if (positionInDB == null)
                return NotFound();

            positionInDB.ContractDetailId = id;
            _context.SaveChanges();
            return Ok(positionInDB);
        }

        // Listado de Fechas de ingreso
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/FechasVacacion")]
        public IHttpActionResult FechasVacacion()
        {
            string query = "SELECT fn.\"FullName\",a.\"CUNI\", cd.\"StartDate\", cd.\"BranchesId\",  cv.\"CantidadDias\" as \"Rango\", years_between(\"StartDate\",current_date) \"Años\"" +
                           "\r\nFROM " + CustomSchema.Schema + ".\"Vacaciones\" a " +
                           "\r\ninner join " + CustomSchema.Schema + ".\"ContractDetail\" cd on cd.\"Id\" = a.\"ContractDetailId\"" +
                           "\r\ninner join " + CustomSchema.Schema + ".\"FullName\" fn on fn.cuni = a.cuni" +
                           "\r\ninner join " + CustomSchema.Schema + ".\"CantVacacion\" cv on years_between(\"StartDate\",current_date) between cv.\"Min\" and cv.\"Max\"" +
                           "\r\norder by fn.\"FullName\";";

            var rawResult = _context.Database.SqlQuery<FechaVacacionAux>(query).Select(x => new
            {
                StartDate = x.StartDate.ToString("dd MMM yyyy", new CultureInfo("es-ES")),
                x.CUNI,
                x.FullName,
                x.BranchesId,
                x.Rango
            }).AsQueryable();
            return Ok(rawResult);
        }
        [System.Web.Http.HttpPut]
        [System.Web.Http.Route("api/UpdateFechaVacacion/{cuni}/{id}")]
        public IHttpActionResult UpdateFechaVacacion(string cuni, int id)
        {

            var positionInDB = _context.Vacacioneses.FirstOrDefault(d => d.CUNI == cuni && d.Activo == true);
            if (positionInDB == null)
                return NotFound();

            positionInDB.ContractDetailId = id;
            _context.SaveChanges();
            return Ok(positionInDB);
        }

    }
}
