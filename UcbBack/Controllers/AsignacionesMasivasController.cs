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

        //  1) Upload Excel

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



        //  2) GetDetail -> tabla del paso 2
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
                    a.CantidadMeses,  // NEW
                                      // NEW CALCULATION: HorasMes * CostoHora * CantidadMeses
                    MontoTotal = a.HorasMes * a.CostoHora * a.CantidadMeses,



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
                        a.CiDocente,
                        a.PrimerApellido,
                        a.SegundoApellido,
                        a.TercerApellido,
                        a.Nombres,
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
                        a.CantidadMeses,
                        a.MontoTotal,
                        a.NumeroContrato
                    };
                })
                .ToList();

            // 5) MUY IMPORTANTE: devolvemos un ARRAY, no { data, total }
            return Ok(resultado);
        }




        //  3) Asignar número de contrato en masa

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

            // Validate same CI
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

            // NEW: Check for duplicate CodigoParalelo (WARNING, not error)
            var duplicateCodigoParalelo = asignaciones
                .Where(a => !string.IsNullOrWhiteSpace(a.CodigoParalelo))
                .GroupBy(a => a.CodigoParalelo.Trim())
                .Where(g => g.Count() > 1)
                .Select(g => new
                {
                    CodigoParalelo = g.Key,
                    Count = g.Count()
                })
                .ToList();

            string warning = null;
            if (duplicateCodigoParalelo.Any())
            {
                var codes = string.Join(", ", duplicateCodigoParalelo.Select(d => string.Format("{0} ({1}x)", d.CodigoParalelo, d.Count)));
                warning = string.Format("⚠️ Advertencia: Hay códigos de paralelo duplicados en este contrato: {0}. Esto puede ser correcto si el mismo docente dicta múltiples horarios del mismo paralelo.", codes);
            }

            // Assign contract number
            foreach (var a in asignaciones)
            {
                a.NumeroContrato = model.ContractNumber;
            }

            proceso.LastUpdateBy = user.Id;
            _context.SaveChanges();

            var response = new
            {
                Message = "Número de contrato asignado correctamente",
                Warning = warning
            };

            return Ok(response);
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
            public int CantidadMeses { get; set; }
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
            public int CantidadMeses { get; set; }
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

            using (var dummyStream = new MemoryStream())
            {
                var excelHelper = new AsignacionesExcel(
                    dummyStream,
                    _context,
                    "manual",
                    proceso,
                    user
                );

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
                    return Content(
                        HttpStatusCode.BadRequest,
                        new { Errors = rowErrors }
                    );
                }

                var paraleloMatch = excelHelper.FindParalelo(
                    model.CodigoParalelo?.Trim(),
                    periodoProceso,
                    model.Sigla?.Trim(),
                    model.Paralelo?.Trim()
                );

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
                    CantidadMeses = model.CantidadMeses,
                    NumeroContrato = null
                };

                if (paraleloMatch != null)
                {
                    asignacion.UnidadOrganizacional = paraleloMatch.CODUNIDADORGANIZACIONAL;
                    asignacion.Sede = paraleloMatch.SEDE;
                }
                else
                {
                    asignacion.UnidadOrganizacional = string.Empty;
                    asignacion.Sede = string.Empty;
                }

                _context.AsignacionesCarga.Add(asignacion);
                _context.SaveChanges();

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
                    asignacion.CantidadMeses,  // NEW
                    asignacion.Sede,
                    asignacion.UnidadOrganizacional,
                    MontoTotal = asignacion.HorasMes * asignacion.CostoHora * asignacion.CantidadMeses,  // NEW CALC
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

            var asignacion = _context.AsignacionesCarga.FirstOrDefault(a => a.Id == model.Id);
            if (asignacion == null)
                return NotFound();

            var proceso = _context.AsigProcesos.FirstOrDefault(p => p.Id == asignacion.AsigProcesoId);
            if (proceso == null)
                return BadRequest("El proceso asociado a la asignación no existe.");

            using (var dummyStream = new MemoryStream())
            {
                var excelHelper = new AsignacionesExcel(
                    dummyStream,
                    _context,
                    "manual-edit",
                    proceso,
                    user
                );

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

                var paraleloMatch = excelHelper.FindParalelo(
                    model.CodigoParalelo?.Trim(),
                    periodoParaValidar?.Trim(),
                    model.Sigla?.Trim(),
                    model.Paralelo?.Trim()
                );

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
                asignacion.CantidadMeses = model.CantidadMeses;  // NEW

                if (paraleloMatch != null)
                {
                    asignacion.UnidadOrganizacional = paraleloMatch.CODUNIDADORGANIZACIONAL;
                    asignacion.Sede = paraleloMatch.SEDE;
                }

                _context.SaveChanges();

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
                    asignacion.CantidadMeses,  // NEW
                    MontoTotal = asignacion.HorasMes * asignacion.CostoHora * asignacion.CantidadMeses,  // NEW CALC
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
                    new { Message = string.Format("No se encontró ningún paralelo con Sigla='{0}', Paralelo='{1}', Sede='{2}', Periodo='{3}'", sigla, paralelo, sedeAbr, periodo) }
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

        public class DeleteAsignacionRequest
        {
            public int Id { get; set; }
        }

        [HttpPost]
        [Route("DeleteSingle")]
        public IHttpActionResult DeleteSingle([FromBody] DeleteAsignacionRequest model)
        {
            if (model == null || model.Id <= 0)
                return BadRequest("ID inválido.");

            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            // 1) Find the assignment to delete
            var asignacion = _context.AsignacionesCarga.FirstOrDefault(a => a.Id == model.Id);
            if (asignacion == null)
                return NotFound();

            // 2) Get the associated process for permission validation
            var proceso = _context.AsigProcesos.FirstOrDefault(p => p.Id == asignacion.AsigProcesoId);
            if (proceso == null)
                return BadRequest("El proceso asociado no existe.");

            // 3) NEW: Check if proceso is FINALIZADO
            if (proceso.State == "FINALIZADO")
            {
                return Content(
                    HttpStatusCode.BadRequest,
                    new { Message = "No se puede eliminar una asignación de un proceso finalizado." }
                );
            }

            // 4) Validate permissions 
            var procesosUser = auth
                .filerByRegional(_context.AsigProcesos, user)
                .OfType<AsigProceso>();

            // 5) Delete the assignment
            _context.AsignacionesCarga.Remove(asignacion);
            proceso.LastUpdateBy = user.Id;
            _context.SaveChanges();

            return Ok(new { Message = "Asignación eliminada correctamente." });
        }

        // DTO for the response
        public class ProcesoListItem
        {
            public int Id { get; set; }
            public int BranchesId { get; set; }
            public string SedeAbr { get; set; }
            public string SedeNombre { get; set; }
            public string PeriodoId { get; set; }
            public DateTime CreatedAt { get; set; }
            public string State { get; set; }
            public int TotalAsignaciones { get; set; }
        }

        [HttpGet]
        [Route("GetProcesos")]
        public IHttpActionResult GetProcesos(int page = 1, int pageSize = 5)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            // 1) Get processes with regional filtering
            var procesosBase = auth
                .filerByRegional(_context.AsigProcesos, user)
                .OfType<AsigProceso>();

            // 2) Join with Branches to get Abr and Name
            var query = from p in procesosBase
                        join b in _context.Branch on p.BranchesId equals b.Id
                        orderby p.CreatedAt descending
                        select new
                        {
                            p.Id,
                            p.BranchesId,
                            SedeAbr = b.Abr,
                            SedeNombre = b.Name,
                            p.PeriodoId,
                            p.CreatedAt,
                            p.State
                        };

            // 3) Get total count before pagination
            var total = query.Count();

            // 4) Apply pagination
            var procesos = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 5) Count asignaciones for each proceso
            var items = procesos.Select(p => new ProcesoListItem
            {
                Id = p.Id,
                BranchesId = p.BranchesId,
                SedeAbr = p.SedeAbr ?? "",
                PeriodoId = p.PeriodoId ?? "",
                CreatedAt = p.CreatedAt,
                State = p.State ?? "INICIADO",
                TotalAsignaciones = _context.AsignacionesCarga
                    .Count(a => a.AsigProcesoId == p.Id)
            }).ToList();

            return Ok(new
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        }

        [HttpGet]
        [Route("GetProceso/{id}")]
        public IHttpActionResult GetProceso(int id)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var proceso = _context.AsigProcesos.FirstOrDefault(p => p.Id == id);
            if (proceso == null)
                return NotFound();

            // Validate permissions
            var procesosUser = auth
                .filerByRegional(_context.AsigProcesos, user)
                .OfType<AsigProceso>();

            if (!procesosUser.Any(p => p.Id == id))
                return Unauthorized();

            // Get branch info
            var branch = _context.Branch.FirstOrDefault(b => b.Id == proceso.BranchesId);

            return Ok(new
            {
                Id = proceso.Id,
                BranchesId = proceso.BranchesId,
                SedeAbr = branch != null ? branch.Abr : "",
                PeriodoId = proceso.PeriodoId,
                CreatedAt = proceso.CreatedAt,
                State = proceso.State
            });
        }


        [HttpPost]
        [Route("ValidarFinalizacion/{procesoId}")]
        public IHttpActionResult ValidarFinalizacion(int procesoId)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var proceso = _context.AsigProcesos.FirstOrDefault(p => p.Id == procesoId);
            if (proceso == null)
                return NotFound();

            // Validate permissions
            var procesosUser = auth
                .filerByRegional(_context.AsigProcesos, user)
                .OfType<AsigProceso>();

            /*  if (!procesosUser.Any(p => p.Id == procesoId))
                  return Unauthorized();*/

            // Call private validation method
            var response = ValidarProcesoInterno(procesoId, proceso);

            return Ok(response);
        }

        [HttpPost]
        [Route("FinalizarProceso/{procesoId}")]
        public IHttpActionResult FinalizarProceso(int procesoId)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var proceso = _context.AsigProcesos.FirstOrDefault(p => p.Id == procesoId);
            if (proceso == null)
                return NotFound();

            // Validate permissions
            var procesosUser = auth
                .filerByRegional(_context.AsigProcesos, user)
                .OfType<AsigProceso>();

            /* if (!procesosUser.Any(p => p.Id == procesoId))
                 return Unauthorized();*/

            // Check if already finalized
            if (proceso.State == "FINALIZADO")
            {
                return Content(
                    HttpStatusCode.BadRequest,
                    new { Message = "Este proceso ya está finalizado." }
                );
            }

            // Run validation using private method
            var validacion = ValidarProcesoInterno(procesoId, proceso);

            if (!validacion.IsValid)
            {
                return Content(
                    HttpStatusCode.BadRequest,
                    new
                    {
                        Message = "El proceso no puede ser finalizado debido a errores de validación.",
                        Errors = validacion.Errors,
                        AsignacionesSinContrato = validacion.AsignacionesSinContrato,
                        ContratosDuplicados = validacion.ContratosDuplicados
                    }
                );
            }

            var asignacionesConContrato = _context.AsignacionesCarga
    .Where(a => a.AsigProcesoId == procesoId
             && a.NumeroContrato != null
             && a.NumeroContrato != "")
    .ToList();


            var asignacionesPorContrato = asignacionesConContrato
                .Where(a => !string.IsNullOrWhiteSpace(a.NumeroContrato))
                .GroupBy(a => a.NumeroContrato.Trim())
                .ToList();

            var contratosCreados = new List<AsigContrato>();

            // Create AsigContrato entries
            foreach (var grupo in asignacionesPorContrato)
            {
                var numeroContrato = grupo.Key;
                var asignaciones = grupo.ToList();

                // Calculate total amount
                var montoTotal = asignaciones.Sum(a => a.HorasMes * a.CostoHora * a.CantidadMeses);

                // Get teacher name from first assignment (all should have same teacher for same contract number)
                var primeraAsignacion = asignaciones.First();
                var nombreDocente = string.Join(" ", new[]
                {
                    primeraAsignacion.PrimerApellido,
                    primeraAsignacion.SegundoApellido,
                    primeraAsignacion.TercerApellido,
                    primeraAsignacion.Nombres
                }.Where(s => !string.IsNullOrWhiteSpace(s)));

                var contrato = new AsigContrato
                {
                    NumeroContrato = numeroContrato,
                    NombreDocente = nombreDocente,  // ADD THIS LINE
                    BranchesId = proceso.BranchesId,
                    AsigProcesoId = proceso.Id,
                    PeriodoId = proceso.PeriodoId,
                    MontoTotal = montoTotal,
                    Estado = "PENDIENTE",
                    Observaciones = null,
                    CreatedAt = DateTime.Now,
                    CreatedBy = user.Id
                };

                _context.AsigContratos.Add(contrato);
                contratosCreados.Add(contrato);
            }

            // Mark proceso as finalized
            proceso.State = "FINALIZADO";
            proceso.LastUpdateBy = user.Id;

            _context.SaveChanges();

            // Get summary
            var totalAsignaciones = _context.AsignacionesCarga.Count(a => a.AsigProcesoId == procesoId);

            return Ok(new
            {
                Message = "Proceso finalizado correctamente.",
                ProcesoId = procesoId,
                TotalAsignaciones = totalAsignaciones,
                TotalContratos = contratosCreados.Count,
                Contratos = contratosCreados.Select(c => new
                {
                    c.Id,
                    c.NumeroContrato,
                    c.NombreDocente,
                    c.MontoTotal
                }).ToList()
            });
        }


        [NonAction]
        private ValidacionFinalizarResponse ValidarProcesoInterno(int procesoId, AsigProceso proceso)
        {
            var response = new ValidacionFinalizarResponse
            {
                IsValid = true,
                Errors = new List<string>(),
                AsignacionesSinContrato = new List<AsignacionSinContrato>(),
                ContratosDuplicados = new List<ContratoDuplicado>()
            };

            // 1) Get all assignments for this proceso
            var asignaciones = _context.AsignacionesCarga
                .Where(a => a.AsigProcesoId == procesoId)
                .ToList();

            if (!asignaciones.Any())
            {
                response.IsValid = false;
                response.Errors.Add("No hay asignaciones en este proceso.");
                return response;
            }

            // 2) Check all assignments have a contract number
            var sinContrato = asignaciones
                .Where(a => string.IsNullOrWhiteSpace(a.NumeroContrato))
                .Select(a => new AsignacionSinContrato
                {
                    Id = a.Id,
                    CiDocente = a.CiDocente,
                    NombreCompleto = string.Join(" ",
                        new[] { a.PrimerApellido, a.SegundoApellido, a.TercerApellido, a.Nombres }
                        .Where(s => !string.IsNullOrWhiteSpace(s))),
                    Sigla = a.Sigla,
                    Paralelo = a.Paralelo
                })
                .ToList();

            if (sinContrato.Any())
            {
                response.IsValid = false;
                response.AsignacionesSinContrato = sinContrato;
                response.Errors.Add(string.Format("Hay {0} asignación(es) sin número de contrato.", sinContrato.Count));
            }

            // 3) Get unique contract numbers in this batch
            var numerosContratoActual = asignaciones
                .Where(a => !string.IsNullOrWhiteSpace(a.NumeroContrato))
                .Select(a => a.NumeroContrato.Trim())
                .Distinct()
                .ToList();

            // 4) Check for duplicates in AsigContratos table (same Sede + Periodo)
            var duplicados = new List<ContratoDuplicado>();

            foreach (var numeroContrato in numerosContratoActual)
            {
                var contratoExistente = _context.AsigContratos
                    .Where(c => c.NumeroContrato == numeroContrato
                             && c.BranchesId == proceso.BranchesId
                             && c.PeriodoId == proceso.PeriodoId)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefault();

                if (contratoExistente != null)
                {
                    duplicados.Add(new ContratoDuplicado
                    {
                        NumeroContrato = numeroContrato,
                        ContratoIdExistente = contratoExistente.Id,
                        FechaExistente = contratoExistente.CreatedAt ?? DateTime.Now
                    });
                }
            }

            if (duplicados.Any())
            {
                response.IsValid = false;
                response.ContratosDuplicados = duplicados;
                response.Errors.Add(string.Format("Hay {0} número(s) de contrato duplicado(s) en la misma sede y período.", duplicados.Count));
            }

            return response;
        }

        // DTOs (keep these at the class level)
        public class ValidacionFinalizarResponse
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; }
            public List<AsignacionSinContrato> AsignacionesSinContrato { get; set; }
            public List<ContratoDuplicado> ContratosDuplicados { get; set; }
        }

        public class AsignacionSinContrato
        {
            public int Id { get; set; }
            public string CiDocente { get; set; }
            public string NombreCompleto { get; set; }
            public string Sigla { get; set; }
            public string Paralelo { get; set; }
        }

        public class ContratoDuplicado
        {
            public string NumeroContrato { get; set; }
            public int ContratoIdExistente { get; set; }
            public DateTime FechaExistente { get; set; }
        }



        [HttpGet]
        [Route("Periodos")]
        public IHttpActionResult GetPeriodos()
        {
            // Query you provided
            var sql = "SELECT PERIODOSAP FROM ADMNAL.T_REG_PARALELOS_NS GROUP BY PERIODOSAP;";

            var periodosRaw = _context.Database
                .SqlQuery<string>(sql)
                .ToList();

            // Sort by year (last 4 digits) descending, then by the prefix
            var periodosSorted = periodosRaw
                .Where(p => !string.IsNullOrWhiteSpace(p) && p.Length >= 4)
                .OrderByDescending(p => {
                    // Extract last 4 characters (year)
                    var year = p.Substring(p.Length - 4);
                    int yearNum;
                    if (int.TryParse(year, out yearNum))
                        return yearNum;
                    return 0; // If not a valid year, put at bottom
                })
                .ThenBy(p => {
                    // Secondary sort by prefix (1S, 2S, A, V, etc.)
                    // This ensures: 2S2025, 1S2025, V2025, A2025 (alphabetical by prefix)
                    if (p.Length > 4)
                        return p.Substring(0, p.Length - 4);
                    return p;
                })
                .Select((p, index) => new
                {
                    Id = index + 1,
                    Name = p,
                    Value = p
                })
                .ToList();

            return Ok(periodosSorted);
        }

        public class DeleteProcesoRequest
        {
            public int ProcesoId { get; set; }
        }

        [HttpPost]
        [Route("DeleteProceso")]
        public IHttpActionResult DeleteProceso([FromBody] DeleteProcesoRequest model)
        {
            if (model == null || model.ProcesoId <= 0)
                return BadRequest("ID de proceso inválido");

            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var proceso = _context.AsigProcesos.FirstOrDefault(p => p.Id == model.ProcesoId);
            if (proceso == null)
                return NotFound();

            // Validate permissions
            var procesosUser = auth
                .filerByRegional(_context.AsigProcesos, user)
                .OfType<AsigProceso>();

            /* if (!procesosUser.Any(p => p.Id == model.ProcesoId))
                 return Unauthorized();*/

            // Check if already finalized
            if (proceso.State == "FINALIZADO")
            {
                return Content(
                    HttpStatusCode.BadRequest,
                    new { Message = "No se puede eliminar un proceso finalizado." }
                );
            }

            // Delete associated asignaciones first
            var asignaciones = _context.AsignacionesCarga
                .Where(a => a.AsigProcesoId == model.ProcesoId)
                .ToList();

            if (asignaciones.Any())
            {
                _context.AsignacionesCarga.RemoveRange(asignaciones);
            }

            // Delete the proceso
            _context.AsigProcesos.Remove(proceso);
            _context.SaveChanges();

            return Ok(new
            {
                Message = "Proceso eliminado correctamente",
                ProcesoId = model.ProcesoId,
                AsignacionesEliminadas = asignaciones.Count
            });
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
                Id = AsigProceso.GetNextId(_context),
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