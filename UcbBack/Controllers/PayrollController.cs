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

namespace UcbBack.Controllers
{
    public class PayrollController : ApiController
    {
        private ApplicationDbContext _context;

        private struct ProcessState
        {
            public static string STARTED = "STARTED";
            public static string ERROR = "ERROR";
            public static string CANCELED = "CANCELED";
            public static string VALIDATED = "VALIDATED";
            public static string PROCESSED = "PROCESSED";
            public static string WARNING = "WARNING";
            public static string INSAP = "INSAP";
        }
        private struct FileState
        {
            public static string SENDED = "SENDED";
            public static string UPLOADED = "UPLOADED";
            public static string ERROR = "ERROR";
            public static string CANCELED = "CANCELED";
        }
        private enum ExcelFileType
        {
            Payroll = 1,
            Academic,
            Discount,
            Postgrado,
            Pregrado,
            OR
        }

        private int ExcelHeaders = 3;

        private ValidateAuth auth;
        public PayrollController()
        {
            auth = new ValidateAuth();
            _context = new ApplicationDbContext();
        }

        [HttpPost]
        [Route("api/payroll/CheckUpload")]
        public IHttpActionResult CheckUpload([FromBody] JObject upload)
        {
            int branchid = 0;
            if (upload["mes"] == null || upload["gestion"] == null || upload["segmentoOrigen"] == null || !Int32.TryParse(upload["segmentoOrigen"].ToString(), out branchid))
                return BadRequest("Debes enviar mes,gestion y segmentoOrigen");
            var mes = upload["mes"].ToString();
            var gestion = upload["gestion"].ToString();

            var process = _context.DistProcesses.FirstOrDefault(f => f.mes == mes
                                                             && f.gestion == gestion
                                                             && f.BranchesId == branchid
                                                             && f.State != ProcessState.CANCELED);
            if (process == null)
                return Ok();
            var files = _context.FileDbs.Where(f => f.DistProcessId == process.Id && f.State == FileState.UPLOADED).Include(f => f.DistFileTypeId).Select(f => new { f.DistFileType.FileType });

            List<string> tipos = new List<string>();
            foreach (var tipo in files)
            {
                tipos.Add(tipo.FileType);
            }

            dynamic res = new JObject();
            res.array = JToken.FromObject(tipos);
            res.id = process.Id;
            res.state = process.State;
            return Ok(res);
        }

        [NonAction]
        private Dist_File AddFileToProcess(string mes, string gestion, int BranchesId, ExcelFileType FileType, int userid, string fileName)
        {
            var processInDB = _context.DistProcesses.FirstOrDefault(p =>
                    p.BranchesId == BranchesId && p.gestion == gestion && p.mes == mes && p.State != ProcessState.CANCELED);

            if (processInDB != null && (processInDB.State == ProcessState.STARTED || processInDB.State == ProcessState.ERROR || processInDB.State == ProcessState.WARNING))
            {
                var fileInDB = _context.FileDbs.FirstOrDefault(f => f.DistProcessId == processInDB.Id && f.DistFileTypeId == (int)FileType && f.State == FileState.UPLOADED);
                if (fileInDB == null)
                {
                    processInDB.State = ProcessState.STARTED;
                    _context.Database.ExecuteSqlCommand("UPDATE \"" + CustomSchema.Schema + "\".\"Dist_LogErrores\" set \"Inspected\" = true where \"DistProcessId\" = " + processInDB.Id);
                    var file = new Dist_File();
                    file.Id = Dist_File.GetNextId(_context);
                    file.UploadedDate = DateTime.Now;
                    file.DistFileTypeId = (int)FileType;
                    file.Name = fileName;
                    file.State = FileState.SENDED;
                    file.CustomUserId = userid;
                    file.DistProcessId = processInDB.Id;
                    _context.FileDbs.Add(file);
                    _context.SaveChanges();
                    return file;
                }
            }

            else if (processInDB != null &&
                (processInDB.State == ProcessState.INSAP || processInDB.State == ProcessState.PROCESSED || processInDB.State == ProcessState.VALIDATED))
            {
                return null;
            }
            else
            {
                var process = new Dist_Process();
                process.UploadedDate = DateTime.Now;
                process.Id = Dist_Process.GetNextId(_context);
                process.BranchesId = BranchesId;
                process.mes = mes;
                process.gestion = gestion;
                process.State = ProcessState.STARTED;
                _context.DistProcesses.Add(process);
                _context.SaveChanges();

                var file = new Dist_File();
                file.Id = Dist_File.GetNextId(_context);
                file.UploadedDate = DateTime.Now;
                file.DistFileTypeId = (int)FileType;
                file.Name = fileName;
                file.State = FileState.SENDED;
                file.CustomUserId = userid;
                file.DistProcessId = process.Id;
                _context.FileDbs.Add(file);
                _context.SaveChanges();
                return file;
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
                if (varname == "\"mes\"")
                {
                    if (contentPart.ReadAsStringAsync().Result.Length == 2)
                        res.mes = contentPart.ReadAsStringAsync().Result.ToString();
                }
                else if (varname == "\"gestion\"")
                {
                    if (contentPart.ReadAsStringAsync().Result.Length == 4)
                        res.gestion = contentPart.ReadAsStringAsync().Result.ToString();
                }
                else if (varname == "\"segmentoOrigen\"")
                {
                    res.segmentoOrigen = Int32.Parse(contentPart.ReadAsStringAsync().Result.ToString());
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
        private bool verifyName(string fileName, string mes, string gestion, int branchId, string fileType,
            out string realfileName)
        {
            string Abr = _context.Branch.Where(x => x.Id == branchId).Select(x => x.Abr).FirstOrDefault();
            realfileName = Abr + gestion + mes + fileType;
            return fileName.Split('.')[0].Equals(realfileName);
        }

        [HttpGet]
        [Route("api/payroll/PayrollExcel")]
        public HttpResponseMessage GetPayrollExcel()
        {
            PayrollExcel contractExcel = new PayrollExcel(fileName: "Planilla.xlsx", headerin: ExcelHeaders);
            return contractExcel.getTemplate();
        }
        [HttpDelete]
        [Route("api/payroll/PayrollExcel")]
        public IHttpActionResult CancelPayrollExcel(JObject data)
        {
            int branchesid;
            if (data["mes"] == null || data["gestion"] == null || data["segmentoOrigen"] == null || !Int32.TryParse(data["segmentoOrigen"].ToString(), out branchesid))
            {
                ModelState.AddModelError("Mal Formato", "Debes enviar mes, gestion y segmentoOrigen");
                return BadRequest();

            }
            string mes = data["mes"].ToString();
            string gestion = data["gestion"].ToString();

            var file = _context.FileDbs.Include(f => f.DistProcess)
                .FirstOrDefault(f => f.DistProcess.mes == mes
                                     && f.DistProcess.gestion == gestion
                                     && f.DistProcess.BranchesId == branchesid
                                     && f.DistFileTypeId == (int)ExcelFileType.Payroll
                                     && f.State == FileState.UPLOADED);
            if (file == null)
            {
                return NotFound();
            }

            file.State = FileState.CANCELED;
            _context.SaveChanges();

            return Ok();
        }

        [HttpPost]
        [Route("api/payroll/PayrollExcel")]
        public async Task<HttpResponseMessage> UploadPayrollExcel()
        {
            var response = new HttpResponseMessage();
            PayrollExcel ExcelFile = null;
            try
            {
                var req = await Request.Content.ReadAsMultipartAsync();
                dynamic o = HttpContentToVariables(req).Result;

                if (!((IDictionary<string, object>)o).ContainsKey("mes")
                    || !((IDictionary<string, object>)o).ContainsKey("gestion")
                    || !((IDictionary<string, object>)o).ContainsKey("segmentoOrigen")
                    || !((IDictionary<string, object>)o).ContainsKey("fileName")
                    || !((IDictionary<string, object>)o).ContainsKey("excelStream")
                    || !o.fileName.ToString().EndsWith(".xlsx"))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors",
                        "{ \"Faltan datos\": \"Debe enviar mes(mm), gestion(yyyy), segmentoOrigen(id) y un archivo excel llamado file (en formato .xlsx)\"}");
                    response.Content =
                        new StringContent(
                            "Debe enviar mes(mm), gestion(yyyy), segmentoOrigen(id) y un archivo excel llamado file");
                    return response;
                }

                string realFileName = "";
                if (!verifyName(o.fileName, o.mes, o.gestion, o.segmentoOrigen, "PLAN", out realFileName))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors",
                        "{ \"Nombre Incorrecto\": \"El archivo enviado no cumple con la regla de nombres. Nombre sugerido: " +
                        realFileName + "\"}");
                    response.Content = new StringContent("El archivo enviado no cumple con la regla de nombres.");
                    return response;
                }

                int userid = 0;
                if (!Int32.TryParse(Request.Headers.GetValues("id").First(), out userid))
                {
                    response.StatusCode = HttpStatusCode.Unauthorized;
                    return response;
                }

                var file = AddFileToProcess(o.mes.ToString(), o.gestion.ToString(), o.segmentoOrigen,
                    ExcelFileType.Payroll, userid, o.fileName.ToString());

                if (file == null)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors",
                        "{ \"Ya se Subio archivos para este mes\": \"Ya se subio  datos para este mes, si quiere volver a subir cancele el anterior archivo.\"}");
                    response.Content =
                        new StringContent(
                            "Ya se subió  datos para este mes, si quiere volver a subir cancele el anterior archivo.");
                    return response;
                }

                ExcelFile = new PayrollExcel(o.excelStream, _context, o.fileName, o.mes, o.gestion,
                    o.segmentoOrigen.ToString(), file, headerin: ExcelHeaders, sheets: 1);

                if (ExcelFile.ValidateFile())
                {
                    ExcelFile.toDataBase();
                    file.State = FileState.UPLOADED;
                    _context.SaveChanges();
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new StringContent("Se subio el archivo correctamente.");
                    return response;
                }

                file.State = FileState.ERROR;
                _context.SaveChanges();
                return ExcelFile.toResponse();
            }
            catch (System.ArgumentException e)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors",
                    "{ \"Formato Archivo Invalido\": \"Por favor enviar un archivo en formato excel (.xlsx)\"}");
                ExcelFile.addError("Formato Archivo Invalido", "Por favor enviar un archivo en formato excel (.xlsx)");
                response.Content = new StringContent("Por favor enviar un archivo en formato excel (.xlsx)" + e);
                return response;
            }
            catch (System.IO.IOException e)
            {
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors",
                    "{ \"Archivo demasiado grande\": \"El archivo es demasiado grande para ser procesado.\"}");
                response.Content = new StringContent("El archivo es demasiado grande para ser procesado.");
                return response;
            }
            catch (HanaException e)
            {
                if (e.NativeError == 258)
                {
                    Console.WriteLine(e);
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("ErrorSAP", "{ \"La conexion con SAP se perdio\": \"No se pudo validar el archivo con con SAP.\"}");
                    response.Content = new StringContent("Error conexion SAP");
                    return response;
                }
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("ErrorSAP", "{ \"La conexion con SAP se perdio\": \"No se pudo validar el archivo con con SAP.\"}");
                response.Content = new StringContent("Error conexion SAP");
                return response;
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors", "{ \"Existen Enlaces a otros archivos\": \"Existen celdas con referencias a otros archivos.\"}");
                response.Content = new StringContent("Por favor enviar un archivo en formato excel sin referencias a otros libros excel o formulas(.xls, .xslx)");
                return response;
            }
        }


        [HttpGet]
        [Route("api/payroll/AcademicExcel")]
        public HttpResponseMessage GetAcademicExcel()
        {
            AcademicExcel contractExcel = new AcademicExcel(fileName: "Academico.xlsx", headerin: ExcelHeaders);
            return contractExcel.getTemplate();
        }

        [HttpDelete]
        [Route("api/payroll/AcademicExcel")]
        public IHttpActionResult CancelAcademicExcel(JObject data)
        {
            int branchesid;
            if (data["mes"] == null || data["gestion"] == null || data["segmentoOrigen"] == null || !Int32.TryParse(data["segmentoOrigen"].ToString(), out branchesid))
            {
                ModelState.AddModelError("Mal Formato", "Debes enviar mes, gestion y segmentoOrigen");
                return BadRequest();

            }

            string mes = data["mes"].ToString();
            string gestion = data["gestion"].ToString();

            var file = _context.FileDbs.Include(f => f.DistProcess)
                .FirstOrDefault(f => f.DistProcess.mes == mes
                                     && f.DistProcess.gestion == gestion
                                     && f.DistProcess.BranchesId == branchesid
                                     && f.DistFileTypeId == (int)ExcelFileType.Academic
                                     && f.State == FileState.UPLOADED);
            if (file == null)
            {
                return NotFound();
            }

            file.State = FileState.CANCELED;
            _context.SaveChanges();

            return Ok();
        }

        [HttpPost]
        [Route("api/payroll/AcademicExcel")]
        public async Task<HttpResponseMessage> UploadAcademicExcel()
        {
            var response = new HttpResponseMessage();
            AcademicExcel ExcelFile = null;
            try
            {
                var req = await Request.Content.ReadAsMultipartAsync();
                dynamic o = HttpContentToVariables(req).Result;

                if (!((IDictionary<string, object>)o).ContainsKey("mes")
                    || !((IDictionary<string, object>)o).ContainsKey("gestion")
                    || !((IDictionary<string, object>)o).ContainsKey("segmentoOrigen")
                    || !((IDictionary<string, object>)o).ContainsKey("fileName")
                    || !((IDictionary<string, object>)o).ContainsKey("excelStream")
                    || !o.fileName.ToString().EndsWith(".xlsx"))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors",
                        "{ \"Faltan datos\": \"Debe enviar mes(mm), gestion(yyyy), segmentoOrigen(id) y un archivo excel llamado file (en formato .xlsx)\"}");
                    response.Content =
                        new StringContent(
                            "Debe enviar mes(mm), gestion(yyyy), segmentoOrigen(id) y un archivo excel llamado file");
                    return response;
                }

                string realFileName;
                if (!verifyName(o.fileName, o.mes, o.gestion, o.segmentoOrigen, "ACAD", out realFileName))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors",
                        "{ \"Nombre Incorrecto\": \"El archivo enviado no cumple con la regla de nombres. Nombre sugerido: " +
                        realFileName + "\"}");
                    response.Content = new StringContent("El archivo enviado no cumple con la regla de nombres.");
                    return response;
                }

                int userid = Int32.Parse(Request.Headers.GetValues("id").First());
                var file = AddFileToProcess(o.mes.ToString(), o.gestion.ToString(), o.segmentoOrigen,
                    ExcelFileType.Academic, userid, o.fileName.ToString());

                if (file == null)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors",
                        "{ \"Ya se Subio archivos para este mes\": \"Ya se subio  datos para este mes, si quiere volver a subir cancele el anterior archivo.\"}");
                    response.Content =
                        new StringContent(
                            "Ya se subió  datos para este mes, si quiere volver a subir cancele el anterior archivo.");
                    return response;
                }

                ExcelFile = new AcademicExcel(o.excelStream, _context, o.fileName, o.mes, o.gestion,
                    o.segmentoOrigen.ToString(), file, headerin: ExcelHeaders, sheets: 1);

                if (ExcelFile.ValidateFile())
                {
                    ExcelFile.toDataBase();
                    response.StatusCode = HttpStatusCode.OK;
                    file.State = FileState.UPLOADED;
                    _context.SaveChanges();
                    response.Content = new StringContent("Se subio el archivo correctamente.");
                    return response;
                }

                file.State = FileState.ERROR;
                _context.SaveChanges();
                return ExcelFile.toResponse();
            }
            catch (System.ArgumentException e)
            {
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors",
                    "{ \"Formato Archivo Invalido\": \"Por favor enviar un archivo en formato excel (.xlsx)\"}");
                ExcelFile.addError("Formato Archivo Invalido", "Por favor enviar un archivo en formato excel (.xlsx)");
                response.Content = new StringContent("Por favor enviar un archivo en formato excel (.xlsx)" + e);
                return response;
            }
            catch (System.IO.IOException e)
            {
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors",
                    "{ \"Archivo demasiado grande\": \"El archivo es demasiado grande para ser procesado.\"}");
                response.Content = new StringContent("El archivo es demasiado grande para ser procesado.");
                return response;
            }
            catch (HanaException e)
            {
                if (e.NativeError == 258)
                {
                    Console.WriteLine(e);
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("ErrorSAP",
                        "{ \"La conexion con SAP se perdio\": \"No se pudo validar el archivo con con SAP.\"}");
                    response.Content = new StringContent("Error conexion SAP");
                    return response;
                }

                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("ErrorSAP",
                    "{ \"La conexion con SAP se perdio\": \"No se pudo validar el archivo con con SAP.\"}");
                response.Content = new StringContent("Error conexion SAP");
                return response;
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors",
                    "{ \"Existen Enlaces a otros archivos\": \"Existen celdas con referencias a otros archivos.\"}");
                response.Content =
                    new StringContent(
                        "Por favor enviar un archivo en formato excel sin referencias a otros libros excel o formulas(.xls, .xslx)");
                return response;
            }
        }

        [HttpGet]
        [Route("api/payroll/DiscountExcel")]
        public HttpResponseMessage GetDiscountExcel()
        {
            DiscountExcel contractExcel = new DiscountExcel(fileName: "Descuentos.xlsx", headerin: ExcelHeaders);
            return contractExcel.getTemplate();
        }

        [HttpDelete]
        [Route("api/payroll/DiscountExcel")]
        public IHttpActionResult CancelDiscountExcel(JObject data)
        {
            int branchesid;
            if (data["mes"] == null || data["gestion"] == null || data["segmentoOrigen"] == null || !Int32.TryParse(data["segmentoOrigen"].ToString(), out branchesid))
            {
                ModelState.AddModelError("Mal Formato", "Debes enviar mes, gestion y segmentoOrigen");
                return BadRequest();

            }
            string mes = data["mes"].ToString();
            string gestion = data["gestion"].ToString();
            var file = _context.FileDbs.Include(f => f.DistProcess)
                .FirstOrDefault(f => f.DistProcess.mes == mes
                                     && f.DistProcess.gestion == gestion
                                     && f.DistProcess.BranchesId == branchesid
                                     && f.DistFileTypeId == (int)ExcelFileType.Discount
                                     && f.State == FileState.UPLOADED);
            if (file == null)
            {
                return NotFound();
            }

            file.State = FileState.CANCELED;
            _context.SaveChanges();

            return Ok();
        }

        [HttpPost]
        [Route("api/payroll/DiscountExcel")]
        public async Task<HttpResponseMessage> UploadDiscountExcel()
        {
            var response = new HttpResponseMessage();
            DiscountExcel ExcelFile = null;
            try
            {
                var req = await Request.Content.ReadAsMultipartAsync();
                dynamic o = HttpContentToVariables(req).Result;

                if (!((IDictionary<string, object>)o).ContainsKey("mes")
                    || !((IDictionary<string, object>)o).ContainsKey("gestion")
                    || !((IDictionary<string, object>)o).ContainsKey("segmentoOrigen")
                    || !((IDictionary<string, object>)o).ContainsKey("fileName")
                    || !((IDictionary<string, object>)o).ContainsKey("excelStream")
                    || !o.fileName.ToString().EndsWith(".xlsx"))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors", "{ \"Faltan datos\": \"Debe enviar mes(mm), gestion(yyyy), segmentoOrigen(id) y un archivo excel llamado file (en formato .xlsx)\"}");
                    response.Content = new StringContent("Debe enviar mes(mm), gestion(yyyy), segmentoOrigen(id) y un archivo excel llamado file");
                    return response;
                }

                string realFileName;
                if (!verifyName(o.fileName, o.mes, o.gestion, o.segmentoOrigen, "DEDU", out realFileName))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors", "{ \"Nombre Incorrecto\": \"El archivo enviado no cumple con la regla de nombres. Nombre sugerido: " + realFileName + "\"}");
                    response.Content = new StringContent("El archivo enviado no cumple con la regla de nombres.");
                    return response;
                }

                int userid = Int32.Parse(Request.Headers.GetValues("id").First());
                var file = AddFileToProcess(o.mes.ToString(), o.gestion.ToString(), o.segmentoOrigen, ExcelFileType.Discount, userid, o.fileName.ToString());

                if (file == null)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors", "{ \"Ya se Subio archivos para este mes\": \"Ya se subio  datos para este mes, si quiere volver a subir cancele el anterior archivo.\"}");
                    response.Content = new StringContent("Ya se subió  datos para este mes, si quiere volver a subir cancele el anterior archivo.");
                    return response;
                }

                ExcelFile = new DiscountExcel(o.excelStream, _context, o.fileName, o.mes, o.gestion,
                    o.segmentoOrigen.ToString(), file, headerin: ExcelHeaders, sheets: 1);
                if (ExcelFile.ValidateFile())
                {
                    ExcelFile.toDataBase();
                    file.State = FileState.UPLOADED;
                    _context.SaveChanges();
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new StringContent("Se subio el archivo correctamente.");
                    return response;
                }

                file.State = FileState.CANCELED;
                _context.SaveChanges();
                return ExcelFile.toResponse();
            }
            catch (System.ArgumentException e)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors", "{ \"Formato Archivo Invalido\": \"Por favor enviar un archivo en formato excel (.xlsx)\"}");
                ExcelFile.addError("Formato Archivo Invalido", "Por favor enviar un archivo en formato excel (.xlsx)");
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
                    response.Headers.Add("ErrorSAP", "{ \"La conexion con SAP se perdio\": \"No se pudo validar el archivo con con SAP.\"}");
                    response.Content = new StringContent("Error conexion SAP");
                    return response;
                }
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("ErrorSAP", "{ \"La conexion con SAP se perdio\": \"No se pudo validar el archivo con con SAP.\"}");
                response.Content = new StringContent("Error conexion SAP");
                return response;
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors", "{ \"Existen Enlaces a otros archivos\": \"Existen celdas con referencias a otros archivos.\"}");
                ExcelFile.addError("Existen Enlaces a otros archivos", "Existen celdas con referencias a otros archivos.");
                response.Content = new StringContent("Por favor enviar un archivo en formato excel sin referencias a otros libros excel o formulas(.xls, .xslx)");
                return response;
            }
        }

        [HttpGet]
        [Route("api/payroll/PostgradoExcel")]
        public HttpResponseMessage GetPostgradoExcel()
        {
            PostgradoExcel contractExcel = new PostgradoExcel(fileName: "PosGrado.xlsx", headerin: ExcelHeaders);
            return contractExcel.getTemplate();
        }

        [HttpDelete]
        [Route("api/payroll/PostgradoExcel")]
        public IHttpActionResult CancelPostgradoExcel(JObject data)
        {
            int branchesid;
            if (data["mes"] == null || data["gestion"] == null || data["segmentoOrigen"] == null || !Int32.TryParse(data["segmentoOrigen"].ToString(), out branchesid))
            {
                ModelState.AddModelError("Mal Formato", "Debes enviar mes, gestion y segmentoOrigen");
                return BadRequest();

            }
            string mes = data["mes"].ToString();
            string gestion = data["gestion"].ToString();

            var file = _context.FileDbs.Include(f => f.DistProcess)
                .FirstOrDefault(f => f.DistProcess.mes == mes
                                     && f.DistProcess.gestion == gestion
                                     && f.DistProcess.BranchesId == branchesid
                                     && f.DistFileTypeId == (int)ExcelFileType.Postgrado
                                     && f.State == FileState.UPLOADED);
            if (file == null)
            {
                return NotFound();
            }

            file.State = FileState.CANCELED;
            _context.SaveChanges();

            return Ok();
        }

        [HttpPost]
        [Route("api/payroll/PostgradoExcel")]
        public async Task<HttpResponseMessage> UploadPostgradoExcel()
        {
            var response = new HttpResponseMessage();
            PostgradoExcel ExcelFile = null;
            try
            {
                var req = await Request.Content.ReadAsMultipartAsync();
                dynamic o = HttpContentToVariables(req).Result;

                if (!((IDictionary<string, object>)o).ContainsKey("mes")
                    || !((IDictionary<string, object>)o).ContainsKey("gestion")
                    || !((IDictionary<string, object>)o).ContainsKey("segmentoOrigen")
                    || !((IDictionary<string, object>)o).ContainsKey("fileName")
                    || !((IDictionary<string, object>)o).ContainsKey("excelStream")
                    || !o.fileName.ToString().EndsWith(".xlsx"))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors", "{ \"Faltan datos\": \"Debe enviar mes(mm), gestion(yyyy), segmentoOrigen(id) y un archivo excel llamado file (en formato .xlsx)\"}");
                    response.Content = new StringContent("Debe enviar mes(mm), gestion(yyyy), segmentoOrigen(id) y un archivo excel llamado file");
                    return response;
                }

                string realFileName;
                if (!verifyName(o.fileName, o.mes, o.gestion, o.segmentoOrigen, "POST", out realFileName))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors", "{ \"Nombre Incorrecto\": \"El archivo enviado no cumple con la regla de nombres. Nombre sugerido: " + realFileName + "\"}");
                    response.Content = new StringContent("El archivo enviado no cumple con la regla de nombres.");
                    return response;
                }

                int userid = Int32.Parse(Request.Headers.GetValues("id").First());
                var file = AddFileToProcess(o.mes.ToString(), o.gestion.ToString(), o.segmentoOrigen, ExcelFileType.Postgrado, userid, o.fileName.ToString());

                if (file == null)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors", "{ \"Ya se Subio archivos para este mes\": \"Ya se subio  datos para este mes, si quiere volver a subir cancele el anterior archivo.\"}");
                    response.Content = new StringContent("Ya se subió  datos para este mes, si quiere volver a subir cancele el anterior archivo.");
                    return response;
                }

                ExcelFile = new PostgradoExcel(o.excelStream, _context, o.fileName, o.mes, o.gestion, o.segmentoOrigen.ToString(), file, headerin: ExcelHeaders, sheets: 1);
                if (ExcelFile.ValidateFile())
                {
                    ExcelFile.toDataBase();
                    file.State = FileState.UPLOADED;
                    _context.SaveChanges();
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new StringContent("Se subio el archivo correctamente.");
                    return response;
                }
                file.State = FileState.ERROR;
                _context.SaveChanges();
                return ExcelFile.toResponse();
            }
            catch (System.ArgumentException e)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors", "{ \"Formato Archivo Invalido\": \"Por favor enviar un archivo en formato excel (.xlsx)\"}");
                ExcelFile.addError("Formato Archivo Invalido", "Por favor enviar un archivo en formato excel (.xlsx)");
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
                    response.Headers.Add("ErrorSAP", "{ \"La conexion con SAP se perdio\": \"No se pudo validar el archivo con con SAP.\"}");
                    response.Content = new StringContent("Error conexion SAP");
                    return response;
                }
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("ErrorSAP", "{ \"La conexion con SAP se perdio\": \"No se pudo validar el archivo con con SAP.\"}");
                response.Content = new StringContent("Error conexion SAP");
                return response;
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors", "{ \"Existen Enlaces a otros archivos\": \"Existen celdas con referencias a otros archivos.\"}");
                response.Content = new StringContent("Por favor enviar un archivo en formato excel sin referencias a otros libros excel o formulas(.xls, .xslx)");
                return response;
            }
        }

        [HttpGet]
        [Route("api/payroll/PregradoExcel")]
        public HttpResponseMessage GetPregradoExcel()
        {
            PregradoExcel contractExcel = new PregradoExcel(fileName: "Pregrado.xlsx", headerin: ExcelHeaders);
            return contractExcel.getTemplate();
        }

        [HttpDelete]
        [Route("api/payroll/PregradoExcel")]
        public IHttpActionResult CancelGetPregradoExcel(JObject data)
        {
            int branchesid;
            if (data["mes"] == null || data["gestion"] == null || data["segmentoOrigen"] == null || !Int32.TryParse(data["segmentoOrigen"].ToString(), out branchesid))
            {
                ModelState.AddModelError("Mal Formato", "Debes enviar mes, gestion y segmentoOrigen");
                return BadRequest();

            }
            string mes = data["mes"].ToString();
            string gestion = data["gestion"].ToString();
            var file = _context.FileDbs.Include(f => f.DistProcess)
                .FirstOrDefault(f => f.DistProcess.mes == mes
                                     && f.DistProcess.gestion == gestion
                                     && f.DistProcess.BranchesId == branchesid
                                     && f.DistFileTypeId == (int)ExcelFileType.Pregrado
                                     && f.State == FileState.UPLOADED);
            if (file == null)
            {
                return NotFound();
            }

            file.State = FileState.CANCELED;
            _context.SaveChanges();

            return Ok();
        }

        [HttpPost]
        [Route("api/payroll/PregradoExcel")]
        public async Task<HttpResponseMessage> UploadPregradoExcel()
        {
            var response = new HttpResponseMessage();
            PregradoExcel ExcelFile = null;
            try
            {
                var req = await Request.Content.ReadAsMultipartAsync();
                dynamic o = HttpContentToVariables(req).Result;

                if (!((IDictionary<string, object>)o).ContainsKey("mes")
                    || !((IDictionary<string, object>)o).ContainsKey("gestion")
                    || !((IDictionary<string, object>)o).ContainsKey("segmentoOrigen")
                    || !((IDictionary<string, object>)o).ContainsKey("fileName")
                    || !((IDictionary<string, object>)o).ContainsKey("excelStream")
                    || !o.fileName.ToString().EndsWith(".xlsx"))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors", "{ \"Faltan datos\": \"Debe enviar mes(mm), gestion(yyyy), segmentoOrigen(id) y un archivo excel llamado file (en formato .xlsx)\"}");
                    response.Content = new StringContent("Debe enviar mes(mm), gestion(yyyy), segmentoOrigen(id) y un archivo excel llamado file");
                    return response;
                }

                string realFileName;
                if (!verifyName(o.fileName, o.mes, o.gestion, o.segmentoOrigen, "PREG", out realFileName))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors", "{ \"Nombre Incorrecto\": \"El archivo enviado no cumple con la regla de nombres. Nombre sugerido: " + realFileName + "\"}");
                    response.Content = new StringContent("El archivo enviado no cumple con la regla de nombres.");
                    return response;
                }

                int userid = Int32.Parse(Request.Headers.GetValues("id").First());
                var file = AddFileToProcess(o.mes.ToString(), o.gestion.ToString(), o.segmentoOrigen, ExcelFileType.Pregrado, userid, o.fileName.ToString());

                if (file == null)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors", "{ \"Ya se Subio archivos para este mes\": \"Ya se subio  datos para este mes, si quiere volver a subir cancele el anterior archivo.\"}");
                    response.Content = new StringContent("Ya se subió  datos para este mes, si quiere volver a subir cancele el anterior archivo.");
                    return response;
                }

                ExcelFile = new PregradoExcel(o.excelStream, _context, o.fileName, o.mes, o.gestion, o.segmentoOrigen.ToString(), file, headerin: ExcelHeaders, sheets: 1);
                if (ExcelFile.ValidateFile())
                {
                    ExcelFile.toDataBase();
                    file.State = FileState.UPLOADED;
                    _context.SaveChanges();
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new StringContent("Se subio el archivo correctamente.");
                    return response;
                }
                file.State = FileState.ERROR;
                _context.SaveChanges();
                return ExcelFile.toResponse();
            }
            catch (System.ArgumentException e)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors", "{ \"Formato Archivo Invalido\": \"Por favor enviar un archivo en formato excel (.xlsx)\"}");
                ExcelFile.addError("Formato Archivo Invalido", "Por favor enviar un archivo en formato excel (.xlsx)");
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
                    response.Headers.Add("ErrorSAP", "{ \"La conexion con SAP se perdio\": \"No se pudo validar el archivo con con SAP.\"}");
                    response.Content = new StringContent("Error conexion SAP");
                    return response;
                }
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("ErrorSAP", "{ \"La conexion con SAP se perdio\": \"No se pudo validar el archivo con con SAP.\"}");
                response.Content = new StringContent("Error conexion SAP");
                return response;
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors", "{ \"Existen Enlaces a otros archivos\": \"Existen celdas con referencias a otros archivos.\"}");
                response.Content = new StringContent("Por favor enviar un archivo en formato excel sin referencias a otros libros excel o formulas(.xls, .xslx)");
                return response;
            }
        }

        [HttpGet]
        [Route("api/payroll/ORExcel")]
        public HttpResponseMessage GetORExcel()
        {
            ORExcel contractExcel = new ORExcel(fileName: "OtrasRegionales.xlsx", headerin: ExcelHeaders);
            return contractExcel.getTemplate();
        }

        [HttpDelete]
        [Route("api/payroll/ORExcel")]
        public IHttpActionResult CancelORExcel(JObject data)
        {
            int branchesid;
            if (data["mes"] == null || data["gestion"] == null || data["segmentoOrigen"] == null || !Int32.TryParse(data["segmentoOrigen"].ToString(), out branchesid))
            {
                ModelState.AddModelError("Mal Formato", "Debes enviar mes, gestion y segmentoOrigen");
                return BadRequest();

            }
            string mes = data["mes"].ToString();
            string gestion = data["gestion"].ToString();
            var file = _context.FileDbs.Include(f => f.DistProcess)
                .FirstOrDefault(f => f.DistProcess.mes == mes
                                     && f.DistProcess.gestion == gestion
                                     && f.DistProcess.BranchesId == branchesid
                                     && f.DistFileTypeId == (int)ExcelFileType.OR
                                     && f.State == FileState.UPLOADED);
            if (file == null)
            {
                return NotFound();
            }

            file.State = FileState.CANCELED;
            _context.SaveChanges();

            return Ok();
        }

        [HttpPost]
        [Route("api/payroll/ORExcel")]
        public async Task<HttpResponseMessage> UploadORExcel()
        {
            var response = new HttpResponseMessage();
            ORExcel ExcelFile = null;
            try
            {
                var req = await Request.Content.ReadAsMultipartAsync();
                dynamic o = HttpContentToVariables(req).Result;

                if (!((IDictionary<string, object>)o).ContainsKey("mes")
                    || !((IDictionary<string, object>)o).ContainsKey("gestion")
                    || !((IDictionary<string, object>)o).ContainsKey("segmentoOrigen")
                    || !((IDictionary<string, object>)o).ContainsKey("fileName")
                    || !((IDictionary<string, object>)o).ContainsKey("excelStream")
                    || !o.fileName.ToString().EndsWith(".xlsx"))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors", "{ \"Faltan datos\": \"Debe enviar mes(mm), gestion(yyyy), segmentoOrigen(id) y un archivo excel llamado file (en formato .xlsx)\"}");
                    response.Content = new StringContent("Debe enviar mes(mm), gestion(yyyy), segmentoOrigen(id) y un archivo excel llamado file");
                    return response;
                }

                string realFileName;
                if (!verifyName(o.fileName, o.mes, o.gestion, o.segmentoOrigen, "REGI", out realFileName))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors", "{ \"Nombre Incorrecto\": \"El archivo enviado no cumple con la regla de nombres. Nombre sugerido: " + realFileName + "\"}");
                    response.Content = new StringContent("El archivo enviado no cumple con la regla de nombres.");
                    return response;
                }

                int userid = Int32.Parse(Request.Headers.GetValues("id").First());
                var file = AddFileToProcess(o.mes.ToString(), o.gestion.ToString(), o.segmentoOrigen, ExcelFileType.OR, userid, o.fileName.ToString());

                if (file == null)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors", "{ \"Ya se Subio archivos para este mes\": \"Ya se subio  datos para este mes, si quiere volver a subir cancele el anterior archivo.\"}");
                    response.Content = new StringContent("Ya se subió  datos para este mes, si quiere volver a subir cancele el anterior archivo.");
                    return response;
                }

                ExcelFile = new ORExcel(o.excelStream, _context, o.fileName, o.mes, o.gestion, o.segmentoOrigen.ToString(), file, headerin: ExcelHeaders, sheets: 1);
                if (ExcelFile.ValidateFile())
                {
                    ExcelFile.toDataBase();
                    file.State = FileState.UPLOADED;
                    _context.SaveChanges();
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new StringContent("Se subio el archivo correctamente.");
                    return response;
                }
                file.State = FileState.ERROR;
                _context.SaveChanges();
                return ExcelFile.toResponse();
            }
            catch (System.ArgumentException e)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors", "{ \"Formato Archivo Invalido\": \"Por favor enviar un archivo en formato excel (.xlsx)\"}");
                ExcelFile.addError("Formato Archivo Invalido", "Por favor enviar un archivo en formato excel (.xlsx)");
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
                    response.Headers.Add("ErrorSAP", "{ \"La conexion con SAP se perdio\": \"No se pudo validar el archivo con con SAP.\"}");
                    response.Content = new StringContent("Error conexion SAP");
                    return response;
                }
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("ErrorSAP", "{ \"La conexion con SAP se perdio\": \"No se pudo validar el archivo con con SAP.\"}");
                response.Content = new StringContent("Error conexion SAP");
                return response;
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors", "{ \"Existen Enlaces a otros archivos\": \"Existen celdas con referencias a otros archivos.\"}");
                response.Content = new StringContent("Por favor enviar un archivo en formato excel sin referencias a otros libros excel o formulas(.xls, .xslx)");
                return response;
            }
        }

        [HttpGet]
        [Route("api/payroll/GetErrors/{id}")]
        public IHttpActionResult GetErrors(int id)
        {
            var process = _context.DistProcesses.FirstOrDefault(p => p.Id == id);

            if (process == null)
                return NotFound();

            if (process.State != ProcessState.ERROR && process.State != ProcessState.WARNING)
                return Ok();

            var err = _context.DistLogErroreses.Where(e => e.DistProcessId == process.Id && !e.Inspected)
                .Include(e => e.Error)
                .Select(e => new { e.Id, e.ErrorId, e.Error.Name, e.Error.Description, e.Error.Type, e.Archivos, e.CUNI })
                .OrderBy(x => x.Type);
            return Ok(err);
        }


        [HttpGet]
        [Route("api/payroll/Validate/{id}")]
        public IHttpActionResult Validate(int id)
        {
            var process = _context.DistProcesses.FirstOrDefault(p => p.Id == id && p.State == ProcessState.STARTED);
            if (process == null)
                return NotFound();

            int userid = Int32.Parse(Request.Headers.GetValues("id").First());

            // todo reset ProccessEmployeeType
            var pay = _context.DistPayrolls.Where(x =>
                x.DistFile.DistProcessId == process.Id && x.EmployeeType != x.ProcedureTypeEmployee);
            if (pay.Count() > 0)
            {
                foreach (var p in pay)
                {
                    p.ProcedureTypeEmployee = p.EmployeeType;
                }

                _context.SaveChanges();
            }

            // validaciones cruzadas

            _context.Database.ExecuteSqlCommand("CALL \"" + CustomSchema.Schema + "\".FIX_ACAD(" + process.Id + ")");

            _context.Database.ExecuteSqlCommand("CALL \"" + CustomSchema.Schema + "\".VALIDATE_HASALLFILES(" + userid + "," + process.Id + ")");
            _context.Database.ExecuteSqlCommand("CALL \"" + CustomSchema.Schema + "\".VALIDATE_TIPOEMPLEADO(" + userid + "," + process.Id + ")");
            _context.Database.ExecuteSqlCommand("CALL \"" + CustomSchema.Schema + "\".VALIDATE_CE(" + userid + "," + process.Id + ")");
            _context.Database.ExecuteSqlCommand("CALL \"" + CustomSchema.Schema + "\".VALIDATE_OD(" + userid + "," + process.Id + ")");
            _context.Database.ExecuteSqlCommand("CALL \"" + CustomSchema.Schema + "\".VALIDATE_OR(" + userid + "," + process.Id + ")");

            _context.Database.ExecuteSqlCommand("CALL \"" + CustomSchema.Schema + "\".VALIDATE_CUADRARDESCUENTOS(" + userid + "," + process.Id + ")");
            _context.Database.ExecuteSqlCommand("CALL \"" + CustomSchema.Schema + "\".VALIDATE_ACADSUM(" + userid + "," + process.Id + ")");
            _context.Database.ExecuteSqlCommand("CALL \"" + CustomSchema.Schema + "\".VALIDATE_OTHERINCOMES(" + userid + "," + process.Id + ")");
            _context.Database.ExecuteSqlCommand("CALL \"" + CustomSchema.Schema + "\".VALIDATE_HORASTRABAJADASpi(" + userid + "," + process.Id + ")");


            var err = _context.DistLogErroreses.Where(e => e.DistProcessId == process.Id && !e.Inspected).Include(e => e.Error).Select(e => new { e.Id, e.ErrorId, e.Error.Name, e.Error.Description, e.Error.Type, e.Archivos, e.CUNI });
            if (err.Count() > 0)
            {
                if (err.Where(e => e.Type == "E").Count() > 0)
                    process.State = ProcessState.ERROR;
                else
                    process.State = ProcessState.WARNING;
                _context.SaveChanges();
                return Ok("Se encontró errores en los archivos subidos");
            }

            process.State = ProcessState.VALIDATED;
            _context.SaveChanges();
            return Ok("La información es correcta");

        }

        [HttpGet]
        [Route("api/payroll/AcceptWarnings/{id}")]
        public IHttpActionResult AcceptWarnings(int id)
        {
            var process = _context.DistProcesses.FirstOrDefault(p => p.Id == id && p.State == ProcessState.WARNING);
            if (process == null)
                return NotFound();
            process.State = ProcessState.VALIDATED;
            _context.SaveChanges();
            return Ok();
        }

        [HttpGet]
        [Route("api/payroll/Distribute/{id}")]
        public IHttpActionResult Distribute(int id)
        {
            var process = _context.DistProcesses.FirstOrDefault(p => p.Id == id && p.State == ProcessState.VALIDATED);
            if (process == null)
                return NotFound();

            _context.Database.ExecuteSqlCommand("CALL \"" + CustomSchema.Schema + "\".SET_PERCENTS(" + process.Id + ")");
            _context.Database.ExecuteSqlCommand("CALL \"" + CustomSchema.Schema + "\".DIST_PERCENTS(" + process.Id + ")");
            _context.Database.ExecuteSqlCommand("CALL \"" + CustomSchema.Schema + "\".DIST_COSTS(" + process.Id + ")");
            process.State = ProcessState.PROCESSED;
            _context.SaveChanges();
            string query = "UPDATE \"" + CustomSchema.Schema + "\".\"Dist_Cost\" dc " +
                           " set dc.\"BussinesPartner\" = CONCAT('H',p.\"CUNI\")" +
                           " from \"" + CustomSchema.Schema + "\".\"Dist_Cost\" dc\r\n" +
                           " inner join (select * from \"" + CustomSchema.Schema + "\".\"Dist_Process\" where \"State\" = 'PROCESSED' and \"Id\" = " + process.Id + ") dp\r\n" +
                           "\ton dp.\"Id\" = dc.\"DistProcessId\"\r\n" +
                           "inner join \"" + CustomSchema.Schema + "\".\"People\" p\r\n" +
                           "\ton p.\"Document\" = dc.\"Document\"\r\n" +
                           "inner join (select * from \"" + CustomSchema.Schema + "\".\"Dist_File\" where \"State\" = 'UPLOADED' and \"DistFileTypeId\" = 1) df\r\n" +
                           "\tON dp.\"Id\" = df.\"DistProcessId\"\r\n" +
                           "inner join ( select * from \"" + CustomSchema.Schema + "\".\"Dist_Payroll\" where \"ModoPago\" = 'CHQ') dpy\r\n" +
                           "\tON df.\"Id\" = dpy.\"DistFileId\"\r\n" +
                           "\tand dpy.cuni = p.cuni\r\n" +
                           "where dc.\"Columna\" = 'S_PPAGAR' \r\n" +
                           "and dc.\"DistProcessId\" = " + process.Id + ";";
            _context.Database.ExecuteSqlCommand(query);

            return Ok("Se procesó la información");
        }

        [HttpGet]
        [Route("api/payroll/Process")]
        public IHttpActionResult GetProcesses()
        {
            var processes = _context.DistProcesses.Include(p => p.Branches)
                .Where(p => p.State != ProcessState.CANCELED).
                Select(p => new
                {
                    p.BranchesId,
                    p.Branches.Name,
                    p.State,
                    p.Id,
                    p.gestion,
                    p.mes
                }).OrderByDescending(x => x.gestion).ThenByDescending(x => x.mes).ThenBy(x => x.BranchesId);

            var user = auth.getUser(Request);
            var res = auth.filerByRegional(processes, user);

            return Ok(res);
        }

        [HttpDelete]
        [Route("api/payroll/Process/{id}")]
        public IHttpActionResult Process(int id)
        {

            var processInDB = _context.DistProcesses.FirstOrDefault(p => p.Id == id && (p.State != ProcessState.CANCELED && p.State != ProcessState.INSAP));
            var user = auth.getUser(Request);
            ADClass ad = new ADClass();
            var rols = ad.getUserRols(user).FirstOrDefault(x => x.Name == "Admin");
            if (rols != null)
                processInDB = _context.DistProcesses.FirstOrDefault(p => p.Id == id && (p.State != ProcessState.CANCELED));
            if (processInDB == null)
                return BadRequest("No se Puede borrar este Proceso.");
            processInDB.State = ProcessState.CANCELED;
            _context.SaveChanges();

            var files = _context.FileDbs.Where(f => f.State == FileState.UPLOADED && f.DistProcessId == processInDB.Id);
            foreach (var file in files)
            {
                file.State = FileState.CANCELED;
            }
            _context.SaveChanges();
            return Ok("Proceso Cancelado");
        }

        [HttpGet]
        [Route("api/payroll/GetDistribution/{id}")]
        public HttpResponseMessage GetDistribution(int id)
        {
            HttpResponseMessage response = new HttpResponseMessage();

            var pro = _context.DistProcesses.Include(x => x.Branches).FirstOrDefault(x => x.Id == id);
            if (pro == null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                return response;
            }

            string query =
                "SELECT " +
                /*Start TEST join personas*/
                " p.\"FirstSurName\" \"Paterno\", p.\"SecondSurName\" \"Materno\", p.\"Names\" \"Nombres\", " +
                /*END TEST join personas*/
                " a.\"Document\" \"Documento\",a.\"TipoEmpleado\",a.\"Dependency\" \"Dependencia\",a.\"PEI\"," +
                " a.\"PlanEstudios\",a.\"Paralelo\",a.\"Periodo\",a.\"Project\" \"Proyecto\",a.\"BussinesPartner\" \"SocioNegocio\"," +
                " a.\"Monto\" \"MontoBase\",a.\"Porcentaje\",a.\"MontoDividido\",a.\"segmentoOrigen\"," +
                " b.\"mes\",b.\"gestion\",e.\"Name\" as Segmento ,d.\"Concept\" \"Concepto\",d.\"Name\" as CuentasContables,d.\"Indicator\" \"Indicador\",g.\"Cod\" \"UnidadOrganizacional\" " +
                " FROM \"" + CustomSchema.Schema + "\".\"Dist_Cost\" a " +
                " INNER JOIN  \"" + CustomSchema.Schema + "\".\"Dist_Process\" b " +
                " on a.\"DistProcessId\"=b.\"Id\" " +
                " AND a.\"DistProcessId\"= " + id +
                " INNER JOIN  \"" + CustomSchema.Schema + "\".\"Dist_TipoEmpleado\" c " +
                "on a.\"TipoEmpleado\"=c.\"Name\" " +
                " INNER JOIN  \"" + CustomSchema.Schema + "\".\"CuentasContables\" d " +
                " on c.\"GrupoContableId\" = d.\"GrupoContableId\" " +
                " and b.\"BranchesId\" = d.\"BranchesId\" " +
                " and a.\"Columna\" = d.\"Concept\" " +
                " INNER JOIN \"" + CustomSchema.Schema + "\".\"Branches\" e " +
                " on b.\"BranchesId\" = e.\"Id\" " +
                " LEFT JOIN \"" + CustomSchema.Schema + "\".\"Dependency\" f " +
                " on f.\"Cod\" = a.\"Dependency\" " +
                " LEFT JOIN \"" + CustomSchema.Schema + "\".\"OrganizationalUnit\" g " +
                " on g.\"Id\" = f.\"OrganizationalUnitId\" " +
                /*Start TEST join personas*/
                " LEFT JOIN \"" + CustomSchema.Schema + "\".\"People\" p" +
                " on p.\"Document\"= a.\"Document\"";
            /*END TEST join personas*/
            IEnumerable<Distribution> dist = _context.Database.SqlQuery<Distribution>(query).ToList();

            var ex = new XLWorkbook();
            var d = new Distribution();
            ex.Worksheets.Add(d.CreateDataTable(dist), "TotalDetalle");


            var ms = new MemoryStream();
            ex.SaveAs(ms);
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StreamContent(ms);
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
            response.Content.Headers.ContentDisposition.FileName = pro.Branches.Abr + pro.gestion + pro.mes + "TotalDetalle.xlsx";
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Content.Headers.ContentLength = ms.Length;
            ms.Seek(0, SeekOrigin.Begin);
            return response;
        }

        [HttpGet]
        [Route("api/payroll/GetTotalGeneral/{id}")]
        public HttpResponseMessage GetTotalGeneral(int id)
        {
            HttpResponseMessage response = new HttpResponseMessage();

            var pro = _context.DistProcesses.Include(x => x.Branches).FirstOrDefault(x => x.Id == id);
            if (pro == null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                return response;
            }

            IEnumerable<Distribution> dist = _context.Database.SqlQuery<Distribution>("SELECT a.\"Document\" \"Documento\",a.\"TipoEmpleado\",a.\"Dependency\" \"Dependencia\",a.\"PEI\"," +
                " a.\"PlanEstudios\",a.\"Paralelo\",a.\"Periodo\",a.\"Project\" \"Proyecto\",a.\"BussinesPartner\" \"SocioNegocio\"," +
                " a.\"Monto\" \"MontoBase\",a.\"Porcentaje\",a.\"MontoDividido\",a.\"segmentoOrigen\"," +
                " b.\"mes\",b.\"gestion\",e.\"Name\" as Segmento ,d.\"Concept\" \"Concepto\",d.\"Name\" as CuentasContables,d.\"Indicator\" \"Indicador\" " +
            " FROM \"" + CustomSchema.Schema + "\".\"Dist_Cost\" a " +
                " INNER JOIN  \"" + CustomSchema.Schema + "\".\"Dist_Process\" b " +
                " on a.\"DistProcessId\"=b.\"Id\" " +
            " AND a.\"DistProcessId\"= " + id +
            " INNER JOIN  \"" + CustomSchema.Schema + "\".\"Dist_TipoEmpleado\" c " +
                "on a.\"TipoEmpleado\"=c.\"Name\" " +
            " INNER JOIN  \"" + CustomSchema.Schema + "\".\"CuentasContables\" d " +
               " on c.\"GrupoContableId\" = d.\"GrupoContableId\" " +
            " and b.\"BranchesId\" = d.\"BranchesId\" " +
            " and a.\"Columna\" = d.\"Concept\" " +
            " INNER JOIN \"" + CustomSchema.Schema + "\".\"Branches\" e " +
               " on b.\"BranchesId\" = e.\"Id\"").ToList();

            var groupedD = dist.Where(x => x.Indicador == "D").GroupBy(x => new
            {
                x.Concepto,
                x.Indicador
            })
                .Select(y => new
                {
                    Concepto = y.First().Concepto,
                    Debe = y.Sum(z => z.MontoDividido),
                    Haber = 0.0m
                })
                .OrderBy(x => x.Concepto);
            var groupedH = dist.Where(x => x.Indicador == "H").GroupBy(x => new
            {
                x.Concepto,
                x.Indicador
            })
                .Select(y => new
                {
                    Concepto = y.First().Concepto,
                    Debe = 0.0m,
                    Haber = y.Sum(z => z.MontoDividido)
                })
                .OrderBy(x => x.Concepto);
            var res = groupedD.Concat(groupedH);

            var ex = new XLWorkbook();
            var d = new Distribution();
            ex.Worksheets.Add(d.CreateDataTable(res), "TotalGeneral");


            var ms = new MemoryStream();
            ex.SaveAs(ms);
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StreamContent(ms);
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
            response.Content.Headers.ContentDisposition.FileName = pro.Branches.Abr + pro.gestion + pro.mes + "TotalGeneral.xlsx";
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Content.Headers.ContentLength = ms.Length;
            ms.Seek(0, SeekOrigin.Begin);
            return response;
        }

        [HttpGet]
        [Route("api/payroll/GetTotalCuenta/{id}")]
        public HttpResponseMessage GetTotalCuenta(int id)
        {
            HttpResponseMessage response = new HttpResponseMessage();

            var pro = _context.DistProcesses.Include(x => x.Branches).FirstOrDefault(x => x.Id == id);
            if (pro == null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                return response;
            }

            IEnumerable<Distribution> dist = _context.Database.SqlQuery<Distribution>("SELECT a.\"Document\" \"Documento\",a.\"TipoEmpleado\",a.\"Dependency\" \"Dependencia\",a.\"PEI\"," +
              " a.\"PlanEstudios\",a.\"Paralelo\",a.\"Periodo\",a.\"Project\" \"Proyecto\",a.\"BussinesPartner\" \"SocioNegocio\"," +
              " a.\"Monto\" \"MontoBase\",a.\"Porcentaje\",a.\"MontoDividido\",a.\"segmentoOrigen\"," +
              " b.\"mes\",b.\"gestion\",e.\"Name\" as Segmento ,d.\"Concept\" \"Concepto\",d.\"Name\" as CuentasContables,d.\"Indicator\" \"Indicador\" " +
            " FROM \"" + CustomSchema.Schema + "\".\"Dist_Cost\" a " +
                " INNER JOIN  \"" + CustomSchema.Schema + "\".\"Dist_Process\" b " +
                " on a.\"DistProcessId\"=b.\"Id\" " +
            " AND a.\"DistProcessId\"= " + id +
            " INNER JOIN  \"" + CustomSchema.Schema + "\".\"Dist_TipoEmpleado\" c " +
                "on a.\"TipoEmpleado\"=c.\"Name\" " +
            " INNER JOIN  \"" + CustomSchema.Schema + "\".\"CuentasContables\" d " +
               " on c.\"GrupoContableId\" = d.\"GrupoContableId\" " +
            " and b.\"BranchesId\" = d.\"BranchesId\" " +
            " and a.\"Columna\" = d.\"Concept\" " +
            " INNER JOIN \"" + CustomSchema.Schema + "\".\"Branches\" e " +
               " on b.\"BranchesId\" = e.\"Id\"").ToList();

            var groupedD = dist.Where(x => x.Indicador == "D").GroupBy(x => new
            {
                x.CuentasContables,
                x.Indicador
            })
                .Select(y => new
                {
                    Cuenta = y.First().CuentasContables,
                    Debe = y.Sum(z => z.MontoDividido),
                    Haber = 0.0m
                }).OrderBy(x => x.Cuenta);
            var groupedH = dist.Where(x => x.Indicador == "H").GroupBy(x => new
            {
                x.CuentasContables,
                x.Indicador
            })
                .Select(y => new
                {
                    Cuenta = y.First().CuentasContables,
                    Debe = 0.0m,
                    Haber = y.Sum(z => z.MontoDividido)
                }).OrderBy(x => x.Cuenta);
            var res = groupedD.Concat(groupedH);

            var ex = new XLWorkbook();
            var d = new Distribution();
            ex.Worksheets.Add(d.CreateDataTable(res), "TotalPorCuenta");


            var ms = new MemoryStream();
            ex.SaveAs(ms);
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StreamContent(ms);
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
            response.Content.Headers.ContentDisposition.FileName = pro.Branches.Abr + pro.gestion + pro.mes + "TotalPorCuenta.xlsx";
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Content.Headers.ContentLength = ms.Length;
            ms.Seek(0, SeekOrigin.Begin);
            return response;
        }

        [HttpGet]
        [Route("api/payroll/processRows/{id}")]
        public IHttpActionResult GetSAPResumeRows(int id)
        {
            var pro = _context.DistProcesses.Include(x => x.Branches).FirstOrDefault(x => x.Id == id);
            if (pro == null)
            {
                return NotFound();
            }
            IEnumerable<SapVoucher> dist = _context.Database.SqlQuery<SapVoucher>("SELECT \"ParentKey\",\"LineNum\",\"AccountCode\",sum(\"Debit\") \"Debit\",sum(\"Credit\") \"Credit\", \"ShortName\", null as \"LineMemo\",\"ProjectCode\",\"CostingCode\",\"CostingCode2\",\"CostingCode3\",\"CostingCode4\",\"CostingCode5\",\"BPLId\" " +
                                                                                        " FROM (" +
                                                                                        " select x.\"Id\" \"ParentKey\"," +
                                                                                        "  null \"LineNum\"," +
                                                                                        "  coalesce(b.\"AcctCode\",x.\"CUENTASCONTABLES\") \"AccountCode\"," +
                                                                                        "  CASE WHEN x.\"Indicator\"='D' then x.\"MontoDividido\" else 0 end as \"Debit\"," +
                                                                                        "  CASE WHEN x.\"Indicator\"='H' then x.\"MontoDividido\"else 0 end as \"Credit\"," +
                                                                                        "  x.\"BussinesPartner\" \"ShortName\"," +
                                                                                        "  x.\"Concept\" \"LineMemo\"," +
                                                                                        "  x.\"Project\" \"ProjectCode\"," +
                                                                                        "  f.\"Cod\" \"CostingCode\"," +
                                                                                        "  x.\"PEI\" \"CostingCode2\"," +
                                                                                        "  x.\"PlanEstudios\" \"CostingCode3\"," +
                                                                                        "  x.\"Paralelo\" \"CostingCode4\"," +
                                                                                        "  x.\"Periodo\" \"CostingCode5\"," +
                                                                                        "  x.\"CodigoSAP\" \"BPLId\"" +
                                                                                        " from  (SELECT a.\"Id\",  a.\"Document\",a.\"TipoEmpleado\",a.\"Dependency\",a.\"PEI\"," +
                                                                                        "           a.\"PlanEstudios\",a.\"Paralelo\",a.\"Periodo\",a.\"Project\"," +
                                                                                        "           a.\"Monto\",a.\"Porcentaje\",a.\"MontoDividido\",a.\"segmentoOrigen\",a.\"BussinesPartner\"," +
                                                                                        "           b.\"mes\",b.\"gestion\",e.\"Name\" as Segmento ,d.\"Concept\",d.\"Name\" as CuentasContables,d.\"Indicator\", e.\"CodigoSAP\"" +
                                                                                        "           FROM \"" + CustomSchema.Schema + "\".\"Dist_Cost\" a " +
                                                                                        "               INNER JOIN  \"" + CustomSchema.Schema + "\".\"Dist_Process\" b " +
                                                                                        "               on a.\"DistProcessId\"=b.\"Id\" " +
                                                                                        "           AND a.\"DistProcessId\"= " + id +
                                                                                        "           INNER JOIN  \"" + CustomSchema.Schema + "\".\"Dist_TipoEmpleado\" c " +
                                                                                        "                on a.\"TipoEmpleado\"=c.\"Name\" " +
                                                                                        "           INNER JOIN  \"" + CustomSchema.Schema + "\".\"CuentasContables\" d " +
                                                                                        "              on c.\"GrupoContableId\" = d.\"GrupoContableId\"" +
                                                                                        "           and b.\"BranchesId\" = d.\"BranchesId\" " +
                                                                                        "           and a.\"Columna\" = d.\"Concept\" " +
                                                                                        "           INNER JOIN \"" + CustomSchema.Schema + "\".\"Branches\" e " +
                                                                                        "              on b.\"BranchesId\" = e.\"Id\") x" +
                                                                                        " left join \"" + ConfigurationManager.AppSettings["B1CompanyDB"] + "\".oact b" +
                                                                                        " on x.CUENTASCONTABLES=b.\"FormatCode\"" +
                                                                                        " left join \"" + CustomSchema.Schema + "\".\"Dependency\" d" +
                                                                                        " on x.\"Dependency\"=d.\"Cod\"" +
                                                                                        " left join \"" + CustomSchema.Schema + "\".\"OrganizationalUnit\" f" +
                                                                                        " on d.\"OrganizationalUnitId\"=f.\"Id\"" +
                                                                                        ") V " +
                                                                                        "GROUP BY \"ParentKey\",\"LineNum\",\"AccountCode\", \"ShortName\",\"ProjectCode\",\"CostingCode\",\"CostingCode2\",\"CostingCode3\",\"CostingCode4\",\"CostingCode5\",\"BPLId\";").ToList();
            var dist1 = dist.GroupBy(g => new
            {
                g.AccountCode,
                g.ShortName,
                g.CostingCode,
                g.CostingCode2,
                g.CostingCode3,
                g.CostingCode4,
                g.CostingCode5,
                g.ProjectCode,
                g.BPLId
            })
                .Select(g => new
                {
                    g.Key.AccountCode,
                    g.Key.ShortName,
                    g.Key.CostingCode,
                    g.Key.CostingCode2,
                    g.Key.CostingCode3,
                    g.Key.CostingCode4,
                    g.Key.CostingCode5,
                    g.Key.ProjectCode,
                    g.Key.BPLId,
                    Credit = g.Sum(s => Double.Parse(s.Credit)),
                    Debit = g.Sum(s => Double.Parse(s.Debit))
                }).OrderBy(z => z.Debit == 0.00d ? 1 : 0).ThenBy(z => z.AccountCode);

            dynamic res = new JObject();

            res.rowCount = dist1.Count();
            return Ok(res);
        }

        [HttpPost]
        [Route("api/payroll/GetSAPResume/{id}")]
        public HttpResponseMessage GetSAPResume(int id, JObject data)
        {
            HttpResponseMessage response = new HttpResponseMessage();

            if (data == null || data["date"] == null)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                return response;
            }
            ValidateAuth authval = new ValidateAuth();
            var user = authval.getUser(Request);
            bool sendToSAP = true;
            bool uploadedToSAP = false;
            bool dowloadDataTransfer = false;
            B1Connection b1conn = B1Connection.Instance();

            var pro = _context.DistProcesses.Include(x => x.Branches).FirstOrDefault(x => x.Id == id);
            if (pro == null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                return response;
            }

            DateTime date = DateTime.Parse(data["date"].ToString());
            pro.RegisterDate = date;
            _context.SaveChanges();
            if (sendToSAP && b1conn.connectedtoB1)
            {
                var newkey = b1conn.addVoucher(user.Id, pro);
                uploadedToSAP = newkey != "ERROR";
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StringContent("{\"newkey\": \"" + newkey + "\"}");
                pro.ComprobanteSAP = newkey;
            }
            if (dowloadDataTransfer && (!uploadedToSAP || !sendToSAP))
            {
                IEnumerable<SapVoucher> dist = _context.Database.SqlQuery<SapVoucher>("SELECT 'BatchNum' \"ParentKey\",'LineNum' \"LineNum\",'Cuentas' \"AccountCode\",'Debe BS' \"Debit\",'Credito BS' \"Credit\",'ShortName' \"ShortName\", 'Glosa de linea' as \"LineMemo\",'Project' \"ProjectCode\",'ProfitCode' \"CostingCode\",'OcrCode2' \"CostingCode2\",'OcrCode3' \"CostingCode3\",'OcrCode4' \"CostingCode4\",'OcrCode5' \"CostingCode5\",'BPLId' \"BPLId\" from dummy " +
                                                                                  " union  SELECT \"ParentKey\",\"LineNum\",\"AccountCode\",case when replace(sum(\"Debit\"),',','.')='0.00' then null else replace(sum(\"Debit\"),',','.') end \"Debit\",case when replace(sum(\"Credit\"),',','.')='0.00' then null else replace(sum(\"Credit\"),',','.') end \"Credit\", \"ShortName\", null as \"LineMemo\",\"ProjectCode\",\"CostingCode\",\"CostingCode2\",\"CostingCode3\",\"CostingCode4\",\"CostingCode5\",\"BPLId\" " +
                                                                                        " FROM (" +
                                                                                        " select '1' \"ParentKey\"," +
                                                                                        "  null \"LineNum\"," +
                                                                                        "  coalesce(b.\"AcctCode\",x.\"CUENTASCONTABLES\") \"AccountCode\"," +
                                                                                        "  CASE WHEN x.\"Indicator\"='D' then x.\"MontoDividido\" else 0 end as \"Debit\"," +
                                                                                        "  CASE WHEN x.\"Indicator\"='H' then x.\"MontoDividido\"else 0 end as \"Credit\"," +
                                                                                        "  x.\"BussinesPartner\" \"ShortName\"," +
                                                                                        "  x.\"Concept\" \"LineMemo\"," +
                                                                                        "  x.\"Project\" \"ProjectCode\"," +
                                                                                        "  f.\"Cod\" \"CostingCode\"," +
                                                                                        "  x.\"PEI\" \"CostingCode2\"," +
                                                                                        "  x.\"PlanEstudios\" \"CostingCode3\"," +
                                                                                        "  x.\"Paralelo\" \"CostingCode4\"," +
                                                                                        "  x.\"Periodo\" \"CostingCode5\"," +
                                                                                        "  x.\"CodigoSAP\" \"BPLId\"" +
                                                                                        " from  (SELECT a.\"Document\",a.\"TipoEmpleado\",a.\"Dependency\",a.\"PEI\"," +
                                                                                        "           a.\"PlanEstudios\",a.\"Paralelo\",a.\"Periodo\",a.\"Project\"," +
                                                                                        "           a.\"Monto\",a.\"Porcentaje\",a.\"MontoDividido\",a.\"segmentoOrigen\",a.\"BussinesPartner\"," +
                                                                                        "           b.\"mes\",b.\"gestion\",e.\"Name\" as Segmento ,d.\"Concept\",d.\"Name\" as CuentasContables,d.\"Indicator\", e.\"CodigoSAP\"" +
                                                                                        "           FROM \"" + CustomSchema.Schema + "\".\"Dist_Cost\" a " +
                                                                                        "               INNER JOIN  \"" + CustomSchema.Schema + "\".\"Dist_Process\" b " +
                                                                                        "               on a.\"DistProcessId\"=b.\"Id\" " +
                                                                                        "           AND a.\"DistProcessId\"= " + id +
                                                                                        "           INNER JOIN  \"" + CustomSchema.Schema + "\".\"Dist_TipoEmpleado\" c " +
                                                                                        "                on a.\"TipoEmpleado\"=c.\"Name\" " +
                                                                                        "           INNER JOIN  \"" + CustomSchema.Schema + "\".\"CuentasContables\" d " +
                                                                                        "              on c.\"GrupoContableId\" = d.\"GrupoContableId\"" +
                                                                                        "           and b.\"BranchesId\" = d.\"BranchesId\" " +
                                                                                        "           and a.\"Columna\" = d.\"Concept\" " +
                                                                                        "           INNER JOIN \"" + CustomSchema.Schema + "\".\"Branches\" e " +
                                                                                        "              on b.\"BranchesId\" = e.\"Id\") x" +
                                                                                        " left join \"" + ConfigurationManager.AppSettings["B1CompanyDB"] + "\".oact b" +
                                                                                        " on x.CUENTASCONTABLES=b.\"FormatCode\"" +
                                                                                        " left join \"" + CustomSchema.Schema + "\".\"Dependency\" d" +
                                                                                        " on x.\"Dependency\"=d.\"Cod\"" +
                                                                                        " left join \"" + CustomSchema.Schema + "\".\"OrganizationalUnit\" f" +
                                                                                        " on d.\"OrganizationalUnitId\"=f.\"Id\"" +
                                                                                        ") V " +
                                                                                        "GROUP BY \"ParentKey\",\"LineNum\",\"AccountCode\", \"ShortName\",\"ProjectCode\",\"CostingCode\",\"CostingCode2\",\"CostingCode3\",\"CostingCode4\",\"CostingCode5\",\"BPLId\";").ToList();

                var ex = new XLWorkbook();
                var d = new Distribution();

                var lastday = pro.gestion + pro.mes + DateTime.DaysInMonth(Int32.Parse(pro.gestion), Int32.Parse(pro.mes)).ToString();

                IEnumerable<VoucherHeader> dist1 = _context.Database.SqlQuery<VoucherHeader>("SELECT 'BatchNum' \"ParentKey\",'LineNum' \"LineNum\", 'Fecha Contabilización' \"ReferenceDate\",'Glosa del asiento' \"Memo\",'Ref1' \"Reference\",'Ref2' \"Reference2\",'TransCode' \"TransactionCode\",'Project' \"ProjectCode\",'Fecha Documento' \"TaxDate\",'Indicator' \"Indicator\",'AutoStorno' \"UseAutoStorno\",'StornoDate' \"StornoDate\",'VatDate' \"VatDate\",'Series' \"Series\",'StampTax' \"StampTax\",'DueDate' \"DueDate\",'AutoVAT' \"AutoVAT\",'ReportEU' \"ReportEU\",'Report347' \"Report347\",'Location' \"LocationCode\",'BlockDunn' \"BlockDunningLetter\",'AutoWT' \"AutomaticWT\",'Corisptivi' \"Corisptivi\" FROM DUMMY " +
                                                                                             "union SELECT '1' \"ParentKey\", null \"LineNum\", '" + lastday + "' \"ReferenceDate\",'Planilla Menusal " + pro.Branches.Abr + "-" + pro.mes + "-" + pro.gestion + "' \"Memo\",null \"Reference\",null \"Reference2\",null \"TransactionCode\",null \"ProjectCode\",'" + lastday + "' \"TaxDate\",null \"Indicator\",null \"UseAutoStorno\",null \"StornoDate\",null \"VatDate\",'" + pro.Branches.SerieComprobanteContalbeSAP + "' \"Series\",null \"StampTax\",'" + lastday + "' \"DueDate\",null \"AutoVAT\",null \"ReportEU\",null \"Report347\",null \"LocationCode\",null \"BlockDunningLetter\",null \"AutomaticWT\",null \"Corisptivi\" FROM DUMMY;");
                var n = d.CreateDataTable(dist1);
                int desiredSize = 1;

                while (n.Columns.Count > desiredSize)
                {
                    n.Columns.RemoveAt(desiredSize);
                }
                ex.Worksheets.Add(n, "Voucher");

                ex.Worksheets.Add(d.CreateDataTable(dist1), "Cabecera");

                ex.Worksheets.Add(d.CreateDataTable(dist), "Detalle");

                var ms = new MemoryStream();
                ex.SaveAs(ms);
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StreamContent(ms);
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                response.Content.Headers.ContentDisposition.FileName = "SAP_Voucher_Lines-" + pro.Branches.Abr + "-" + pro.mes + pro.gestion + ".xlsx";
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                response.Content.Headers.ContentLength = ms.Length;
                ms.Seek(0, SeekOrigin.Begin);
            }
            if (!uploadedToSAP)
            {
                response.StatusCode = HttpStatusCode.GatewayTimeout;
            }
            else
            {
                pro.State = ProcessState.INSAP;
                _context.SaveChanges();
            }

            return response;
        }
        [HttpGet]
        [Route("api/payroll/compareLastMonth/{id}")]
        public HttpResponseMessage compareLastMonth(int id)
        {
            HttpResponseMessage response = new HttpResponseMessage();

            var pro = _context.DistProcesses.Include(x => x.Branches).FirstOrDefault(x => x.Id == id);
            if (pro == null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                return response;
            }

            string lastMonth;
            string lastGestion;
            if ((Int32.Parse(pro.mes) - 1) != 0)
            {
                lastMonth = (Int32.Parse(pro.mes) - 1).ToString().PadLeft(2, '0');
                lastGestion = pro.gestion;
            }
            else
            {
                lastMonth = "12";
                lastGestion = (Int32.Parse(pro.gestion) - 1).ToString();
            }


            var lastPro = _context.DistProcesses.Include(x => x.Branches)
                .FirstOrDefault(x => x.BranchesId == pro.BranchesId
                                    && x.mes == lastMonth
                                    && x.gestion == lastGestion
                                    && (x.State == ProcessState.INSAP || x.State == ProcessState.PROCESSED)
                                    );

            if (lastPro == null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                response.Content = new StringContent("No existe mes anterior en SAP, para procesar el pedido");
                return response;
            }

            string query = "select coalesce(thismonth.\"CUNI\", lastmonth.\"CUNI\") \"CUNI\", " +
                           "\r\ncoalesce(thismonth.\"Document\", lastmonth.\"Document\") \"Documento\", " +
                           "\r\nconcat(p.\"FirstSurName\", " +
                           "\r\n     concat( case when p.\"UseSecondSurName\"=1 then concat(' ',p.\"SecondSurName\") else '' end, " +
                           "\r\n         concat( case when p.\"UseMariedSurName\"=1 then concat(' ',p.\"MariedSurName\") else '' end, " +
                           "\r\n         concat(' ',p.\"Names\")" +
                           "\r\n            ) " +
                           "\r\n        ) " +
                           "\r\n    ) \"NombreCompleto\"," +
                           "\r\ncoalesce(thismonth.\"POSICION\", lastmonth.\"POSICION\") \"Posicion\"," +
                           "\r\ncoalesce(thismonth.\"BasicSalary\",0) - coalesce(lastmonth.\"BasicSalary\",0) \"difHB\", " +
                           "\r\ncoalesce(thismonth.\"AntiquityBonus\",0) - coalesce(lastmonth.\"AntiquityBonus\",0) \"difBA\", " +
                           "\r\ncoalesce(thismonth.\"OtherIncome\",0) - coalesce(lastmonth.\"OtherIncome\",0) \"difOI\", " +
                           "\r\ncoalesce(thismonth.\"TeachingIncome\",0) - coalesce(lastmonth.\"TeachingIncome\",0) \"difDOC\", " +
                           "\r\ncoalesce(thismonth.\"OtherAcademicIncomes\",0) - coalesce(lastmonth.\"OtherAcademicIncomes\",0) \"difOAA\", " +
                           "\r\ncoalesce(thismonth.\"AFPLaboral\",0) - coalesce(lastmonth.\"AFPLaboral\",0) \"difAFPL\", " +
                           "\r\n\r\ncoalesce(thismonth.\"BasicSalary\",0) \"actHB\", \r\ncoalesce(thismonth.\"AntiquityBonus\",0) \"actBA\", " +
                           "\r\ncoalesce(thismonth.\"OtherIncome\",0) \"actOI\", " +
                           "\r\ncoalesce(thismonth.\"TeachingIncome\",0) \"actDOC\", " +
                           "\r\ncoalesce(thismonth.\"OtherAcademicIncomes\",0) \"actOAA\", " +
                           "\r\ncoalesce(thismonth.\"AFPLaboral\",0) \"actAFPL\", " +
                           "\r\ncoalesce(lastmonth.\"BasicSalary\",0) \"antHB\", " +
                           "\r\ncoalesce(lastmonth.\"AntiquityBonus\",0) \"antBA\", " +
                           "\r\ncoalesce(lastmonth.\"OtherIncome\",0) \"antOI\", " +
                           "\r\ncoalesce(lastmonth.\"TeachingIncome\",0) \"antDOC\", " +
                           "\r\ncoalesce(lastmonth.\"OtherAcademicIncomes\",0) \"antOAA\", " +
                           "\r\ncoalesce(lastmonth.\"AFPLaboral\",0) \"antAFPL\" " +
                           "\r\nfrom ( " +
                           "\r\nselect dp.\"CUNI\", \"Document\", \"Names\", \"FirstSurName\", \"SecondSurName\", \"MariedSurName\", \"POSICION\"," +
                           "\r\n \"BasicSalary\", \"AntiquityBonus\", \"OtherIncome\", \"TeachingIncome\", \"OtherAcademicIncomes\", " +
                           "\r\n \"AFPLaboral\" " +
                           "\r\nfrom " + CustomSchema.Schema + ".\"Dist_Payroll\" dp" +
                           "\r\ninner join (SELECT\r\n\t \"F\".\"CUNI\" ,\r\n\t \"F\".\"Positions\" AS \"POSICION\",\r\n\t \"F\".\"PositionDescription\" AS \"CARGO\"" +
                           "\r\nfrom (\r\nselect\r\n\t x.*\r\n\tfrom ( select\r\n\t a.\"Id\",\r\n\t a.cuni,\r\n\t b.\"Name\" as \"Positions\",\r\n\t a.\"PositionDescription\"," +
                           "\r\n\t ROW_NUMBER() OVER ( PARTITION BY cuni \r\n\t\t\torder by \t\r\n\t\t\ta.\"Active\" desc,\r\n\t b.\"LevelId\" asc,\r\n\t c.\"Cod\" desc," +
                           "\r\n\t (case when a.\"EndDate\" is null\r\n\t\t\t\tthen 1 \r\n\t\t\t\telse 0 \r\n\t\t\t\tend) desc,\r\n\t a.\"EndDate\" desc ) AS row_num " +
                           "\r\n\t\tfrom " + CustomSchema.Schema + ".\"ContractDetail\" a \r\n\t\tinner join " + CustomSchema.Schema + ".\"Position\" b on a.\"PositionsId\" = b.\"Id\" " +
                           "\r\n\t\tinner join " + CustomSchema.Schema + ".\"Dependency\" c on a.\"DependencyId\" = c.\"Id\" " +
                           "\r\n\t\tinner join " + CustomSchema.Schema + ".\"OrganizationalUnit\" o on c.\"OrganizationalUnitId\" = o.\"Id\" " +
                           "\r\n\t\tinner join " + CustomSchema.Schema + ".\"Branches\" d on c.\"BranchesId\" = d.\"Id\" " +
                           "\r\n\t\tinner join " + CustomSchema.Schema + ".\"TableOfTables\"e on a.\"Linkage\"= e.\"Id\" " +
                           "\r\n\t\twhere ((a.\"EndDate\" is null and year(a.\"StartDate\")*100+month(a.\"StartDate\")<=" + pro.gestion + "*100+" + pro.mes + ")" +
                           "\r\n\t\tor " + pro.gestion + "*100+" + pro.mes + " between year(a.\"StartDate\")*100+month(a.\"StartDate\") and year(a.\"EndDate\")*100+month(a.\"EndDate\"))) x " +
                           "\r\n\tinner join " + CustomSchema.Schema + ".\"People\" p on x.cuni = p.cuni " +
                           "\r\n\twhere row_num = 1 " +
                           "\r\n\t) f ) p on p.\"CUNI\" = dp.\"CUNI\"" +
                           "\r\nwhere \"DistFileId\" in (select \"Id\" from " + CustomSchema.Schema + ".\"Dist_File\" " +
                           "\r\n\t\t\twhere \"DistProcessId\" =  " + pro.Id +
                           "\r\n\t\t\tand \"DistFileTypeId\" = 1 \r\n\t\t\tand \"State\"='UPLOADED') \r\n\r\n\r\n) thismonth " +
                           "\r\n\r\nFull outer join  \r\n\r\n( \r\n\r\nselect dp.\"CUNI\", \"Document\", \"Names\", \"FirstSurName\", \"SecondSurName\", \"MariedSurName\", \"POSICION\"," +
                           "\r\n\"BasicSalary\", \"AntiquityBonus\", \"OtherIncome\", \"TeachingIncome\", \"OtherAcademicIncomes\", " +
                           "\r\n\"AFPLaboral\" \r\nfrom " + CustomSchema.Schema + ".\"Dist_Payroll\" dp\r\ninner join (SELECT\r\n\t \"F\".\"CUNI\" ,\r\n\t \"F\".\"Positions\" AS \"POSICION\"," +
                           "\r\n\t \"F\".\"PositionDescription\" AS \"CARGO\"\r\nfrom (\r\nselect\r\n\t x.*\r\n\tfrom ( select\r\n\t a.\"Id\",\r\n\t a.cuni,\r\n\t b.\"Name\" as \"Positions\"," +
                           "\r\n\t a.\"PositionDescription\",\r\n\t ROW_NUMBER() OVER ( PARTITION BY cuni \r\n\t\t\torder by \t\r\n\t\t\ta.\"Active\" desc,\r\n\t b.\"LevelId\" asc," +
                           "\r\n\t c.\"Cod\" desc,\r\n\t (case when a.\"EndDate\" is null\r\n\t\t\t\tthen 1 \r\n\t\t\t\telse 0 \r\n\t\t\t\tend) desc,\r\n\t a.\"EndDate\" desc ) AS row_num " +
                           "\r\n\t\tfrom " + CustomSchema.Schema + ".\"ContractDetail\" a \r\n\t\tinner join " + CustomSchema.Schema + ".\"Position\" b on a.\"PositionsId\" = b.\"Id\" " +
                           "\r\n\t\tinner join " + CustomSchema.Schema + ".\"Dependency\" c on a.\"DependencyId\" = c.\"Id\" " +
                           "\r\n\t\tinner join " + CustomSchema.Schema + ".\"OrganizationalUnit\" o on c.\"OrganizationalUnitId\" = o.\"Id\" " +
                           "\r\n\t\tinner join " + CustomSchema.Schema + ".\"Branches\" d on c.\"BranchesId\" = d.\"Id\" \r\n\t\tinner join " + CustomSchema.Schema + ".\"TableOfTables\"e on a.\"Linkage\"= e.\"Id\" " +
                           "\r\n\t\twhere ((a.\"EndDate\" is null and year(a.\"StartDate\")*100+month(a.\"StartDate\")<=" + lastPro.gestion + "*100+" + lastPro.mes + ")" +
                           "\r\n\t\tor " + lastPro.gestion + "*100+" + lastPro.mes + " between year(a.\"StartDate\")*100+month(a.\"StartDate\") and year(a.\"EndDate\")*100+month(a.\"EndDate\"))) x " +
                           "\r\n\tinner join " + CustomSchema.Schema + ".\"People\" p on x.cuni = p.cuni \r\n\twhere row_num = 1 \r\n\t) f ) p on p.\"CUNI\" = dp.\"CUNI\"" +
                           "\r\nwhere \"DistFileId\" in (select \"Id\" from " + CustomSchema.Schema + ".\"Dist_File\" \r\n\t\t\twhere \"DistProcessId\" =  " + lastPro.Id +
                           "\r\n\t\t\tand \"DistFileTypeId\" = 1 " +
                           "\r\n\t\t\tand \"State\"='UPLOADED') \r\n\t\t\t\r\n) lastmonth \r\n\r\non thismonth.\"CUNI\" = lastmonth.\"CUNI\" " +
                           "\r\ninner join " + CustomSchema.Schema + ".\"People\" p \r\n on coalesce(thismonth.\"CUNI\", lastmonth.\"CUNI\") = p.\"CUNI\" " +
                           "\r\nwhere coalesce(thismonth.\"BasicSalary\",0) != coalesce(lastmonth.\"BasicSalary\",0) " +
                           "\r\nor coalesce(thismonth.\"AntiquityBonus\",0) != coalesce(lastmonth.\"AntiquityBonus\",0) " +
                           "\r\nor coalesce(thismonth.\"OtherIncome\",0) != coalesce(lastmonth.\"OtherIncome\",0) " +
                           "\r\nor coalesce(thismonth.\"TeachingIncome\",0) != coalesce(lastmonth.\"TeachingIncome\",0) " +
                           "\r\nor coalesce(thismonth.\"OtherAcademicIncomes\",0) != coalesce(lastmonth.\"OtherAcademicIncomes\",0) " +
                           "\r\nor coalesce(thismonth.\"AFPLaboral\",0) != coalesce(lastmonth.\"AFPLaboral\",0) ";

            IEnumerable<Comparativo> dist = _context.Database.SqlQuery<Comparativo>(query).ToList();

            var ex = new XLWorkbook();
            var d = new Distribution();
            ex.Worksheets.Add(d.CreateDataTable(dist), "Diferencias Anterior Planilla");

            //sheet.Cell(1, 2).InsertData(dist);


            var ms = new MemoryStream();
            ex.SaveAs(ms);
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StreamContent(ms);
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
            response.Content.Headers.ContentDisposition.FileName = pro.Branches.Abr + pro.gestion + pro.mes + "VerificaCambios.xlsx";
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Content.Headers.ContentLength = ms.Length;
            ms.Seek(0, SeekOrigin.Begin);
            return response;
        }

        [HttpGet]
        [Route("api/payroll/SumTotalesPlanilla/{id}")]
        public IHttpActionResult SumTotalesPlanilla(int id)
        {
            var process = _context.DistProcesses.FirstOrDefault(p => p.Id == id && p.State == ProcessState.VALIDATED);

            if (process == null)
                return NotFound();

            var file = _context.FileDbs.FirstOrDefault(f => f.DistFileTypeId == 1 && f.DistProcessId == process.Id && f.State == FileState.UPLOADED);

            if (file == null)
                return NotFound();

            var sum = _context.DistPayrolls.Where(p => p.DistFileId == file.Id).Select(
                p => new
                {
                    p.BasicSalary,
                    p.AntiquityBonus,
                    p.OtherIncome,
                    p.TeachingIncome,
                    p.OtherAcademicIncomes,
                    p.Reintegro,
                    p.TotalAmountEarned,
                    p.AFPLaboral,
                    p.RcIva,
                    p.Discounts,
                    p.TotalAmountDiscounts,
                    p.TotalAfterDiscounts,
                    p.AFPPatronal,
                    p.SeguridadCortoPlazoPatronal,
                    p.ProvAguinaldo,
                    p.ProvPrimas,
                    p.ProvIndeminizacion
                });

            List<string> names = new List<string>()
            {
                "Haber Basico",
                "Bono Antiguedad",
                "Otros Ingresos",
                "Ingresos por Docencia",
                "Ingresos por otras actividades academicas",
                "Reintegro",
                "Total Ganado",
                "Aporte Laboral AFP",
                "RC IVA",
                "Descuentos",
                "Total Deducciones",
                "Liquido Pagable",
                "Aporte patronal AFP",
                "Aporte patronal SCP",
                "Provision Aguinaldos",
                "Provision Primas",
                "Provision Indeminizacion",
            };
            List<JObject> result = new List<JObject>();

            List<decimal> totales = new List<decimal>()
            {
                sum.Sum(p => p.BasicSalary),
                sum.Sum(p => p.AntiquityBonus),
                sum.Sum(p => p.OtherIncome),
                sum.Sum(p => p.TeachingIncome),
                sum.Sum(p => p.OtherAcademicIncomes),
                sum.Sum(p => p.Reintegro),
                sum.Sum(p => p.TotalAmountEarned),
                sum.Sum(p => p.AFPLaboral),
                sum.Sum(p => p.RcIva),
                sum.Sum(p => p.Discounts),
                sum.Sum(p => p.TotalAmountDiscounts),
                sum.Sum(p => p.TotalAfterDiscounts),
                sum.Sum(p => p.AFPPatronal),
                sum.Sum(p => p.SeguridadCortoPlazoPatronal),
                sum.Sum(p => p.ProvAguinaldo),
                sum.Sum(p => p.ProvPrimas),
                sum.Sum(p => p.ProvIndeminizacion),
            };

            for (int j = 0; j < totales.Count; j++)
            {
                dynamic re = new JObject();
                re.name = names[j];
                re.total = totales[j];
                result.Add(re);
            }


            return Ok(result);
        }


        [HttpPost]
        [Route("api/payroll/SendPreliminarSAP/{id}")]
        public HttpResponseMessage SendPreliminarSAP(int id, JObject data)
        {

            HttpResponseMessage response = new HttpResponseMessage();

            if (data == null || data["regionalOrigen"] == null)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                return response;
            }
            ValidateAuth authval = new ValidateAuth();
            var user = authval.getUser(Request);
            bool sendToSAP = true;
            bool uploadedToSAP = false;
            bool dowloadDataTransfer = false;
            B1Connection b1conn = B1Connection.Instance();

            var pro = _context.DistProcesses.Include(x => x.Branches).FirstOrDefault(x => x.Id == id);
            if (pro == null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                return response;
            }
            //string regionalOrigen= (data["regionalOrigen"].ToString());

            string regionalO = (data["regionalOrigen"].ToString());
            int reg = _context.Branch.Where(x => x.Abr == regionalO).Select(x => x.Id).FirstOrDefault();
            string processRegionalOrigen = _context.DistProcesses.Where(x => x.mes == pro.mes).Where(x => x.gestion == pro.gestion).Where(x => x.BranchesId == reg).Where(x  => x.State == "INSAP").Select(x => x.Id).FirstOrDefault().ToString();
            int procOr = int.Parse(processRegionalOrigen);

            //string Abr = _context.Branch.Where(x => x.Id == branchId).Select(x => x.Abr).FirstOrDefault();
            //DateTime date = DateTime.Parse(data["date"].ToString());
            //pro.RegisterDate = date;
            _context.SaveChanges();

            Dist_Interregional dint = new Dist_Interregional();

            dint.Id = Dist_Interregional.GetNextId(_context);
            dint.UploadedDate = DateTime.Now;
            dint.BranchesId = pro.BranchesId;
            dint.segmentoOrigen = reg;
            dint.mes = pro.mes;
            dint.gestion = pro.gestion;
            dint.User = user.Id;
            
            if (sendToSAP && b1conn.connectedtoB1)
            {
                var newkey = b1conn.addPreliminar(user.Id, pro, reg, procOr);
                uploadedToSAP = newkey != "ERROR";
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StringContent("{\"newkey\": \"" + newkey + "\"}");
                dint.TransNumber = newkey;
            }
            if (dowloadDataTransfer && (!uploadedToSAP || !sendToSAP))
            {
                IEnumerable<SapVoucher> dist = _context.Database.SqlQuery<SapVoucher>("SELECT 'BatchNum' \"ParentKey\",'LineNum' \"LineNum\",'Cuentas' \"AccountCode\",'Debe BS' \"Debit\",'Credito BS' \"Credit\",'ShortName' \"ShortName\", 'Glosa de linea' as \"LineMemo\",'Project' \"ProjectCode\",'ProfitCode' \"CostingCode\",'OcrCode2' \"CostingCode2\",'OcrCode3' \"CostingCode3\",'OcrCode4' \"CostingCode4\",'OcrCode5' \"CostingCode5\",'BPLId' \"BPLId\" from dummy " +
                                                                                  " union  SELECT \"ParentKey\",\"LineNum\",\"AccountCode\",case when replace(sum(\"Debit\"),',','.')='0.00' then null else replace(sum(\"Debit\"),',','.') end \"Debit\",case when replace(sum(\"Credit\"),',','.')='0.00' then null else replace(sum(\"Credit\"),',','.') end \"Credit\", \"ShortName\", null as \"LineMemo\",\"ProjectCode\",\"CostingCode\",\"CostingCode2\",\"CostingCode3\",\"CostingCode4\",\"CostingCode5\",\"BPLId\" " +
                                                                                        " FROM (" +
                                                                                        " select '1' \"ParentKey\"," +
                                                                                        "  null \"LineNum\"," +
                                                                                        "  coalesce(b.\"AcctCode\",x.\"CUENTASCONTABLES\") \"AccountCode\"," +
                                                                                        "  CASE WHEN x.\"Indicator\"='D' then x.\"MontoDividido\" else 0 end as \"Debit\"," +
                                                                                        "  CASE WHEN x.\"Indicator\"='H' then x.\"MontoDividido\"else 0 end as \"Credit\"," +
                                                                                        "  x.\"BussinesPartner\" \"ShortName\"," +
                                                                                        "  x.\"Concept\" \"LineMemo\"," +
                                                                                        "  x.\"Project\" \"ProjectCode\"," +
                                                                                        "  f.\"Cod\" \"CostingCode\"," +
                                                                                        "  x.\"PEI\" \"CostingCode2\"," +
                                                                                        "  x.\"PlanEstudios\" \"CostingCode3\"," +
                                                                                        "  x.\"Paralelo\" \"CostingCode4\"," +
                                                                                        "  x.\"Periodo\" \"CostingCode5\"," +
                                                                                        "  x.\"CodigoSAP\" \"BPLId\"" +
                                                                                        " from  (SELECT a.\"Document\",a.\"TipoEmpleado\",a.\"Dependency\",a.\"PEI\"," +
                                                                                        "           a.\"PlanEstudios\",a.\"Paralelo\",a.\"Periodo\",a.\"Project\"," +
                                                                                        "           a.\"Monto\",a.\"Porcentaje\",a.\"MontoDividido\",a.\"segmentoOrigen\",a.\"BussinesPartner\"," +
                                                                                        "           b.\"mes\",b.\"gestion\",e.\"Name\" as Segmento ,d.\"Concept\",d.\"Name\" as CuentasContables,d.\"Indicator\", e.\"CodigoSAP\"" +
                                                                                        "           FROM \"" + CustomSchema.Schema + "\".\"Dist_Cost\" a " +
                                                                                        "               INNER JOIN  \"" + CustomSchema.Schema + "\".\"Dist_Process\" b " +
                                                                                        "               on a.\"DistProcessId\"=b.\"Id\" " +
                                                                                        "           AND a.\"DistProcessId\"= " + id +
                                                                                        "           INNER JOIN  \"" + CustomSchema.Schema + "\".\"Dist_TipoEmpleado\" c " +
                                                                                        "                on a.\"TipoEmpleado\"=c.\"Name\" " +
                                                                                        "           INNER JOIN  \"" + CustomSchema.Schema + "\".\"CuentasContables\" d " +
                                                                                        "              on c.\"GrupoContableId\" = d.\"GrupoContableId\"" +
                                                                                        "           and b.\"BranchesId\" = d.\"BranchesId\" " +
                                                                                        "           and a.\"Columna\" = d.\"Concept\" " +
                                                                                        "           INNER JOIN \"" + CustomSchema.Schema + "\".\"Branches\" e " +
                                                                                        "              on b.\"BranchesId\" = e.\"Id\") x" +
                                                                                        " left join \"" + ConfigurationManager.AppSettings["B1CompanyDB"] + "\".oact b" +
                                                                                        " on x.CUENTASCONTABLES=b.\"FormatCode\"" +
                                                                                        " left join \"" + CustomSchema.Schema + "\".\"Dependency\" d" +
                                                                                        " on x.\"Dependency\"=d.\"Cod\"" +
                                                                                        " left join \"" + CustomSchema.Schema + "\".\"OrganizationalUnit\" f" +
                                                                                        " on d.\"OrganizationalUnitId\"=f.\"Id\"" +
                                                                                        ") V " +
                                                                                        "GROUP BY \"ParentKey\",\"LineNum\",\"AccountCode\", \"ShortName\",\"ProjectCode\",\"CostingCode\",\"CostingCode2\",\"CostingCode3\",\"CostingCode4\",\"CostingCode5\",\"BPLId\";").ToList();

                var ex = new XLWorkbook();
                var d = new Distribution();

                var lastday = pro.gestion + pro.mes + DateTime.DaysInMonth(Int32.Parse(pro.gestion), Int32.Parse(pro.mes)).ToString();

                IEnumerable<VoucherHeader> dist1 = _context.Database.SqlQuery<VoucherHeader>("SELECT 'BatchNum' \"ParentKey\",'LineNum' \"LineNum\", 'Fecha Contabilización' \"ReferenceDate\",'Glosa del asiento' \"Memo\",'Ref1' \"Reference\",'Ref2' \"Reference2\",'TransCode' \"TransactionCode\",'Project' \"ProjectCode\",'Fecha Documento' \"TaxDate\",'Indicator' \"Indicator\",'AutoStorno' \"UseAutoStorno\",'StornoDate' \"StornoDate\",'VatDate' \"VatDate\",'Series' \"Series\",'StampTax' \"StampTax\",'DueDate' \"DueDate\",'AutoVAT' \"AutoVAT\",'ReportEU' \"ReportEU\",'Report347' \"Report347\",'Location' \"LocationCode\",'BlockDunn' \"BlockDunningLetter\",'AutoWT' \"AutomaticWT\",'Corisptivi' \"Corisptivi\" FROM DUMMY " +
                                                                                             "union SELECT '1' \"ParentKey\", null \"LineNum\", '" + lastday + "' \"ReferenceDate\",'Planilla Menusal " + pro.Branches.Abr + "-" + pro.mes + "-" + pro.gestion + "' \"Memo\",null \"Reference\",null \"Reference2\",null \"TransactionCode\",null \"ProjectCode\",'" + lastday + "' \"TaxDate\",null \"Indicator\",null \"UseAutoStorno\",null \"StornoDate\",null \"VatDate\",'" + pro.Branches.SerieComprobanteContalbeSAP + "' \"Series\",null \"StampTax\",'" + lastday + "' \"DueDate\",null \"AutoVAT\",null \"ReportEU\",null \"Report347\",null \"LocationCode\",null \"BlockDunningLetter\",null \"AutomaticWT\",null \"Corisptivi\" FROM DUMMY;");
                var n = d.CreateDataTable(dist1);
                int desiredSize = 1;

                while (n.Columns.Count > desiredSize)
                {
                    n.Columns.RemoveAt(desiredSize);
                }
                ex.Worksheets.Add(n, "Voucher");

                ex.Worksheets.Add(d.CreateDataTable(dist1), "Cabecera");

                ex.Worksheets.Add(d.CreateDataTable(dist), "Detalle");

                var ms = new MemoryStream();
                ex.SaveAs(ms);
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StreamContent(ms);
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                response.Content.Headers.ContentDisposition.FileName = "SAP_Voucher_Lines-" + pro.Branches.Abr + "-" + pro.mes + pro.gestion + ".xlsx";
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                response.Content.Headers.ContentLength = ms.Length;
                ms.Seek(0, SeekOrigin.Begin);
            }
            if (!uploadedToSAP)
            {
                response.StatusCode = HttpStatusCode.GatewayTimeout;
            }
            else
            {
                dint.State = ProcessState.INSAP;
                _context.DistInterregionales.Add(dint);
                _context.SaveChanges();
            }

            return response;
        }


    }

}
