using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using ClosedXML.Excel;
using Newtonsoft.Json.Linq;
using UcbBack.Logic;
using UcbBack.Models;
using UcbBack.Models.Not_Mapped;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sap.Data.Hana;
using UcbBack.Logic.ExcelFiles;
using UcbBack.Logic.ExcelFiles.Serv;
using UcbBack.Models.Not_Mapped.ViewMoldes;
using UcbBack.Models.Serv;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using UcbBack.Logic.B1;
using UcbBack.Models.Auth;
using System.Globalization;
using DocumentFormat.OpenXml.Office2010.Excel;

namespace UcbBack.Controllers
{
    public class ServContractController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidateAuth auth;


        public ServContractController()
        {
            _context = new ApplicationDbContext();
            auth = new ValidateAuth();
        }

        [HttpGet]
        [Route("api/ServContract/History/")]
        public IHttpActionResult History()
        {
            var user = auth.getUser(Request);
            var query = "select * from " + CustomSchema.Schema + ".\"Serv_Process\" " +
                        " where \"State\" = '" + ServProcess.Serv_FileState.PendingApproval + "' " +
                        " or \"State\" = '" + ServProcess.Serv_FileState.INSAP + "' " +
                        " or \"State\" = '" + ServProcess.Serv_FileState.Rejected + "' " +
                        /*
                        " order by (" +
                        "   case when \"State\" = '" + ServProcess.Serv_FileState.PendingApproval + "' then 1 " +
                        " when \"State\" = '" + ServProcess.Serv_FileState.INSAP + "' then 3 " +
                        " when \"State\" = '" + ServProcess.Serv_FileState.Rejected + "' then 5 " +
                        " end) asc, " +
                        " \"CreatedAt\" desc;";
                         * */
                        "order by \"Id\" desc";
            var rawresult = _context.Database.SqlQuery<ServProcess>(query).ToList();

            if (rawresult.Count() == 0)
                return NotFound();

            var res = auth.filerByRegional(rawresult.AsQueryable(), user).Cast<ServProcess>();

            if (res.Count() == 0)
                return Unauthorized();

            var res2 = (from r in res
                join b in _context.Branch.ToList()
                    on r.BranchesId equals b.Id
                select new
                {
                    r.Id,
                    r.BranchesId,
                    Branches = b.Name,
                    r.FileType,
                    r.State,
                    r.SAPId,
                    CreatedAt = r.CreatedAt.ToString("dd MMM yyyy")
                }).ToList();
            return Ok(res2);
        }

        [HttpGet]
        [Route("api/ServContract/HistoryBP/{CardCode}")]
        public IHttpActionResult HistoryBp(string CardCode)
        {
            var user = auth.getUser(Request);
            var query = "select \r\nx.\"Serv_ProcessId\",\r\nx.\"CardCode\",\r\nx.\"CardName\",\r\nx.\"DependencyId\", d.\"Cod\" \"Dependency\",\r\nx.\"PEI\"," +
                        "\r\nx.\"ServiceName\",\r\nx.\"ContractAmount\",\r\nx.\"IUE\",\r\nx.\"IT\",\r\nx.\"TotalAmount\",\r\nsp.\"BranchesId\",\r\nb.\"Abr\" \"Branch\"," +
                        "\r\nsp.\"FileType\",\r\nsp.\"SAPId\", \r\nsp.\"InSAPAt\",\r\nx.\"Comments\"" +
                        "\r\nfrom" +
                        "(\r\nselect \"Serv_ProcessId\",\r\n\"CardCode\",\r\n\"CardName\",\r\n\"DependencyId\",\r\n\"PEI\",\r\n\"ServiceName\"," +
                        "\r\n\"ContractAmount\",\r\n\"IUE\",\r\n\"IT\",\r\n\"TotalAmount\",\r\n\"Comments\"" +
                        "\r\nfrom "+CustomSchema.Schema+".\"Serv_Carrera\"" +
                        "\r\nunion" +
                        "\r\nselect \"Serv_ProcessId\",\r\n\"CardCode\",\r\n\"CardName\",\r\n\"DependencyId\",\r\n\"PEI\",\r\n\"ServiceName\"," +
                        "\r\n\"ContractAmount\",\r\n\"IUE\",\r\n\"IT\",\r\n\"TotalAmount\",\r\n\"Comments\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"Serv_Paralelo\"" +
                        "\r\nunion" +
                        "\r\nselect \"Serv_ProcessId\",\r\n\"CardCode\",\r\n\"CardName\",\r\n\"DependencyId\",\r\n\"PEI\",\r\n\"ServiceName\"," +
                        "\r\n\"ContractAmount\",\r\n\"IUE\",\r\n\"IT\",\r\n\"TotalAmount\",\r\n\"Comments\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"Serv_Proyectos\"" +
                        "\r\nunion" +
                        "\r\nselect \"Serv_ProcessId\",\r\n\"CardCode\",\r\n\"CardName\",\r\n\"DependencyId\",\r\n\"PEI\",\r\n\"ServiceName\"," +
                        "\r\n\"ContractAmount\",\r\n\"IUE\",\r\n\"IT\",\r\n\"TotalAmount\",\r\n\"Comments\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"Serv_Varios\") x" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Serv_Process\" sp on sp.\"Id\" = x.\"Serv_ProcessId\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" b on b.\"Id\" = sp.\"BranchesId\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Dependency\" d on d.\"Id\" =x.\"DependencyId\"" +
                        "\r\nwhere sp.\"State\" = 'IN SAP' and sp.\"SAPId\" is not null and x.\"CardCode\" = '" + CardCode + "'" +
                        "\r\norder by sp.\"Id\"";
            var rawresult = _context.Database.SqlQuery<HistorialBPSarai>(query).ToList();

            if (rawresult.Count() == 0)
                return NotFound();

            var res = auth.filerByRegional(rawresult.AsQueryable(), user).Cast<HistorialBPSarai>().Select(x => new
            {
                x.Serv_ProcessId,
                x.CardCode,
                x.CardName,
                x.Dependency,
                x.PEI,
                x.ServiceName,
                x.ContractAmount,
                x.IUE,
                x.IT,
                 x.TotalAmount,
                x.Branch,
                x.FileType,
                x.SAPId,
                x.InSAPAt,
                x.Comments

            });

            if (res.Count() == 0)
                return Unauthorized();
           
            return Ok(res);
        }

        [HttpGet]
        [Route("api/ServContract/HistoryBP")]
        public IHttpActionResult HistoryDocentes()
        {
            var user = auth.getUser(Request);
            var query = "select \r\nx.\"CardCode\",\r\nx.\"CardName\",\r\nsp.\"BranchesId\"" +
                        "\r\nfrom" +
                        "(\r\nselect \"Serv_ProcessId\",\r\n\"CardCode\",\r\n\"CardName\",\r\n\"DependencyId\",\r\n\"PEI\",\r\n\"ServiceName\"," +
                        "\r\n\"ContractAmount\",\r\n\"IUE\",\r\n\"IT\",\r\n\"TotalAmount\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"Serv_Carrera\"" +
                        "\r\nunion" +
                        "\r\nselect \"Serv_ProcessId\",\r\n\"CardCode\",\r\n\"CardName\",\r\n\"DependencyId\",\r\n\"PEI\",\r\n\"ServiceName\"," +
                        "\r\n\"ContractAmount\",\r\n\"IUE\",\r\n\"IT\",\r\n\"TotalAmount\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"Serv_Paralelo\"" +
                        "\r\nunion" +
                        "\r\nselect \"Serv_ProcessId\",\r\n\"CardCode\",\r\n\"CardName\",\r\n\"DependencyId\",\r\n\"PEI\",\r\n\"ServiceName\"," +
                        "\r\n\"ContractAmount\",\r\n\"IUE\",\r\n\"IT\",\r\n\"TotalAmount\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"Serv_Proyectos\"" +
                        "\r\nunion" +
                        "\r\nselect \"Serv_ProcessId\",\r\n\"CardCode\",\r\n\"CardName\",\r\n\"DependencyId\",\r\n\"PEI\",\r\n\"ServiceName\"," +
                        "\r\n\"ContractAmount\",\r\n\"IUE\",\r\n\"IT\",\r\n\"TotalAmount\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"Serv_Varios\") x" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Serv_Process\" sp on sp.\"Id\" = x.\"Serv_ProcessId\"" +
                        "\r\nwhere sp.\"State\" = 'IN SAP' and sp.\"SAPId\" is not null\r\ngroup by x.\"CardCode\", " +
                        "x.\"CardName\",sp.\"BranchesId\"\r\norder by x.\"CardName\"";
            var rawresult = _context.Database.SqlQuery<HistorialBPSarai>(query).ToList();

            if (rawresult.Count() == 0)
                return NotFound();

            var res = auth.filerByRegional(rawresult.AsQueryable(), user).Cast<HistorialBPSarai>();

            if (res.Count() == 0)
                return Unauthorized();
            /*
            var res2 = (from r in res
                join b in _context.Branch.ToList()
                    on r.BranchesId equals b.Id
                select new
                {
                   r.Serv_ProcessId,
                   r.CardCode,
                   r.CardName,
                   r.ServiceName,
                    CreatedAt = r.CreatedAt.ToString("dd MMM yyyy")
                }).ToList();
             * */
            return Ok(res);
        }
        [HttpGet]
        [Route("api/ServContract/PendingApproval/")]
        public IHttpActionResult PendingApproval()
        {
            var user = auth.getUser(Request);
            var query = "select * from " + CustomSchema.Schema + ".\"Serv_Process\" " +
                        " where \"State\" = '" + ServProcess.Serv_FileState.PendingApproval + "' " +
                        " order by (" +
                        "   case when \"State\" = '" + ServProcess.Serv_FileState.PendingApproval + "' then 1 " +
                        " end) asc, " +
                        " \"CreatedAt\" desc;";
            var rawresult = _context.Database.SqlQuery<ServProcess>(query).ToList();

            if (rawresult.Count() == 0)
                return NotFound();

            var res = auth.filerByRegional(rawresult.AsQueryable(), user).Cast<ServProcess>();

            if (res.Count() == 0)
                return Unauthorized();

            var res2 = (from r in res
                        join b in _context.Branch.ToList()
                            on r.BranchesId equals b.Id
                        select new
                        {
                            r.Id,
                            r.BranchesId,
                            Branches = b.Name,
                            r.FileType,
                            r.State,
                            r.SAPId,
                            r.TipoDocente,
                            CreatedAt = r.CreatedAt.ToString("dd MMM yyyy")
                        }).ToList();
            return Ok(res2);
        }

        [HttpGet]
        [Route("api/ServContract/{id}")]
        public IHttpActionResult Get(int id)
        {
            var user = auth.getUser(Request);
            var rawresult = _context.ServProcesses.Where(x=>x.Id==id);

            if (rawresult.Count() == 0)
                return NotFound();

            var res = auth.filerByRegional(rawresult, user).Cast<ServProcess>();

            if (res.Count() == 0)
                return Unauthorized();

            return Ok(res.FirstOrDefault());
        }

        [HttpPost]
        [Route("api/ServContractgenerateExcel/")]
        public HttpResponseMessage generateExcel(JObject data)
        {
            HttpResponseMessage response = new HttpResponseMessage();
            var list = data["list"].ToObject<List<int>>();
            var ex = new XLWorkbook();

            var excelData = getData(list, data["tipo"].ToString());

            ex.Worksheets.Add(excelData, "Plantilla_" + data["tipo"].ToString());
            var ms = new MemoryStream();
            ex.SaveAs(ms);
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StreamContent(ms);
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
            response.Content.Headers.ContentDisposition.FileName = "Plantilla_" + data["tipo"].ToString() + ".xlsx";
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Content.Headers.ContentLength = ms.Length;
            ms.Seek(0, SeekOrigin.Begin);
            return response;
        }

        [HttpDelete]
        [Route("api/ServContract/UploadFile")]
        public IHttpActionResult DeleteFile(JObject data)
        {
            var user = auth.getUser(Request);
            //todo add validation of user by branch
            int branchesid;
            if (data["BranchesId"] == null || data["FileType"] == null || !Int32.TryParse(data["BranchesId"].ToString(), out branchesid))
            {
                ModelState.AddModelError("Mal Formato", "Debes enviar BranchesId y FileType");
                return BadRequest();
            }

            string type = data["FileType"].ToString();
            var process = _context.ServProcesses.FirstOrDefault(x =>
                x.BranchesId == branchesid && x.FileType == type && x.State == ServProcess.Serv_FileState.Started);
            if (process == null)
                return NotFound();
            process.State = ServProcess.Serv_FileState.Canceled;
            process.LastUpdatedBy = user.Id;
            _context.SaveChanges();
            return Ok();
        }
        [HttpPost]
        [Route("api/ServContract/UploadFile")]
        public async Task<HttpResponseMessage> UploadORExcel()
        {
            var response = new HttpResponseMessage();
            try
            {
                var req = await Request.Content.ReadAsMultipartAsync();
                dynamic o = HttpContentToVariables(req).Result;

                if (!((IDictionary<string, object>)o).ContainsKey("BranchesId")
                    || !((IDictionary<string, object>)o).ContainsKey("FileType")
                    || !((IDictionary<string, object>)o).ContainsKey("fileName")
                    || !((IDictionary<string, object>)o).ContainsKey("excelStream")
                    || !o.fileName.ToString().EndsWith(".xlsx"))
                {
                    //escribir aquí los parámetros del excel que quiero ver||en qué parte del excel se manda eso y como es que el fonrtend sube la info del excel si solo pasa las branches , el nombre 'file' y el filetype CC_XXX
                    var name = o.fileName;
                    var branches = o.BranchesId;
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors", "{ \"Faltan datos\": \"Debe enviar mes(mm), gestion(yyyy), segmentoOrigen(id) y un archivo excel llamado file (en formato .xlsx)\"}");
                    response.Content = new StringContent("Debe enviar mes(mm), gestion(yyyy), segmentoOrigen(id) y un archivo excel llamado file");
                    return response;
                }


                //todo validate FileType
                // ...


                string realFileName;
                if (!verifyName(o.fileName, o.BranchesId, o.FileType, out realFileName))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors", "{ \"Nombre Incorrecto\": \"El archivo enviado no cumple con la regla de nombres. Nombre sugerido: " + realFileName + "\"}");
                    response.Content = new StringContent("El archivo enviado no cumple con la regla de nombres.");
                    return response;
                }

                var user = auth.getUser(Request);

                int userid = Int32.Parse(Request.Headers.GetValues("id").First());
                ServProcess file = AddFileToProcess(Int32.Parse(o.BranchesId.ToString()), o.FileType.ToString(), userid);

                if (file == null)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors", "{ \"Ya se Subio archivos para este mes\": \"Ya se subio  datos para este mes, si quiere volver a subir cancele el anterior archivo.\"}");
                    response.Content = new StringContent("Ya se subió  datos para este mes, si quiere volver a subir cancele el anterior archivo.");
                    return response;
                }

                DynamicExcelToDB(o.FileType,o,file,user,out response);
                return response;
            }
            catch (System.ArgumentException e)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors", "{ \"Formato Archivo Invalido\": \"Por favor enviar un archivo en formato excel (.xlsx)\"}");
                response.Content = new StringContent("Por favor enviar un archivo en formato excel (.xlsx)" + e);
                return response;
            }
            catch (System.IO.IOException e)
            {
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors", "{ \"Archivo demasiado grande\": \"El archivo es demasiado grande para ser procesado.\"}");
                response.Content = new StringContent("El archivo es demasiado grande para ser procesado.");
                return response;
            }
            catch (HanaException e)
            {
                if (e.NativeError == 258)
                {
                    Console.WriteLine(e);
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors", "{ \"La conexion con SAP se perdio\": \"No se pudo validar el archivo con SAP\"}");
                    response.Content = new StringContent("Error conexion SAP");
                    return response;
                }
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors", "{ \"La conexion con SAP se perdio\": \"No se pudo validar el archivo con con SAP\"}");
                response.Content = new StringContent("Error conexion SAP");
                return response;
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors", "{ \"Ocurrió un problema\": \"Contactese con el administrador.\"}");
                response.Content = new StringContent(e.ToString());
                return response;
            }
        }

        [HttpGet]
        [Route("api/ServContract/GetDetail/{id}")]
        public IHttpActionResult GetDetail(int id)
        {
            var process = _context.ServProcesses.FirstOrDefault(p => p.Id == id);
            if (process == null)
                return NotFound();
            string query = null;
            switch (process.FileType)
            {
                //el query se hizo de forma estática con el nombre de BD "ADMNALRRHHOLD", se cambio por "ADMNALRRHH_PRUEBA"
                case ServProcess.Serv_FileType.Varios:
                    query =
                        "select sv.\"CardName\", ou.\"Cod\" as \"OU\", sv.\"PEI\", sv.\"ServiceName\" as \"Memo\",  " +
                        " sv.\"ContractObjective\" as \"LineMemo\", sv.\"AssignedAccount\", sv.\"TotalAmount\" as \"Debit\"," +
                        "sv.\"ContractAmount\" as \"Credit\"" +
                        " from " + CustomSchema.Schema + ".\"Serv_Varios\" sv " +
                        " inner join " + CustomSchema.Schema + ".\"Dependency\" d " +
                        " on sv.\"DependencyId\" = d.\"Id\" " +
                        " inner join " + CustomSchema.Schema + ".\"OrganizationalUnit\" ou " +
                        " on d.\"OrganizationalUnitId\" = ou.\"Id\" " +
                        " where \"Serv_ProcessId\" = " + process.Id +
                        " order by sv.\"Id\" asc;";

                    break;
                case ServProcess.Serv_FileType.Carrera:
                    query =
                        "select sv.\"CardName\", ou.\"Cod\" as \"OU\", sv.\"PEI\", sv.\"ServiceName\" as \"Memo\",  " +
                        " sv.\"AssignedJob\"||\' \'||sv.\"Carrera\"||\' \'||sv.\"Student\" as \"LineMemo\", sv.\"AssignedAccount\", sv.\"TotalAmount\" as \"Debit\"," +
                        "sv.\"ContractAmount\" as \"Credit\"" +
                        " from " + CustomSchema.Schema + ".\"Serv_Carrera\" sv " +
                        " inner join " + CustomSchema.Schema + ".\"Dependency\" d " +
                        " on sv.\"DependencyId\" = d.\"Id\" " +
                        " inner join " + CustomSchema.Schema + ".\"OrganizationalUnit\" ou " +
                        " on d.\"OrganizationalUnitId\" = ou.\"Id\"" +
                        " where \"Serv_ProcessId\" = " + process.Id +
                        " order by sv.\"Id\" asc;";

                    break;
                case ServProcess.Serv_FileType.Proyectos:
                    query =
                        "select sv.\"CardName\", ou.\"Cod\" as \"OU\", sv.\"PEI\", sv.\"ServiceName\" as \"Memo\",  " +
                        " sv.\"ProjectSAPName\" as \"LineMemo\", sv.\"AssignedAccount\", sv.\"TotalAmount\" as \"Debit\"," +
                        "sv.\"ContractAmount\" as \"Credit\"" +
                        " from " + CustomSchema.Schema + ".\"Serv_Proyectos\" sv " +
                        " inner join " + CustomSchema.Schema + ".\"Dependency\" d " +
                        " on sv.\"DependencyId\" = d.\"Id\" " +
                        " inner join " + CustomSchema.Schema + ".\"OrganizationalUnit\" ou " +
                        " on d.\"OrganizationalUnitId\" = ou.\"Id\"" +
                        " where \"Serv_ProcessId\" = " + process.Id +
                        " order by sv.\"Id\" asc;";

                    break;
                case ServProcess.Serv_FileType.Paralelo:
                    query =
                        "select sv.\"CardName\", ou.\"Cod\" as \"OU\", sv.\"PEI\", sv.\"ServiceName\" as \"Memo\",  " +
                        " sv.\"Sigla\" as \"LineMemo\", sv.\"AssignedAccount\", sv.\"TotalAmount\" as \"Debit\"," +
                        "sv.\"ContractAmount\" as \"Credit\"" +
                        " from " + CustomSchema.Schema + ".\"Serv_Paralelo\" sv " +
                        " inner join " + CustomSchema.Schema + ".\"Dependency\" d " +
                        " on sv.\"DependencyId\" = d.\"Id\" " +
                        " inner join " + CustomSchema.Schema + ".\"OrganizationalUnit\" ou " +
                        " on d.\"OrganizationalUnitId\" = ou.\"Id\"" +
                        " where \"Serv_ProcessId\" = " + process.Id +
                        " order by sv.\"Id\" asc;";

                    break;
            }
            if (query == null)
                return NotFound();
            
            //IEnumerable<Serv_Voucher> voucher = _context.Database.SqlQuery<Serv_Voucher>(query).ToList();

            var voucher = _context.Database.SqlQuery<Serv_Voucher>(query).ToList();
            var filteredList = voucher
                .Select(x => new
                {
                    x.CardName, 
                    x.CardCode,
                    x.OU,
                    x.PEI,
                    x.Carrera,
                    x.Paralelo,
                    x.Periodo,
                    x.ProjectCode,
                    x.Memo,
                    x.LineMemo,
                    x.Concept,
                    x.AssignedAccount,
                    x.Account,
                    Debit = string.Format("{0,00}", x.Debit),
                    Credit = string.Format("{0,00}", x.Credit)
                }); ;

            return Ok(filteredList);
        }

        [HttpPost]
        [Route("api/ServContract/CheckUpload")]
        public IHttpActionResult CheckUpload([FromBody] JObject upload)
        {
            Console.WriteLine("Datos recibidos en la solicitud POST:");
            Console.WriteLine(upload);

            int branchid = 0;
            int processid = 0;
            if (upload["FileType"] == null || upload["BranchesId"] == null || !Int32.TryParse(upload["BranchesId"].ToString(), out branchid) || upload["ProcessId"] == null || upload["TipoDocente"] == null)
                return BadRequest("Debes enviar Tipo de Archivo, segmentoOrigen");

      
            var FileType = upload["FileType"].ToString();
            var TipoDocente = upload["TipoDocente"].ToString();

            ServProcess process = null;

            if (Int32.TryParse(upload["ProcessId"].ToString(), out processid))
            {
                process = _context.ServProcesses.FirstOrDefault(f => f.BranchesId == branchid
                                                                     && f.Id == processid);
            }
            else
            {
                process = _context.ServProcesses.FirstOrDefault(f => f.BranchesId == branchid
                                                                     && (f.State == ServProcess.Serv_FileState.Started)
                                                                     && f.FileType == FileType);

            }
            if (process == null)
                return Ok();

            List<string> tipos = new List<string>();
            tipos.Add(FileType);
            process.TipoDocente = TipoDocente;

            // Actualiza solo la propiedad TipoDocente en la base de datos
            _context.Entry(process).Property(x => x.TipoDocente).IsModified = true;
            _context.SaveChanges();
            dynamic res = new JObject();
            res.array = JToken.FromObject(tipos);
            res.id = process.Id;
            res.state = process.State;
            res.tipoDocente = process.TipoDocente;
            return Ok(res);
        }


        [HttpGet]
        [Route("api/ServContract/GetDistributionPDF/{id}")]
        public IHttpActionResult GetDistributionPDF(int id)
        {
            string query = "";
            var report = new List<Serv_PDF>();
            var process = _context.ServProcesses.Include(x => x.Branches).FirstOrDefault(p => p.Id == id);

            //query para generar todos los datos de cada docente, ordenado por carrera y docente
            switch (process.FileType)
            {
                case ServProcess.Serv_FileType.Varios:
                    //obtiene el cuerpo de la tabla para el PDF
                    //join para el nombre de la carrera
                    query = "select\r\nsv.\"Id\",\r\nsv.\"CardCode\" \"Codigo_Socio\",\r\nsv.\"CardName\" \"Nombre_Socio\"," +
                            "\r\ndep.\"Cod\" \"Cod_Dependencia\",\r\nou.\"Cod\" \"Cod_UO\",\r\nsv.\"PEI\" \"PEI_PO\"," +
                            "\r\nsv.\"ServiceName\" \"Nombre_del_Servicio\",\r\nsv.\"ContractObjective\" \"Objeto_del_Contrato\"," +
                            "\r\nsv.\"AssignedAccount\" \"Cuenta\",\r\nsv.\"ContractAmount\" \"Contrato\"," +
                            "\r\nsv.\"IUE\" \"IUE\",\r\nsv.\"IT\" \"IT\",\r\nsv.\"TotalAmount\" \"xPagar\"," +
                            "\r\nsv.\"Comments\" \"Observaciones\", sp.\"BranchesId\"\r\n" +
                            "from " +CustomSchema.Schema + ".\"Serv_Varios\" sv" +
                            "\r\ninner join " +CustomSchema.Schema + ".\"Dependency\" dep\r\non dep.\"Id\" = sv.\"DependencyId\"" +
                            " inner join " + CustomSchema.Schema + ".\"Serv_Process\" sp on sv.\"Serv_ProcessId\"= sp.\"Id\"" +
                            "\r\ninner join " +CustomSchema.Schema + ".\"OrganizationalUnit\" ou\r\non ou.\"Id\" = dep.\"OrganizationalUnitId\"" +
                            "\r\nwhere \"Serv_ProcessId\" = "+id+
                            "\r\nunion\r\nselect sum(sv.\"Id\"),\r\n'' \"Codigo_Socio\",\r\n'' \"Nombre_Socio\",\r\n''" +
                            " \"Cod_Dependencia\", \r\n'' \"Cod_UO\",\r\n'' \"PEI_PO\",\r\n'' \"Nombre_del_Servicio\",\r\n''" +
                            " \"Objeto_del_Contrato\",\r\n'' \"Cuenta_Asignada\",\r\nsum(sv.\"ContractAmount\") \"Monto_Contrato\"," +
                            "\r\nsum(sv.\"IUE\") \"Monto_IUE\",\r\nsum(sv.\"IT\") \"Monto_IT\",\r\nsum(sv.\"TotalAmount\") \"Monto_a_Pagar\",\r\n'' \"Observaciones\", max(sp.\"BranchesId\") " +
                            "\r\nfrom " + CustomSchema.Schema + ".\"Serv_Varios\" sv" +
                            " inner join " + CustomSchema.Schema + ".\"Serv_Process\" sp on sv.\"Serv_ProcessId\"= sp.\"Id\"" +
                            "\r\nwhere \"Serv_ProcessId\" =" +id +
                            "\r\norder by \"Id\" asc ";
                    report = _context.Database.SqlQuery<Serv_PDF>(query).ToList();
                    break;

                case ServProcess.Serv_FileType.Carrera:
                    //obtiene los resultados al pie de cada tabla, por carrera
                    query = "select\r\nsv.\"Id\",\r\nsv.\"CardCode\" \"Codigo_Socio\",\r\nsv.\"CardName\" \"Nombre_Socio\",\r\ndep.\"Cod\" \"Cod_Dependencia\",\r\nou.\"Cod\" \"Cod_UO\"," +
                            "\r\nsv.\"PEI\" \"PEI_PO\",\r\nsv.\"ServiceName\" \"Nombre_del_Servicio\",\r\nsv.\"Carrera\" \"Codigo_Carrera\",\r\nsv.\"DocumentNumber\" \"Documento_Base\"," +
                            "\r\nsv.\"Student\" \"Postulante\",\r\nsv.\"AssignedJob\" \"Tarea_Asignada\",\r\nsv.\"AssignedAccount\" \"Cuenta\",\r\nsv.\"ContractAmount\" \"Contrato\"," +
                            "\r\nsv.\"IUE\" \"IUE\",\r\nsv.\"IT\" \"IT\",\r\nsv.\"IUEExterior\" \"IUEExterior\",\r\nsv.\"TotalAmount\" \"xPagar\",\r\nsv.\"Comments\" \"Observaciones\", sp.\"BranchesId\"" +
                            "from " +CustomSchema.Schema + ".\"Serv_Carrera\" sv" +
                            "\r\ninner join " +CustomSchema.Schema + ".\"Dependency\" dep\r\non dep.\"Id\" = sv.\"DependencyId\"" +
                            "\r\ninner join " +CustomSchema.Schema + ".\"OrganizationalUnit\" ou\r\non ou.\"Id\" = dep.\"OrganizationalUnitId\"" +
                            " inner join " + CustomSchema.Schema + ".\"Serv_Process\" sp on sv.\"Serv_ProcessId\"= sp.\"Id\"" +
                            "\r\nwhere \"Serv_ProcessId\" = "+id+
                            "\r\n union" +
                            "\r\n select\r\nsum(sv.\"Id\"),\r\n'' \"Codigo_Socio\",\r\n'' \"Nombre_Socio\",\r\n'' " +
                            "\"Cod_Dependencia\",\r\n'' \"Cod_UO\",\r\n'' \"PEI_PO\",\r\n'' \"Nombre_del_Servicio\",\r\n'' \"Codigo_Carrera\",\r\n'' " +
                            "\"Documento_Base\",\r\n'' \"Postulante\",\r\n'' \"Tipo_Tarea_Asignada\",\r\n'' \"Cuenta_Asignada\",\r\nsum(sv.\"ContractAmount\") " +
                            "\"Monto_Contrato\",\r\nsum(sv.\"IUE\") \"Monto_IUE\",\r\nsum(sv.\"IT\") \"Monto_IT\", \r\nsum(sv.\"IUEExterior\") \"IUEExterior\",\r\nsum(sv.\"TotalAmount\") \"Monto_a_Pagar\",\r\n'' \"Observaciones\", max(sp.\"BranchesId\") " +
                            "\r\nfrom  " + CustomSchema.Schema + ".\"Serv_Carrera\" sv" +
                            " inner join " + CustomSchema.Schema + ".\"Serv_Process\" sp on sv.\"Serv_ProcessId\"= sp.\"Id\"" +
                            "\r\n where \"Serv_ProcessId\" = "+id+"\r\n order by \"Id\"";
                    report = _context.Database.SqlQuery<Serv_PDF>(query).ToList();
                    break;

                case ServProcess.Serv_FileType.Paralelo:
                    //obtiene los resultados al pie de cada tabla, por carrera
                    query = "select\r\nsv.\"Id\",\r\nsv.\"CardCode\" \"Codigo_Socio\",\r\nsv.\"CardName\" \"Nombre_Socio\",\r\ndep.\"Cod\" \"Cod_Dependencia\",\r\nou.\"Cod\" \"Cod_UO\"," +
                            "\r\nsv.\"PEI\" \"PEI_PO\",\r\nsv.\"ServiceName\" \"Nombre_del_Servicio\",\r\nsv.\"Periodo\" \"Periodo_Academico\",\r\nsv.\"Sigla\" \"Sigla_Asignatura\"," +
                            "\r\nsv.\"ParalelNumber\" \"Paralelo\",\r\nsv.\"ParalelSAP\" \"Codigo_Paralelo_SAP\",\r\nsv.\"AssignedAccount\" \"Cuenta\"," +
                            "\r\nsv.\"ContractAmount\" \"Contrato\",\r\nsv.\"IUE\" \"IUE\",\r\nsv.\"IT\" \"IT\",\r\nsv.\"TotalAmount\" \"xPagar\"," +
                            "\r\nsv.\"Comments\" \"Observaciones\", sp.\"BranchesId\"" +
                            "from " +CustomSchema.Schema + ".\"Serv_Paralelo\" sv" +
                            "\r\ninner join " +CustomSchema.Schema + ".\"Dependency\" dep\r\non dep.\"Id\" = sv.\"DependencyId\"" +
                            "\r\ninner join " + CustomSchema.Schema + ".\"OrganizationalUnit\" ou\r\non ou.\"Id\" = dep.\"OrganizationalUnitId\"" +
                            " inner join " + CustomSchema.Schema + ".\"Serv_Process\" sp on sv.\"Serv_ProcessId\"= sp.\"Id\"" +
                            "\r\nwhere \"Serv_ProcessId\" = "+id+
                            "\r\nunion" +
                            "\r\n select \r\n sum(sv.\"Id\"),\r\n'' \"Codigo_Socio\",\r\n'' \"Nombre_Socio\",\r\n'' \"Cod_Dependencia\"," +
                            "\r\n'' \"Cod_UO\",\r\n'' \"PEI_PO\",\r\n'' \"Nombre_del_Servicio\",\r\n'' \"Periodo_Academico\",\r\n'' \"Sigla_Asignatura\"," +
                            "\r\n'' \"Paralelo\",\r\n'' \"Codigo_Paralelo_SAP\",\r\n'' \"Cuenta_Asignada\",\r\n  sum(sv.\"ContractAmount\") \"Monto_Contrato\"," +
                            "\r\n  sum(sv.\"IUE\") \"Monto_IUE\",\r\n  sum(sv.\"IT\") \"Monto_IT\",\r\n  sum(sv.\"TotalAmount\") \"Monto_a_Pagar\",\r\n'' \"Observaciones\", max(sp.\"BranchesId\")" +
                            "\r\nfrom " + CustomSchema.Schema + ".\"Serv_Paralelo\" sv" +
                            " inner join " + CustomSchema.Schema + ".\"Serv_Process\" sp on sv.\"Serv_ProcessId\"= sp.\"Id\"" +
                            "\r\nwhere \"Serv_ProcessId\" = " + id +
                            "\r\norder by\"Id\"";
                    report = _context.Database.SqlQuery<Serv_PDF>(query).ToList();
                    break;
                case ServProcess.Serv_FileType.Proyectos:
                    //obtiene los resultados al pie de cada tabla, por carrera
                    query = "\r\nselect\r\nsv.\"Id\",\r\nsv.\"CardCode\" \"Codigo_Socio\",\r\nsv.\"CardName\" \"Nombre_Socio\",\r\ndep.\"Cod\" \"Cod_Dependencia\"," +
                            "\r\nou.\"Cod\" \"Cod_UO\",\r\nsv.\"PEI\" \"PEI_PO\",\r\nsv.\"ServiceName\" \"Nombre_del_Servicio\",\r\nsv.\"ProjectSAPCode\" \"Codigo_Proyecto_SAP\"," +
                            "\r\nsv.\"ProjectSAPName\" \"Nombre_del_Proyecto\",\r\nsv.\"Version\",\r\nsv.\"AssignedJob\" \"Tarea_Asignada\",\r\nsv.\"AssignedAccount\" \"Cuenta\"," +
                            "\r\nsv.\"ContractAmount\" \"Contrato\",\r\nsv.\"IUE\" \"IUE\",\r\nsv.\"IT\" \"IT\",\r\nsv.\"TotalAmount\" \"xPagar\",\r\nsv.\"Comments\" " +
                            "\"Observaciones\", sp.\"BranchesId\"" +
                            "from " + CustomSchema.Schema + ".\"Serv_Proyectos\" sv" +
                            " inner join " + CustomSchema.Schema + ".\"Serv_Process\" sp on sv.\"Serv_ProcessId\"= sp.\"Id\"" +
                            "\r\ninner join " +CustomSchema.Schema + ".\"Dependency\" dep\r\non dep.\"Id\" = sv.\"DependencyId\"" +
                            "\r\ninner join " +CustomSchema.Schema + ".\"OrganizationalUnit\" ou\r\non ou.\"Id\" = dep.\"OrganizationalUnitId\"" +
                            "\r\nwhere \"Serv_ProcessId\" = "+id+
                            "\r\nunion" +
                            "\r\nselect \r\nsum(sv.\"Id\"), \r\n'' \"Codigo_Socio\", \r\n'' \"Nombre_Socio\", \r\nnull \"Cod_Dependencia\", \r\nnull \"Cod_UO\", \r\n'' \"PEI_PO\", \r\n'' \"Nombre_del_Servicio\", \r\n'' \"Codigo_Proyecto_SAP\", \r\n'' \"Nombre_del_Proyecto\", \r\nnull \"Version\", \r\n'' \"Tipo_Tarea_Asignada\", \r\n'' \"Cuenta_Asignada\", \r\n sum(sv.\"ContractAmount\") \"Monto_Contrato\", \r\n sum(sv.\"IUE\") \"Monto_IUE\", \r\n sum(sv.\"IT\") \"Monto_IT\", \r\n sum(sv.\"TotalAmount\") \"Monto_a_Pagar\", \r\n'' \"Observaciones\", max(sp.\"BranchesId\")" +
                            "\r\nfrom " + CustomSchema.Schema + ".\"Serv_Proyectos\" sv" +
                            " inner join " + CustomSchema.Schema + ".\"Serv_Process\" sp on sv.\"Serv_ProcessId\"= sp.\"Id\"" +
                            "\r\n where \"Serv_ProcessId\" = " + id + " \r\norder by \"Id\" asc ";
                    report = _context.Database.SqlQuery<Serv_PDF>(query).ToList();
                    break;

                default:
                    return BadRequest();
            }
            //Filtro de datos por regional
            var user = auth.getUser(Request);
            if (process.FileType.Equals("VARIOS"))
            {
                var filteredListBody = auth.filerByRegional(report.AsQueryable(), user).ToList().Select(x => new
                {
                    x.Id,
                    x.Nombre_Socio,
                    x.Cod_UO,
                    x.PEI_PO,
                    x.Nombre_del_Servicio,
                    x.Objeto_del_Contrato,
                    x.Cuenta,
                    x.Contrato,
                    x.IUE,
                    x.IT,
                    x.IUEExterior,
                    x.xPagar,
                    x.Observaciones
                    
                });

                return Ok(filteredListBody);
            }
            else if (process.FileType.Equals("CARRERA"))
            {
                var filteredListResult = auth.filerByRegional(report.AsQueryable(), user).ToList().Select(x => new
                {
                    x.Id,
                    x.Nombre_Socio,
                    x.Cod_UO,
                    x.PEI_PO,
                    x.Nombre_del_Servicio,
                    x.Codigo_Carrera,
                    x.Documento_Base,
                    x.Postulante,
                    x.Tarea_Asignada,
                    x.Cuenta,
                    x.Contrato,
                    x.IUE,
                    x.IT,
                    x.IUEExterior,
                    x.xPagar,
                    x.Observaciones
                });
                return Ok(filteredListResult);
            }
            else if (process.FileType.Equals("PARALELO"))
            {
                var filteredListResult = auth.filerByRegional(report.AsQueryable(), user).ToList().Select(x => new
                {
                    x.Id,
                    x.Nombre_Socio,
                    x.Cod_UO,
                    x.PEI_PO,
                    x.Nombre_del_Servicio,
                    x.Sigla_Asignatura,
                    x.Paralelo,
                    x.Codigo_Paralelo_SAP,
                    x.Cuenta,
                    x.Contrato,
                    x.IUE,
                    x.IT,
                    x.IUEExterior,
                    x.xPagar,
                    x.Observaciones
                });
                return Ok(filteredListResult);
            }
            else
            {
                var filteredListResult = auth.filerByRegional(report.AsQueryable(), user).ToList().Select(x => new
                {
                    x.Id,
                    x.Nombre_Socio,
                    x.Cod_UO,
                    x.PEI_PO,
                    x.Nombre_del_Servicio,
                    x.Codigo_Proyecto_SAP,
                    x.Nombre_del_Proyecto,
                    x.Version,
                    x.Tarea_Asignada,
                    x.Cuenta,
                    x.Contrato,
                    x.IUE,
                    x.IT,
                    x.IUEExterior,
                    x.xPagar,
                    x.Observaciones
                });
                return Ok(filteredListResult);
            }
        }

        [HttpGet]
        [Route("api/ServContract/GetDistribution/{id}")]
        public HttpResponseMessage GetDistribution (int id)
        {
            HttpResponseMessage response = new HttpResponseMessage();

            var process = _context.ServProcesses.Include(x => x.Branches).FirstOrDefault(p => p.Id == id);

            if (process == null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                return response;
            }
            var ex = new XLWorkbook();
            var d = new Distribution();
            switch (process.FileType)
            {
                case ServProcess.Serv_FileType.Varios:
                    var dist = _context.ServVarioses.Include(x => x.Dependency).Include(x => x.Dependency.OrganizationalUnit).
                        Where(x => x.Serv_ProcessId == process.Id).Select(x => new
                        {
                            Id = x.Id,
                            Codigo_Socio = x.CardCode,
                            Nombre_Socio = x.CardName,
                            Cod_Dependencia = x.Dependency.Cod,
                            Cod_UO = x.Dependency.OrganizationalUnit.Cod,
                            PEI_PO = x.PEI,
                            Nombre_del_Servicio = x.ServiceName,
                            Objeto_del_Contrato = x.ContractObjective,
                            Cuenta_Asignada = x.AssignedAccount,
                            Monto_Contrato = x.ContractAmount,
                            Monto_IUE = x.IUE,
                            Monto_IT = x.IT,
                            Monto_a_Pagar = x.TotalAmount,
                            Observaciones = x.Comments,
                        }).OrderBy(x => x.Id);
                    ex.Worksheets.Add(d.CreateDataTable(dist), "TotalDetalle");
                    break;
                case ServProcess.Serv_FileType.Carrera:
                    var dist1 = _context.ServCarreras.Include(x => x.Dependency).Include(x => x.Dependency.OrganizationalUnit).
                        Where(x => x.Serv_ProcessId == process.Id).Select(x => new
                        {
                            Id = x.Id,
                            Codigo_Socio = x.CardCode,
                            Nombre_Socio = x.CardName,
                            Cod_Dependencia = x.Dependency.Cod,
                            Cod_UO = x.Dependency.OrganizationalUnit.Cod,
                            PEI_PO = x.PEI,
                            Nombre_del_Servicio = x.ServiceName,
                            Codigo_Carrera = x.Carrera,
                            Documento_Base = x.DocumentNumber,
                            Postulante = x.Student,
                            Tipo_Tarea_Asignada = x.AssignedJob,
                            Cuenta_Asignada = x.AssignedAccount,
                            Monto_Contrato = x.ContractAmount,
                            Monto_IUE = x.IUE,
                            Monto_IT = x.IT,
                            IUEExterior = x.IUEExterior,
                            Monto_a_Pagar = x.TotalAmount,
                            Observaciones = x.Comments,
                        }).OrderBy(x => x.Id);
                    ex.Worksheets.Add(d.CreateDataTable(dist1), "TotalDetalle");
                    break;
                case ServProcess.Serv_FileType.Paralelo:
                    var dist2 = _context.ServParalelos.Include(x => x.Dependency).Include(x => x.Dependency.OrganizationalUnit).
                        Where(x => x.Serv_ProcessId == process.Id).Select(x => new
                        {
                            Id = x.Id,
                            Codigo_Socio = x.CardCode,
                            Nombre_Socio = x.CardName,
                            Cod_Dependencia = x.Dependency.Cod,
                            Cod_UO = x.Dependency.OrganizationalUnit.Cod,
                            PEI_PO = x.PEI,
                            Nombre_del_Servicio = x.ServiceName,
                            Periodo_Academico = x.Periodo,
                            Sigla_Asignatura = x.Sigla,
                            Paralelo = x.ParalelNumber,
                            Codigo_Paralelo_SAP = x.ParalelSAP,
                            Cuenta_Asignada = x.AssignedAccount,
                            Monto_Contrato = x.ContractAmount,
                            Monto_IUE = x.IUE,
                            Monto_IT = x.IT,
                            Monto_a_Pagar = x.TotalAmount,
                            Observaciones = x.Comments,
                        }).OrderBy(x => x.Id);

                    ex.Worksheets.Add(d.CreateDataTable(dist2), "TotalDetalle");
                    break;
                case ServProcess.Serv_FileType.Proyectos:
                    var dist3 = _context.ServProyectoses.Include(x => x.Dependency).Include(x => x.Dependency.OrganizationalUnit).
                        Where(x => x.Serv_ProcessId == process.Id).Select(x => new
                        {
                            Id = x.Id,
                            Codigo_Socio = x.CardCode,
                            Nombre_Socio = x.CardName,
                            Cod_Dependencia = x.Dependency.Cod,
                            Cod_UO = x.Dependency.OrganizationalUnit.Cod,
                            PEI_PO = x.PEI,
                            Nombre_del_Servicio = x.ServiceName,
                            Codigo_Proyecto_SAP = x.ProjectSAPCode,
                            Nombre_del_Proyecto = x.ProjectSAPName,
                            x.Version,
                            Periodo_Academico = x.Periodo,
                            Tipo_Tarea_Asignada = x.AssignedJob,
                            Cuenta_Asignada = x.AssignedAccount,
                            Monto_Contrato = x.ContractAmount,
                            Monto_IUE = x.IUE,
                            Monto_IT = x.IT,
                            Monto_a_Pagar = x.TotalAmount,
                            Observaciones = x.Comments,
                        }).OrderBy(x => x.Id);
                    ex.Worksheets.Add(d.CreateDataTable(dist3), "TotalDetalle");
                    break;
            }
            var ms = new MemoryStream();
            ex.SaveAs(ms);
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StreamContent(ms);
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
            response.Content.Headers.ContentDisposition.FileName = process.Branches.Abr + "-Lote_" + process.Id + "-" + process.FileType + ".xlsx";
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            response.Content.Headers.ContentLength = ms.Length;
            ms.Seek(0, SeekOrigin.Begin);
            return response;
        }
        [HttpGet]
        [Route("api/ServContractprocessRows/{id}")]
        public IHttpActionResult GetSAPResumeRows(int id)
        {
            var processes = _context.ServProcesses.Include(x => x.Branches).FirstOrDefault(f =>
                f.Id == id);
            if (processes == null)
            {
                return NotFound();
            }
            var data = processes.getVoucherData(_context);
            var ppagar = data.Where(g => g.Concept == "PPAGAR").Select(g => new Serv_Voucher()
            {
                CardName = g.CardName,
                CardCode = g.CardCode,
                OU = g.OU,
                PEI = g.PEI,
                Carrera = g.Carrera,
                Paralelo = g.Paralelo,
                Periodo = g.Periodo,
                ProjectCode = g.ProjectCode,
                Memo = g.Memo,
                LineMemo = g.LineMemo,
                Concept = g.Concept,
                AssignedAccount = g.AssignedAccount,
                Account = g.Account,
                Credit = g.Credit,
                Debit = g.Debit
            }).ToList();

            List<Serv_Voucher> rest = data.Where(g => g.Concept != "PPAGAR").GroupBy(g => new
            {
                g.CardCode,
                g.OU,
                g.PEI,
                g.Carrera,
                g.Paralelo,
                g.Periodo,
                g.ProjectCode,
                g.Memo,
                g.LineMemo,
                g.Concept,
                g.AssignedAccount,
                g.Account,
            }).Select(g => new Serv_Voucher()
            {
                CardName = "",
                CardCode = g.Key.CardCode,
                OU = g.Key.OU,
                PEI = g.Key.PEI,
                Carrera = g.Key.Carrera,
                Paralelo = g.Key.Paralelo,
                Periodo = g.Key.Periodo,
                ProjectCode = g.Key.ProjectCode,
                Memo = g.Key.Memo,
                LineMemo = g.Key.LineMemo,
                Concept = g.Key.Concept,
                AssignedAccount = g.Key.AssignedAccount,
                Account = g.Key.Account,
                Credit = g.Sum(s => s.Credit),
                Debit = g.Sum(s => s.Debit)
            }).ToList();

            List<Serv_Voucher> dist1 = ppagar.Union(rest).OrderBy(z => z.Debit == 0.00M ? 1 : 0).ThenBy(z => z.Account).ToList();

            dynamic res = new JObject();

            res.rowCount = dist1.Count();
            return Ok(res);
        }

        [HttpGet]
        [Route("api/ServContractToApproval/{id}")]
        public IHttpActionResult ToApproval(int id)
        {
            var user = auth.getUser(Request);
            var processes = _context.ServProcesses.Where(f =>
                f.Id == id && f.State == ServProcess.Serv_FileState.Started);
            if (processes.Count() == 0)
            {
                return NotFound();
            }

            processes = auth.filerByRegional(processes, user).Cast<ServProcess>();
            var process = processes.FirstOrDefault();

            if (process==null)
                return Unauthorized();

            process.State = ServProcess.Serv_FileState.PendingApproval;
            _context.ServProcesses.AddOrUpdate(process);
            _context.SaveChanges();

            return Ok();
        }

        [HttpDelete]
        [Route("api/ServContract/{id}")]
        public IHttpActionResult DeleteProcess(int id)
        {
            var user = auth.getUser(Request);
            var processes = _context.ServProcesses.Where(x =>
                x.Id == id && (x.State == ServProcess.Serv_FileState.Started || x.State == ServProcess.Serv_FileState.PendingApproval));
            if (processes.Count() == 0)
                return NotFound();

            processes = auth.filerByRegional(processes, user).Cast<ServProcess>();
            var process = processes.FirstOrDefault();

            if (process == null)
                return Unauthorized();

            switch (process.State)
            {
                case ServProcess.Serv_FileState.Started:
                    process.State = ServProcess.Serv_FileState.Canceled;
                    break;
                case ServProcess.Serv_FileState.PendingApproval:
                    process.State = ServProcess.Serv_FileState.Rejected;
                    break;
            }
            process.LastUpdatedBy = user.Id;
            _context.ServProcesses.AddOrUpdate(process);
            _context.SaveChanges();
            return Ok();
        }

        [HttpPost]
        [Route("api/ServContractToSAP/{id}")]
        public IHttpActionResult ToSAP(int id, JObject webdata)
        {
            if (webdata == null || webdata["date"] == null)
            {
                return BadRequest();
            }

            var B1 = B1Connection.Instance();
            HttpResponseMessage response = new HttpResponseMessage();
            var user = auth.getUser(Request);
            var processes = _context.ServProcesses.Include(x => x.Branches).Where(f =>
                f.Id == id && f.State == ServProcess.Serv_FileState.PendingApproval);

            if (processes.Count() == 0)
            {
                return NotFound();
            }

            processes = auth.filerByRegional(processes, user).Cast<ServProcess>();
            var process = processes.FirstOrDefault();

            if (process == null)
            {
                return Unauthorized();
            }

            DateTime date = DateTime.Parse(webdata["date"].ToString());

            /*string fecha = date.ToString("MM/dd/yyyy");

            string format = "d";

            var date2 = DateTime.ParseExact(
            fecha,
            format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);*/

           
            process.InSAPAt = date;

            var data = process.getVoucherData(_context);
            var memos = data.Select(x => x.Memo).Distinct().ToList();

            foreach (var memo in memos)
            {
                //remove special chars
                var goodMemo = Regex.Replace(memo, "[^\\w\\._]", "");
                //remove new line characters
                goodMemo = Regex.Replace(goodMemo, @"\t|\n|\r", "");

                var ppagar = data.Where(g => g.Concept == "PPAGAR" && g.Memo == memo).Select(g => new Serv_Voucher()
                {
                    CardName = g.CardName,
                    CardCode = g.CardCode,
                    OU = g.OU,
                    PEI = g.PEI,
                    Carrera = g.Carrera,
                    Paralelo = g.Paralelo,
                    Periodo = g.Periodo,
                    ProjectCode = g.ProjectCode,
                    Memo = g.Memo,
                    LineMemo = g.LineMemo,
                    Concept = g.Concept,
                    //AssignedAccount=g.AssignedAccount,
                    Account = g.Account,
                    Credit = g.Credit,
                    Debit = g.Debit
                }).ToList();

                List<Serv_Voucher> rest = data.Where(g => g.Concept != "PPAGAR" && g.Memo == memo).GroupBy(g => new
                {
                    g.CardCode,
                    g.OU,
                    g.PEI,
                    g.Carrera,
                    g.Paralelo,
                    g.Periodo,
                    g.ProjectCode,
                    g.Memo,
                    g.LineMemo,
                    g.Concept,
                    //g.AssignedAccount,
                    g.Account,
                }).Select(g => new Serv_Voucher()
                {
                    CardName = "",
                    CardCode = g.Key.CardCode,
                    OU = g.Key.OU,
                    PEI = g.Key.PEI,
                    Carrera = g.Key.Carrera,
                    Paralelo = g.Key.Paralelo,
                    Periodo = g.Key.Periodo,
                    ProjectCode = g.Key.ProjectCode,
                    Memo = g.Key.Memo,
                    LineMemo = g.Key.LineMemo,
                    Concept = g.Key.Concept,
                    //AssignedAccount=g.Key.AssignedAccount,
                    Account = g.Key.Account,
                    Credit = g.Sum(s => s.Credit),
                    Debit = g.Sum(s => s.Debit)
                }).ToList();

                List<Serv_Voucher> dist1 = ppagar.Union(rest).OrderBy(z => z.Debit == 0.00M ? 1 : 0).ThenBy(z => z.Account).ToList();
                Console.WriteLine("La conexión a SAP B1 falló. No se puede continuar.", dist1.ToList(), user.Id, process );
                B1.addServVoucher(user.Id, dist1.ToList(), process);
            }

            if (memos.Count() > 1)
                process.SAPId = "Multiples.";
            process.State = ServProcess.Serv_FileState.INSAP;
            process.LastUpdatedBy = user.Id;
            _context.ServProcesses.AddOrUpdate(process);
            _context.SaveChanges();

            return Ok(process.SAPId);
        }

        [NonAction]
        private DataTable getData(List<int> list,string type)
        {
            var d = new Distribution();

            switch (type)
            {
                case ServProcess.Serv_FileType.Varios:
                    var res = (from bp in _context.Civils
                        where list.Contains(bp.Id)
                        select new Serv_VariosViewModel()
                        {
                            Codigo_Socio = bp.SAPId,
                            Nombre_Socio = bp.FullName,
                            Cod_Dependencia = "",
                            PEI_PO = "",
                            Nombre_del_Servicio = "",
                            Objeto_del_Contrato = "",
                            Cuenta_Asignada = "",
                            Monto_Contrato = 0,
                            Monto_IUE = 0,
                            Monto_IT = 0,
                            IUEExterior = 0,
                            Monto_a_Pagar = 0,
                            Observaciones = "",
                        }).ToList();
                    return d.CreateDataTable(res);
                case ServProcess.Serv_FileType.Carrera:
                    var res1 = (from bp in _context.Civils
                        where list.Contains(bp.Id)
                        select new Serv_PregradoViewModel()
                        {
                            Codigo_Socio = bp.SAPId,
                            Nombre_Socio = bp.FullName,
                            Cod_Dependencia = "",
                            PEI_PO = "",
                            Nombre_del_Servicio = "",
                            Codigo_Carrera = "",
                            Documento_Base = "",
                            Postulante = "",
                            Tipo_Tarea_Asignada= "",
                            Cuenta_Asignada = "",
                            Monto_Contrato = 0,
                            Monto_IUE = 0,
                            Monto_IT = 0,
                            IUEExterior = 0,
                            Monto_a_Pagar = 0,
                            Observaciones = "",
                        }).ToList();
                    return d.CreateDataTable(res1);
                case ServProcess.Serv_FileType.Paralelo:
                    var res2 = (from bp in _context.Civils
                        where list.Contains(bp.Id)
                        select new Serv_ReemplazoViewModel()
                        {
                            Codigo_Socio = bp.SAPId,
                            Nombre_Socio = bp.FullName,
                            Cod_Dependencia = "",
                            PEI_PO = "",
                            Nombre_del_Servicio = "",
                            Periodo_Academico = "",
                            Sigla_Asignatura = "",
                            Paralelo = "",
                            Código_Paralelo_SAP = "",
                            Cuenta_Asignada = "",
                            Monto_Contrato = 0,
                            Monto_IUE = 0,
                            Monto_IT = 0,
                            IUEExterior = 0,
                            Monto_a_Pagar = 0,
                            Observaciones = "",
                        }).ToList();
                    return d.CreateDataTable(res2);
                case ServProcess.Serv_FileType.Proyectos:
                    var res3 = (from bp in _context.Civils
                        where list.Contains(bp.Id)
                        select new Serv_ProyectosViewModel()
                        {
                            Codigo_Socio = bp.SAPId,
                            Nombre_Socio = bp.FullName,
                            Cod_Dependencia = "",
                            PEI_PO = "",
                            Nombre_del_Servicio = "",
                            Código_Proyecto_SAP = "",
                            Nombre_del_Proyecto = "",
                            Versión = "",
                            Periodo_Académico = "",
                            Tipo_Tarea_Asignada = "",
                            Cuenta_Asignada = "",
                            Monto_Contrato = 0,
                            Monto_IUE = 0,
                            Monto_IT = 0,
                            IUEExterior = 0,
                            Monto_a_Pagar = 0,
                            Observaciones = "",
                        }).ToList();
                    return d.CreateDataTable(res3);
            }
            return null;
        }

        [NonAction]
        private async Task<System.Dynamic.ExpandoObject> HttpContentToVariables(MultipartMemoryStreamProvider req)
        {
            dynamic res = new System.Dynamic.ExpandoObject();
            foreach (HttpContent contentPart in req.Contents)
            {
                var contentDisposition = contentPart.Headers.ContentDisposition;
                string varname = contentDisposition.Name;
                if (varname == "\"BranchesId\"")
                {
                    res.BranchesId = Int32.Parse(contentPart.ReadAsStringAsync().Result.ToString());
                }
                else if (varname == "\"FileType\"")
                {
                    res.FileType = contentPart.ReadAsStringAsync().Result.ToString();
                }
                else if (varname == "\"file\"")
                {
                    Stream stream = await contentPart.ReadAsStreamAsync();
                    res.fileName = String.IsNullOrEmpty(contentDisposition.FileName) ? "" : contentDisposition.FileName.Trim('"');
                    res.excelStream = stream;
                }
            }
            return res;
        }

        [NonAction]
        private bool verifyName(string fileName, int branchId, string fileType,
            out string realfileName)
        {
            string Abr = _context.Branch.Where(x => x.Id == branchId).Select(x => x.Abr).FirstOrDefault();
            realfileName = Abr + "-CC_" + fileType;
            return fileName.Split('.')[0].Equals(realfileName);
        }

        [NonAction]
        private ServProcess AddFileToProcess(int BranchesId, string FileType, int userid)
        {
            var processInDB = _context.ServProcesses.FirstOrDefault(p =>
                    p.BranchesId == BranchesId && p.FileType == FileType && p.State == ServProcess.Serv_FileState.Started);

            //if exist a process of the same type, cancel and create a new one
            if (processInDB != null )
            {
                processInDB.State = ServProcess.Serv_FileState.Canceled;
            }
            //create new process
            var process = new ServProcess();
            process.Id = process.GetNextId(_context);
            process.CreatedAt = DateTime.Now;
            process.FileType = FileType;
            process.State = ServProcess.Serv_FileState.Started;
            process.CreatedBy = userid;
            process.BranchesId = BranchesId;

            _context.ServProcesses.Add(process);
            _context.SaveChanges();
            return process;
        }

        [NonAction]
        private void DynamicExcelToDB(string FileType, dynamic o, ServProcess file,CustomUser user,  out HttpResponseMessage response)
        {
            response = new HttpResponseMessage();
            switch (FileType)
            {
                case ServProcess.Serv_FileType.Varios:
                    Serv_VariosExcel ExcelFile = new Serv_VariosExcel(o.excelStream, _context, o.fileName,file,user,headerin:1,sheets:1);
                    if (ExcelFile.ValidateFile())
                    {
                        ExcelFile.toDataBase();
                        file.State = ServProcess.Serv_FileState.Started;
                        _context.SaveChanges();
                        response.StatusCode = HttpStatusCode.OK;
                        response.Content = new StringContent("Se subio el archivo correctamente.");
                        _context.SaveChanges();
                    }
                    else
                    {
                        file.State = ServProcess.Serv_FileState.ERROR;
                        _context.SaveChanges();
                        response = ExcelFile.toResponse();
                    }
                    break;

                case ServProcess.Serv_FileType.Carrera:
                    Serv_CarreraExcel ExcelFile2 = new Serv_CarreraExcel(o.excelStream, _context, o.fileName, file,user, headerin: 1, sheets: 1);
                    if (ExcelFile2.ValidateFile())
                    {
                        ExcelFile2.toDataBase();
                        file.State = ServProcess.Serv_FileState.Started;
                        _context.SaveChanges();
                        response.StatusCode = HttpStatusCode.OK;
                        response.Content = new StringContent("Se subio el archivo correctamente.");
                        _context.SaveChanges();
                    }
                    else
                    {
                        file.State = ServProcess.Serv_FileState.ERROR;
                        _context.SaveChanges();
                        response = ExcelFile2.toResponse();
                    }
                    break;

                case ServProcess.Serv_FileType.Proyectos:
                    Serv_ProyectosExcel ExcelFile3 = new Serv_ProyectosExcel(o.excelStream, _context, o.fileName, file, user,headerin: 1, sheets: 1);
                    if (ExcelFile3.ValidateFile())
                    {
                        ExcelFile3.toDataBase();
                        file.State = ServProcess.Serv_FileState.Started;
                        _context.SaveChanges();
                        response.StatusCode = HttpStatusCode.OK;
                        response.Content = new StringContent("Se subio el archivo correctamente.");
                        _context.SaveChanges();
                    }
                    else
                    {
                        file.State = ServProcess.Serv_FileState.ERROR;
                        _context.SaveChanges();
                        response = ExcelFile3.toResponse();
                    }
                    break;
                case ServProcess.Serv_FileType.Paralelo:
                    Serv_ParaleloExcel ExcelFile4 = new Serv_ParaleloExcel(o.excelStream, _context, o.fileName, file,user, headerin: 1, sheets: 1);
                    if (ExcelFile4.ValidateFile())
                    {
                        ExcelFile4.toDataBase();
                        file.State = ServProcess.Serv_FileState.Started;
                        _context.SaveChanges();
                        response.StatusCode = HttpStatusCode.OK;
                        response.Content = new StringContent("Se subio el archivo correctamente.");
                        _context.SaveChanges();
                    }
                    else
                    {
                        file.State = ServProcess.Serv_FileState.ERROR;
                        _context.SaveChanges();
                        response = ExcelFile4.toResponse();
                    }
                    break;
            }
        }

        

    }
}
