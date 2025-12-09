using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;

using UcbBack.Logic;
using UcbBack.Models;
using System.Data;
using UcbBack.Models.Auth;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using UcbBack.Logic.ExcelFiles.Asignaciones;

namespace UcbBack.Controllers
{
    [RoutePrefix("api/AsignacionesMasivas")]
    public class AsignacionesMasivasController : ApiController
    {
        private readonly ApplicationDbContext _context;
        private readonly ValidateAuth auth;

        public AsignacionesMasivasController()
        {
            _context = new ApplicationDbContext();
            auth = new ValidateAuth();
        }

        // ---------------------------
        //  1) Upload Excel
        // ---------------------------
        [HttpPost]
        [Route("UploadFile")]
        public async Task<HttpResponseMessage> UploadFile()
        {
            var response = new HttpResponseMessage();

            try
            {
                var req = await Request.Content.ReadAsMultipartAsync();
                dynamic o = await HttpContentToVariables(req);

                // Verificamos que existan las claves en el ExpandoObject
                var dict = (IDictionary<string, object>)o;

                // Validación básica
                if (!dict.ContainsKey("BranchesId") ||
                    !dict.ContainsKey("PeriodoId") ||
                    !dict.ContainsKey("fileName") ||
                    !dict.ContainsKey("excelStream") ||
                    string.IsNullOrWhiteSpace(o.BranchesId?.ToString()) ||
                    string.IsNullOrWhiteSpace(o.PeriodoId?.ToString()) ||
                    !o.fileName.ToString().EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors",
                        "{ \"Faltan datos\": \"Debe enviar BranchesId, PeriodoId y un archivo excel (.xlsx).\"}");
                    response.Content = new StringContent("Parámetros incompletos o no válidos");
                    return response;
                }
                int branchesId;
                if (!int.TryParse(o.BranchesId.ToString(), out branchesId))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Headers.Add("UploadErrors",
                        "{ \"BranchesId inválido\": \"BranchesId debe ser numérico.\"}");
                    response.Content = new StringContent("BranchesId inválido");
                    return response;
                }

                string periodoId = o.PeriodoId.ToString().Trim();
                string fileName = o.fileName.ToString();



                var user = auth.getUser(Request);

                // Crear proceso con ids ya parseados
                var proceso = AddFileToProceso(branchesId, periodoId, user.Id);

                // Procesar Excel con validaciones
                var excel = new AsignacionesExcel(
                    o.excelStream,
                    _context,
                    fileName,
                    proceso,
                    user,
                    headerin: 1,
                    sheets: 1
                );

                HttpResponseMessage excelErrorResponse;
                if (!excel.ValidateFile(out excelErrorResponse))
                {
                    // Si hubo errores en el Excel, devolvemos la respuesta generada
                    return excelErrorResponse;
                }

                proceso.State = "INICIADO";
                _context.SaveChanges();

                var payload = new
                {
                    id = proceso.Id,
                    state = proceso.State
                };

                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );
                return response;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("UploadErrors",
                    "{ \"Ocurrió un problema\": \"Contáctese con el administrador.\"}");
                response.Content = new StringContent(e.ToString());
                return response;
            }
        }


        // ---------------------------
        //  2) GetDetail -> tabla del paso 2
        // ---------------------------
        [HttpGet]
        [Route("GetDetail/{id}")]
        public IHttpActionResult GetDetail(int id)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            // 1) Base query: Asignaciones + Proceso (para tener BranchesId)
            var baseQuery =
                from a in _context.AsignacionesCarga
                join p in _context.AsigProcesos on a.AsigProcesoId equals p.Id
                where a.AsigProcesoId == id
                orderby a.Id
                select new
                {
                    a.Id,
                    p.BranchesId,   // IMPORTANTE para filerByRegional

            // Datos del docente
            CiDocente = a.CiDocente,
                    PrimerApellido = a.PrimerApellido,
                    SegundoApellido = a.SegundoApellido,
                    TercerApellido = a.TercerApellido,
                    Nombres = a.Nombres,

            // Datos académicos
            Periodo = a.Periodo,
                    Sigla = a.Sigla,
                    CodigoParalelo = a.CodigoParalelo,
                    Paralelo = a.Paralelo,

            // Datos de carga horaria
            HorasSemana = a.HorasSemana,
                    HorasMes = a.HorasMes,
                    UnidadOrganizacional = a.UnidadOrganizacional,
                    Sede = a.Sede,
                    CostoHora = a.CostoHora,

            // Calculado
            MontoTotal = a.HorasMes * a.CostoHora,

            // Número de contrato
            NumeroContrato = a.NumeroContrato
                };

            // 2) Filtrar por sedes autorizadas (igual que en PagosPendientes)
            var filtrados = auth.filerByRegional(baseQuery.AsQueryable(), user);

            // 3) Materializar y devolver
            var resultado = filtrados.ToList(); // tendrá BranchesId pero el front simplemente no lo usa

            return Ok(resultado);
        }


        // ---------------------------
        //  3) Asignar número de contrato en masa
        // ---------------------------
        public class AssignContractRequest
        {
            public int FileId { get; set; }
            public string ContractNumber { get; set; }
            public List<int> AssignmentIds { get; set; }
        }

        [HttpPost]
        [Route("AssignContractNumber")]
        public IHttpActionResult AssignContractNumber([FromBody] AssignContractRequest model)
        {
            if (model == null || model.AssignmentIds == null || model.AssignmentIds.Count == 0)
                return BadRequest("No se enviaron asignaciones.");

            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var proceso = _context.AsigProcesos.FirstOrDefault(p => p.Id == model.FileId);
            if (proceso == null)
                return NotFound();

   
            //    Asumimos que si pudo crear el proceso ya estaba autorizado.

            var asignaciones = _context.AsignacionesCarga
                .Where(a => a.AsigProcesoId == model.FileId &&
                            model.AssignmentIds.Contains(a.Id))
                .ToList();

            foreach (var a in asignaciones)
            {
                a.NumeroContrato = model.ContractNumber;
            }

            // Ajusta el nombre según tu modelo (LastUpdatedBy / LastUpdateBy)
            proceso.LastUpdateBy = user.Id;

            _context.SaveChanges();

            return Ok();
        }



        [HttpGet]
        [Route("Periodos")]
        public IHttpActionResult GetPeriodos()
        {
            // Query you provided
            var sql = "SELECT PERIODOSAP FROM ADMNAL.T_REG_PARALELOS_NS GROUP BY PERIODOSAP;";

            var periodos = _context.Database
                .SqlQuery<string>(sql)
                .ToList()
                .Select((p, index) => new
                {
                    Id = index + 1, // simple incremental id for the dropdown
            Name = p,       // what the user will see, e.g. "2025-1"
            Value = p       // raw value to save/send back
        });

            return Ok(periodos);
        }


        // ---------------------------
        // Helpers (similar patrón a ServContract)
        // ---------------------------

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
                    var raw = await contentPart.ReadAsStringAsync();
                    res.BranchesId = raw?.Trim(); // lo validamos luego como int
                }
                else if (varname == "\"PeriodoId\"")
                {
                    var raw = await contentPart.ReadAsStringAsync();
                    res.PeriodoId = raw?.Trim();  // aquí ya será "1S2019"
                }
                else if (varname == "\"file\"")
                {
                    Stream stream = await contentPart.ReadAsStreamAsync();
                    res.fileName = string.IsNullOrEmpty(contentDisposition.FileName)
                        ? ""
                        : contentDisposition.FileName.Trim('"');
                    res.excelStream = stream;
                }
            }
            return res;
        }


        [NonAction]
        private AsigProceso AddFileToProceso(int branchesId, string periodoId, int userId)
        {
            var proceso = new AsigProceso
            {
                Id = AsigProceso.GetNextId(_context),   // <-- usa el GetNextId nuevo
                BranchesId = branchesId,
                PeriodoId = periodoId,
                State = "INICIADO",
                CreatedAt = DateTime.Now,
                CreatedBy = userId
            };

            _context.AsigProcesos.Add(proceso);
            _context.SaveChanges();
            return proceso;
        }


    }
}
