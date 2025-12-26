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

            // 1) Base query EXACTAMENTE como la tenías cuando funcionaba
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
            a.CiDocente,
                    a.PrimerApellido,
                    a.SegundoApellido,
                    a.TercerApellido,
                    a.Nombres,

            // Datos académicos
            a.Periodo,
                    a.Sigla,
                    a.CodigoParalelo,
                    a.Paralelo,

            // Datos de carga horaria
            a.HorasSemana,
                    a.HorasMes,
                    a.UnidadOrganizacional,
                    a.Sede,
                    a.CostoHora,

            // Calculado (igual que antes)
            MontoTotal = a.HorasMes * a.CostoHora,

            // Número de contrato
            a.NumeroContrato
                };

            // 2) Filtrar por sedes autorizadas (igual que antes)
            var filtrados = auth.filerByRegional(baseQuery.AsQueryable(), user);

            // 3) MATERIALIZAR – acá recién se ejecuta el SQL
            var lista = filtrados.ToList();

            // 4) NUEVO: agregamos NombreCompleto EN MEMORIA, sin tocar HANA
            var resultado = lista
                .Select(a =>
                {
                    var partes = new[]
                    {
                a.PrimerApellido,
                a.SegundoApellido,
                a.TercerApellido,
                a.Nombres
                    }
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim());

                    var nombreCompleto = string.Join(" ", partes);

                    return new
                    {
                        a.Id,
                // ya no nos importa BranchesId en el front, así que lo podemos omitir
                a.CiDocente,
                        a.PrimerApellido,
                        a.SegundoApellido,
                        a.TercerApellido,
                        a.Nombres,

                // 🔹 nuevo campo que mandamos al front
                NombreCompleto = nombreCompleto,

                        a.Periodo,
                        a.Sigla,
                        a.CodigoParalelo,
                        a.Paralelo,
                        a.HorasSemana,
                        a.HorasMes,
                        a.UnidadOrganizacional,
                        a.Sede,
                        a.CostoHora,
                        a.MontoTotal,
                        a.NumeroContrato
                    };
                })
                .ToList();

            // 5) MUY IMPORTANTE: devolvemos un ARRAY, no { data, total }
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
            public string Observaciones { get; set; }
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

            var asignaciones = _context.AsignacionesCarga
                .Where(a => a.AsigProcesoId == model.FileId &&
                            model.AssignmentIds.Contains(a.Id))
                .ToList();

            // NEW: Validate same CI
            if (asignaciones.Count > 1)
            {
                var uniqueCIs = asignaciones
                    .Select(a => a.CiDocente)
                    .Distinct()
                    .ToList();

                if (uniqueCIs.Count > 1)
                {
                    return Content(
                        HttpStatusCode.BadRequest,
                        new
                        {
                            Message = "No se puede asignar el mismo número de contrato a docentes diferentes (CI distintos)."
                        }
                    );
                }
            }

            foreach (var a in asignaciones)
            {
                a.NumeroContrato = model.ContractNumber;
                // If you want to store observaciones per assignment, add a field to the model
                // Otherwise, you could store it at proceso level or in a separate table
            }

            proceso.LastUpdateBy = user.Id;
            _context.SaveChanges();

            return Ok();
        }

        public class AddAsignacionManualRequest
        {
            public int FileId { get; set; }

            public string CiDocente { get; set; }
            public string PrimerApellido { get; set; }
            public string SegundoApellido { get; set; }
            public string TercerApellido { get; set; }
            public string Nombres { get; set; }

            public string Sigla { get; set; }
            public string CodigoParalelo { get; set; }
            public string Paralelo { get; set; }

            public decimal HorasSemana { get; set; }
            public decimal HorasMes { get; set; }
            public decimal CostoHora { get; set; }
        }

        private class CivilRowMini
        {
            public string NIT { get; set; }
            public int BranchesId { get; set; }
            public string FullName { get; set; }
        }

        private class ParaleloRowMini
        {
            public string CODIGOSAP { get; set; }
            public string SIGLA { get; set; }
            public string NUMPARALELO { get; set; }
            public string CODUNIDADORGANIZACIONAL { get; set; }
            public string SEDE { get; set; }
            public string PERIODOSAP { get; set; }
        }

        public class NuevaAsignacionRequest
        {
            public int ProcesoId { get; set; }

            public string CiDocente { get; set; }
            public string PrimerApellido { get; set; }
            public string SegundoApellido { get; set; }
            public string TercerApellido { get; set; }
            public string Nombres { get; set; }

            public string Periodo { get; set; }
            public string Sigla { get; set; }
            public string CodigoParalelo { get; set; }
            public string Paralelo { get; set; }

            public decimal HorasSemana { get; set; }
            public decimal HorasMes { get; set; }
            public decimal CostoHora { get; set; }
        }

        public class ActualizarAsignacionRequest
        {
            public int Id { get; set; }  // Id de AsignacionCarga a editar

            public string CiDocente { get; set; }
            public string PrimerApellido { get; set; }
            public string SegundoApellido { get; set; }
            public string TercerApellido { get; set; }
            public string Nombres { get; set; }

            public string Periodo { get; set; }
            public string Sigla { get; set; }
            public string CodigoParalelo { get; set; }
            public string Paralelo { get; set; }

            public decimal HorasSemana { get; set; }
            public decimal HorasMes { get; set; }
            public decimal CostoHora { get; set; }
        }



        [HttpPost]
        [Route("AddSingle")]
        public IHttpActionResult AddSingle([FromBody] NuevaAsignacionRequest model)
        {
            if (model == null)
                return BadRequest("Datos vacíos.");

            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var proceso = _context.AsigProcesos.FirstOrDefault(p => p.Id == model.ProcesoId);
            if (proceso == null)
                return NotFound();

            var periodoProceso = (proceso.PeriodoId ?? string.Empty).Trim();

            // Misma lógica de permisos que en GetDetail / AssignContractNumber
            var procesosUser = auth
                .filerByRegional(_context.AsigProcesos, user)
                .OfType<AsigProceso>();

            // Ya esta validado
            //if (!procesosUser.Any(p => p.Id == model.ProcesoId))
                //return Unauthorized();

            // Reusar AsignacionesExcel solo como "validador"
            using (var dummyStream = new MemoryStream())
            {
                var excelHelper = new AsignacionesExcel(
                    dummyStream,
                    _context,
                    "manual",
                    proceso,
                    user
                );

                // 1) Validar los datos EXACTAMENTE con la misma lógica del Excel
                var rowErrors = excelHelper.ValidateRowValues(
                    model.CiDocente,
                    periodoProceso,
                    model.Sigla,
                    model.CodigoParalelo,
                    model.Paralelo,
                    model.PrimerApellido,
                    model.SegundoApellido,
                    model.TercerApellido,
                    model.Nombres
                );

                if (rowErrors.Any())
                {
                    // → mismos mensajes que verías al validar el Excel
                    return Content(
                        HttpStatusCode.BadRequest,
                        new { Errors = rowErrors }
                    );
                }

                // 2) Buscar el paralelo en T_REG_PARALELOS_NS
                //    para obtener UnidadOrganizacional y Sede AUTOMÁTICAMENTE
                var paraleloMatch = excelHelper.FindParalelo(
                    model.CodigoParalelo?.Trim(),
                    periodoProceso,
                    model.Sigla?.Trim(),
                    model.Paralelo?.Trim()
                );

                // 3) Construir la AsignacionCarga igual que MapRowToAsignacion
                var asignacion = new AsignacionCarga
                {
                    Id = AsignacionCarga.GetNextId(_context),
                    AsigProcesoId = proceso.Id,

                    CiDocente = model.CiDocente?.Trim(),
                    PrimerApellido = model.PrimerApellido?.Trim(),
                    SegundoApellido = model.SegundoApellido?.Trim(),
                    TercerApellido = model.TercerApellido?.Trim(),
                    Nombres = model.Nombres?.Trim(),

                    Periodo = periodoProceso,
                    Sigla = model.Sigla?.Trim(),
                    CodigoParalelo = model.CodigoParalelo?.Trim(),
                    Paralelo = model.Paralelo?.Trim(),

                    HorasSemana = model.HorasSemana,
                    HorasMes = model.HorasMes,
                    CostoHora = model.CostoHora,

                    NumeroContrato = null
                };

                // 4) Rellenar Sede y UnidadOrganizacional como en la carga de Excel
                if (paraleloMatch != null)
                {
                    asignacion.UnidadOrganizacional = paraleloMatch.CODUNIDADORGANIZACIONAL;
                    asignacion.Sede = paraleloMatch.SEDE;
                }
                else
                {
                    // En teoría no debería ocurrir porque ya fue validado,
                    // pero dejamos un fallback por seguridad
                    asignacion.UnidadOrganizacional = string.Empty;
                    asignacion.Sede = string.Empty;
                }

                _context.AsignacionesCarga.Add(asignacion);
                _context.SaveChanges();

                // 5) Devolver la fila para que el front la agregue a la tabla
                return Ok(new
                {
                    asignacion.Id,
                    asignacion.CiDocente,
                    asignacion.PrimerApellido,
                    asignacion.SegundoApellido,
                    asignacion.TercerApellido,
                    asignacion.Nombres,
                    asignacion.Periodo,
                    asignacion.Sigla,
                    asignacion.CodigoParalelo,
                    asignacion.Paralelo,
                    asignacion.HorasSemana,
                    asignacion.HorasMes,
                    asignacion.CostoHora,
                    asignacion.Sede,
                    asignacion.UnidadOrganizacional,
                    MontoTotal = asignacion.HorasMes * asignacion.CostoHora,
                    asignacion.NumeroContrato
                });
            }
        }

        [HttpPost]
        [Route("UpdateSingle")]
        public IHttpActionResult UpdateSingle([FromBody] ActualizarAsignacionRequest model)
        {
            if (model == null)
                return BadRequest("Datos vacíos.");

            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            // 1) Buscar la asignación a editar
            var asignacion = _context.AsignacionesCarga.FirstOrDefault(a => a.Id == model.Id);
            if (asignacion == null)
                return NotFound();

            // 2) Obtener el proceso asociado a esa asignación
            var proceso = _context.AsigProcesos.FirstOrDefault(p => p.Id == asignacion.AsigProcesoId);
            if (proceso == null)
                return BadRequest("El proceso asociado a la asignación no existe.");

            // 3) Validar permisos 
            var procesosUser = auth
                .filerByRegional(_context.AsigProcesos, user)
                .OfType<AsigProceso>();

            Console.WriteLine(procesosUser);



            // 4) Reusar AsignacionesExcel como validador
            using (var dummyStream = new MemoryStream())
            {
                var excelHelper = new AsignacionesExcel(
                    dummyStream,
                    _context,
                    "manual-edit",
                    proceso,
                    user
                );

                // Definimos qué período usar para validar
                //    - Si el modelo trae uno, podrías usarlo
                //    - O forzar siempre el del proceso:
                var periodoParaValidar = !string.IsNullOrWhiteSpace(model.Periodo)
                    ? model.Periodo
                    : asignacion.Periodo ?? proceso.PeriodoId;

                var rowErrors = excelHelper.ValidateRowValues(
                    model.CiDocente,
                    periodoParaValidar,
                    model.Sigla,
                    model.CodigoParalelo,
                    model.Paralelo,
                    model.PrimerApellido,
                    model.SegundoApellido,
                    model.TercerApellido,
                    model.Nombres
                );

                if (rowErrors.Any())
                {
                    return Content(
                        HttpStatusCode.BadRequest,
                        new { Errors = rowErrors }
                    );
                }

                // 5) Recalcular Sede y UnidadOrganizacional desde Parallelos
                var paraleloMatch = excelHelper.FindParalelo(
                    model.CodigoParalelo?.Trim(),
                    periodoParaValidar?.Trim(),
                    model.Sigla?.Trim(),
                    model.Paralelo?.Trim()
                );

                // 6) Actualizar la entidad
                asignacion.CiDocente = model.CiDocente?.Trim();
                asignacion.PrimerApellido = model.PrimerApellido?.Trim();
                asignacion.SegundoApellido = model.SegundoApellido?.Trim();
                asignacion.TercerApellido = model.TercerApellido?.Trim();
                asignacion.Nombres = model.Nombres?.Trim();

                asignacion.Periodo = periodoParaValidar?.Trim();
                asignacion.Sigla = model.Sigla?.Trim();
                asignacion.CodigoParalelo = model.CodigoParalelo?.Trim();
                asignacion.Paralelo = model.Paralelo?.Trim();

                asignacion.HorasSemana = model.HorasSemana;
                asignacion.HorasMes = model.HorasMes;
                asignacion.CostoHora = model.CostoHora;

                if (paraleloMatch != null)
                {
                    asignacion.UnidadOrganizacional = paraleloMatch.CODUNIDADORGANIZACIONAL;
                    asignacion.Sede = paraleloMatch.SEDE;
                }

                // Si tu entidad AsignacionCarga tiene auditoría:
                // asignacion.UpdatedAt = DateTime.UtcNow;  // o corriente según tu proyecto
                // asignacion.UpdatedBy = user.IdUsuario;   // ajusta el campo real

                _context.SaveChanges();

                // 7) Opcional: devolver datos actualizados (mismo shape que GetDetail)
                var partesNombre = new[]
                {
            asignacion.PrimerApellido,
            asignacion.SegundoApellido,
            asignacion.TercerApellido,
            asignacion.Nombres
        }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim());

                var nombreCompleto = string.Join(" ", partesNombre);

                return Ok(new
                {
                    asignacion.Id,
                    asignacion.CiDocente,
                    asignacion.PrimerApellido,
                    asignacion.SegundoApellido,
                    asignacion.TercerApellido,
                    asignacion.Nombres,
                    NombreCompleto = nombreCompleto,
                    asignacion.Periodo,
                    asignacion.Sigla,
                    asignacion.CodigoParalelo,
                    asignacion.Paralelo,
                    asignacion.HorasSemana,
                    asignacion.HorasMes,
                    asignacion.UnidadOrganizacional,
                    asignacion.Sede,
                    asignacion.CostoHora,
                    MontoTotal = asignacion.HorasMes * asignacion.CostoHora,
                    asignacion.NumeroContrato
                });
            }
        }

        // Helper class for the response
        public class BuscarCodigoParaleloResponse
        {
            public string CodigoParalelo { get; set; }
            public string UnidadOrganizacional { get; set; }
            public string Sede { get; set; }
        }

        [HttpGet]
        [Route("BuscarCodigoParalelo")]
        public IHttpActionResult BuscarCodigoParalelo(int procesoId, string sigla, string paralelo)
        {
            if (string.IsNullOrWhiteSpace(sigla) || string.IsNullOrWhiteSpace(paralelo))
                return BadRequest("Sigla y Paralelo son requeridos");

            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            // 1) Get the proceso to obtain BranchesId and PeriodoId
            var proceso = _context.AsigProcesos.FirstOrDefault(p => p.Id == procesoId);
            if (proceso == null)
                return NotFound();

            // 2) Get the Branch to find the Abr (which corresponds to SEDE in T_REG_PARALELOS_NS)
            var branch = _context.Branch.FirstOrDefault(b => b.Id == proceso.BranchesId);
            if (branch == null)
                return BadRequest("Sede no encontrada");

            var sedeAbr = branch.Abr; // This is what matches SEDE in T_REG_PARALELOS_NS
            var periodo = proceso.PeriodoId;

            // 3) Query T_REG_PARALELOS_NS - Use HanaParameter instead of SqlParameter
            var sql = "SELECT CODIGOSAP, SIGLA, NUMPARALELO, SEDE, PERIODOSAP, CODUNIDADORGANIZACIONAL " +
                      "FROM ADMNAL.T_REG_PARALELOS_NS " +
                      "WHERE SIGLA = :sigla " +
                      "AND NUMPARALELO = :paralelo " +
                      "AND SEDE = :sede " +
                      "AND PERIODOSAP = :periodo";

            var results = _context.Database.SqlQuery<ParaleloRowMini>(sql,
                new Sap.Data.Hana.HanaParameter("sigla", sigla.Trim()),
                new Sap.Data.Hana.HanaParameter("paralelo", paralelo.Trim()),
                new Sap.Data.Hana.HanaParameter("sede", sedeAbr),
                new Sap.Data.Hana.HanaParameter("periodo", periodo)
            ).ToList();

            if (!results.Any())
            {
                return Content(
                    HttpStatusCode.NotFound,
                    new { Message = $"No se encontró ningún paralelo con Sigla='{sigla}', Paralelo='{paralelo}', Sede='{sedeAbr}', Periodo='{periodo}'" }
                );
            }

            // Should be unique, but just in case
            var result = results.First();

            return Ok(new
            {
                CodigoParalelo = result.CODIGOSAP,
                UnidadOrganizacional = result.CODUNIDADORGANIZACIONAL,
                Sede = result.SEDE
            });
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


        // Helpers
       

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
