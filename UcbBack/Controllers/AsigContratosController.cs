using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Web.Http;
using System.Configuration;
using UcbBack.Logic;

using UcbBack.Logic.B1;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using UcbBack.Models;
using UcbBack.Models.Auth;

namespace UcbBack.Controllers
{
    [RoutePrefix("api/AsigContratos")]
    public class AsigContratosController : ApiController
    {
        private readonly ApplicationDbContext _context;
        private readonly ValidateAuth auth;

        public AsigContratosController()
        {
            _context = new ApplicationDbContext();
            auth = new ValidateAuth();
        }


        //  DTOs
        public class ContratoListItem
        {
            public int Id { get; set; }
            public string NumeroContrato { get; set; }
            public string NombreDocente { get; set; }
            public int BranchesId { get; set; }
            public string SedeAbr { get; set; }
            public string SedeNombre { get; set; }
            public string PeriodoId { get; set; }
            public decimal MontoTotal { get; set; }
            public string Estado { get; set; }
            public string Observaciones { get; set; }
            public DateTime? CreatedAt { get; set; }
            public int? AsigProcesoId { get; set; }
            public int TotalAsignaciones { get; set; }
        }

        public class ContratoDetalleResponse
        {
            public ContratoInfo Contrato { get; set; }
            public List<AsignacionInfo> Asignaciones { get; set; }
        }

        public class ContratoInfo
        {
            public int Id { get; set; }
            public string NumeroContrato { get; set; }
            public string NombreDocente { get; set; }
            public int BranchesId { get; set; }
            public string SedeAbr { get; set; }
            public string SedeNombre { get; set; }
            public string PeriodoId { get; set; }
            public decimal MontoTotal { get; set; }
            public string Estado { get; set; }
            public string Observaciones { get; set; }
            public DateTime? CreatedAt { get; set; }
            public int? AsigProcesoId { get; set; }
        }

        public class AsignacionInfo
        {
            public int Id { get; set; }
            public string CiDocente { get; set; }
            public string NombreCompleto { get; set; }
            public string Sigla { get; set; }
            public string CodigoParalelo { get; set; }
            public string Paralelo { get; set; }
            public decimal HorasSemana { get; set; }
            public decimal HorasMes { get; set; }
            public decimal CostoHora { get; set; }
            public decimal MontoTotal { get; set; }
            public int CantidadMeses { get; set; }
            public string Sede { get; set; }
            public string UnidadOrganizacional { get; set; }
            public string NombreMateria { get; set; }
            public List<PagoProgramadoInfo> PagosProgramados { get; set; }
        }

        public class PagoProgramadoInfo
        {
            public int Id { get; set; }
            public int MesPago { get; set; }
            public int AnioPago { get; set; }
            public decimal Monto { get; set; }
            public decimal? MontoOriginal { get; set; }
            public decimal? Porcentaje { get; set; }
            public string Estado { get; set; }
            public bool EsExcepcion { get; set; }
            public string TipoDocente { get; set; }
            public string Observaciones { get; set; }
        }

        public class UpdateEstadoRequest
        {
            public int Id { get; set; }
            public string Estado { get; set; }
            public string Observaciones { get; set; }
        }

        //  1) Get All Contracts (with filters)

        [HttpGet]
        [Route("GetContratos")]
        public IHttpActionResult GetContratos(
    int? branchesId = null,
    string periodoId = null,
    string estado = null,
    string search = null,
    int page = 1,
    int pageSize = 20)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var query = from c in _context.AsigContratos
                        join b in _context.Branch on c.BranchesId equals b.Id
                        select new
                        {
                            c.Id,
                            c.NumeroContrato,
                            c.NombreDocente,
                            c.BranchesId,
                            SedeAbr = b.Abr,
                            SedeNombre = b.Name,
                            c.PeriodoId,
                            c.MontoTotal,
                            c.Estado,
                            c.Observaciones,
                            c.CreatedAt,
                            c.AsigProcesoId
                        };

            // =========================
            // FILTERS
            // =========================

            if (branchesId.HasValue)
            {
                query = query.Where(x =>
                    x.BranchesId == branchesId.Value);
            }

            if (!string.IsNullOrWhiteSpace(periodoId))
            {
                query = query.Where(x =>
                    x.PeriodoId == periodoId);
            }

            if (!string.IsNullOrWhiteSpace(estado))
            {
                query = query.Where(x =>
                    x.Estado == estado);
            }

            // =========================
            // SERVER-SIDE SEARCH
            // =========================

           

            // =========================
            // REGIONAL FILTER
            // =========================

            var filtrado = auth
                .filerByRegional(query.AsQueryable(), user)
                .ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerms = search
                    .Trim()
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                filtrado = filtrado
                    .Where(x =>
                    {
                        var numeroContrato = (string)x.NumeroContrato ?? "";
                        var nombreDocente = (string)x.NombreDocente ?? "";
                        var sedeNombre = (string)x.SedeNombre ?? "";
                        var sedeAbr = (string)x.SedeAbr ?? "";
                        var periodo = (string)x.PeriodoId ?? "";

                        return searchTerms.All(term =>
                            numeroContrato.IndexOf(
                                term,
                                StringComparison.OrdinalIgnoreCase
                            ) >= 0
                            ||
                            nombreDocente.IndexOf(
                                term,
                                StringComparison.OrdinalIgnoreCase
                            ) >= 0
                            ||
                            sedeNombre.IndexOf(
                                term,
                                StringComparison.OrdinalIgnoreCase
                            ) >= 0
                            ||
                            sedeAbr.IndexOf(
                                term,
                                StringComparison.OrdinalIgnoreCase
                            ) >= 0
                            ||
                            periodo.IndexOf(
                                term,
                                StringComparison.OrdinalIgnoreCase
                            ) >= 0
                        );
                    })
                    .ToList();
            }

            // =========================
            // TOTALS BEFORE PAGINATION
            // =========================

            // Total records
            var total = filtrado.Count;

            // Total amount of ALL filtered contracts
            var montoTotalGeneral = filtrado
                .Select(x => (decimal)x.MontoTotal)
                .DefaultIfEmpty(0m)
                .Sum();

            // Pagination
            var contratos = filtrado
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // =========================
            // ASSIGNMENT COUNTS
            // =========================

            var result = contratos.Select(c => new ContratoListItem
            {
                Id = c.Id,
                NumeroContrato = c.NumeroContrato,
                NombreDocente = c.NombreDocente,
                MontoTotal = c.MontoTotal,
                Estado = c.Estado,
                Observaciones = c.Observaciones,
                CreatedAt = c.CreatedAt,
                AsigProcesoId = c.AsigProcesoId,

                TotalAsignaciones = GetAsignacionesCount(
                    c.NumeroContrato,
                    c.BranchesId,
                    c.PeriodoId
                )
            }).ToList();

            return Ok(new
            {
                Items = result,
                Total = total,
                Page = page,
                PageSize = pageSize,

                TotalPages = (int)Math.Ceiling(
                    (double)total / pageSize
                ),

                MontoTotalGeneral = montoTotalGeneral
            });
        }

        // Helper method to get assignment count
        [NonAction]
        private int GetAsignacionesCount(string numeroContrato, int branchesId, string periodoId)
        {
            var sql = "SELECT COUNT(*) " +
                      "FROM \"" + CustomSchema.Schema + "\".\"AsignacionCarga\" a " +
                      "INNER JOIN \"" + CustomSchema.Schema + "\".\"Asig_Proceso\" p ON a.\"AsigProcesoId\" = p.\"Id\" " +
                      "WHERE a.\"NumeroContrato\" = :numeroContrato " +
                      "  AND p.\"BranchesId\" = :branchesId " +
                      "  AND p.\"PeriodoId\" = :periodoId " +
                      "  AND p.\"State\" = 'FINALIZADO'";

            try
            {
                var count = _context.Database.SqlQuery<int>(sql,
                    new Sap.Data.Hana.HanaParameter("numeroContrato", numeroContrato),
                    new Sap.Data.Hana.HanaParameter("branchesId", branchesId),
                    new Sap.Data.Hana.HanaParameter("periodoId", periodoId ?? "")
                ).FirstOrDefault();

                return count;
            }
            catch
            {
                return 0;
            }
        }

        //  2) Get Contract Detail (with assignments)
        [HttpGet]
        [Route("GetDetalle/{contratoId}")]
        public IHttpActionResult GetDetalle(int contratoId)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var contrato = _context.AsigContratos.FirstOrDefault(c => c.Id == contratoId);
            if (contrato == null)
                return NotFound();

            // Validate permissions
            var contratosUser = auth
                .filerByRegional(_context.AsigContratos, user)
                .OfType<AsigContrato>();

            // Get branch info
            var branch = _context.Branch.FirstOrDefault(b => b.Id == contrato.BranchesId);

            // Get all assignments for this contract (only from finalized processes)
            var asignaciones = _context.AsignacionesCarga
                .Where(a => a.NumeroContrato == contrato.NumeroContrato
                         && a.AsigProceso.BranchesId == contrato.BranchesId
                         && a.AsigProceso.PeriodoId == contrato.PeriodoId
                         && a.AsigProceso.State == "FINALIZADO")
                .ToList();

            var response = new ContratoDetalleResponse
            {
                Contrato = new ContratoInfo
                {
                    Id = contrato.Id,
                    NumeroContrato = contrato.NumeroContrato,
                    NombreDocente = contrato.NombreDocente,
                    BranchesId = contrato.BranchesId,
                    SedeAbr = branch != null ? branch.Abr : "",
                    SedeNombre = branch != null ? branch.Name : "",
                    PeriodoId = contrato.PeriodoId,
                    MontoTotal = contrato.MontoTotal,
                    Estado = contrato.Estado,
                    Observaciones = contrato.Observaciones,
                    CreatedAt = contrato.CreatedAt,
                    AsigProcesoId = contrato.AsigProcesoId
                },
                Asignaciones = asignaciones.Select(a => new AsignacionInfo
                {
                    Id = a.Id,
                    CiDocente = a.CiDocente,
                    NombreCompleto = string.Join(" ",
                        new[] { a.PrimerApellido, a.SegundoApellido, a.TercerApellido, a.Nombres }
                        .Where(s => !string.IsNullOrWhiteSpace(s))),
                    Sigla = a.Sigla,
                    CodigoParalelo = a.CodigoParalelo,
                    Paralelo = a.Paralelo,
                    HorasSemana = a.HorasSemana,
                    HorasMes = a.HorasMes,
                    CostoHora = a.CostoHora,
                    CantidadMeses = a.CantidadMeses,
                    MontoTotal = a.HorasMes * a.CostoHora * a.CantidadMeses,
                    Sede = a.Sede,
                    UnidadOrganizacional = a.UnidadOrganizacional,
                    NombreMateria = GetNombreMateria(a.CodigoParalelo),
                    PagosProgramados = _context.PagosProgramados
                        .Where(p => p.AsignacionCargaId == a.Id)
                        .OrderBy(p => p.AnioPago)
                        .ThenBy(p => p.MesPago)
                        .Select(p => new PagoProgramadoInfo
                        {
                            Id = p.Id,
                            MesPago = p.MesPago,
                            AnioPago = p.AnioPago,
                            Monto = p.Monto,
                            MontoOriginal = p.MontoOriginal,
                            Porcentaje = p.Porcentaje,
                            Estado = p.Estado,
                            EsExcepcion = p.EsExcepcion,
                            TipoDocente = p.TipoDocente,
                            Observaciones = p.Observaciones
                        }).ToList()
                }).ToList()
            };

            return Ok(response);
        }

        [NonAction]
        private string GetNombreMateria(string codigoParalelo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(codigoParalelo))
                    return "";

                var sql = "SELECT NOMBREMATERIA FROM ADMNAL.T_REG_PARALELOS_NS " +
                          "WHERE CODIGOSAP = :codigo";

                var result = _context.Database.SqlQuery<string>(sql,
                    new Sap.Data.Hana.HanaParameter("codigo", codigoParalelo.Trim())
                ).FirstOrDefault();

                return result ?? "";
            }
            catch
            {
                return "";
            }
        }


        //  3) Update Contract State/Observaciones
        [HttpPost]
        [Route("UpdateEstado")]
        public IHttpActionResult UpdateEstado([FromBody] UpdateEstadoRequest model)
        {
            if (model == null)
                return BadRequest("Datos inválidos");

            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var contrato = _context.AsigContratos.FirstOrDefault(c => c.Id == model.Id);
            if (contrato == null)
                return NotFound();

            // Validate permissions
            var contratosUser = auth
                .filerByRegional(_context.AsigContratos, user)
                .OfType<AsigContrato>();

            if (!contratosUser.Any(c => c.Id == model.Id))
                return Unauthorized();

            // Validate estado
            var estadosValidos = new[] { "PENDIENTE", "APROBADO", "PAGADO", "CANCELADO" };
            if (!string.IsNullOrWhiteSpace(model.Estado) && !estadosValidos.Contains(model.Estado.ToUpper()))
            {
                return BadRequest("Estado inválido. Valores permitidos: PENDIENTE, APROBADO, PAGADO, CANCELADO");
            }

            // Update
            if (!string.IsNullOrWhiteSpace(model.Estado))
                contrato.Estado = model.Estado.ToUpper();

            contrato.Observaciones = model.Observaciones;
            contrato.UpdatedAt = DateTime.Now;
            contrato.UpdatedBy = user.Id;

            _context.SaveChanges();

            return Ok(new
            {
                Message = "Contrato actualizado correctamente",
                Contrato = new
                {
                    contrato.Id,
                    contrato.NumeroContrato,
                    contrato.Estado,
                    contrato.Observaciones
                }
            });
        }

        // ---------------------------
        //  4) Get Summary Statistics
        // ---------------------------
        [HttpGet]
        [Route("GetEstadisticas")]
        public IHttpActionResult GetEstadisticas(int? branchesId = null, string periodoId = null)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var query = _context.AsigContratos.AsQueryable();

            // Apply filters
            if (branchesId.HasValue)
                query = query.Where(c => c.BranchesId == branchesId.Value);

            if (!string.IsNullOrWhiteSpace(periodoId))
                query = query.Where(c => c.PeriodoId == periodoId);

            // Apply regional filtering
            var filtrado = auth.filerByRegional(query, user).OfType<AsigContrato>();

            var estadisticas = new
            {
                TotalContratos = filtrado.Count(),
                MontoTotalGeneral = filtrado.Sum(c => (decimal?)c.MontoTotal) ?? 0,
                PorEstado = filtrado
                    .GroupBy(c => c.Estado)
                    .Select(g => new
                    {
                        Estado = g.Key,
                        Cantidad = g.Count(),
                        MontoTotal = g.Sum(c => c.MontoTotal)
                    })
                    .ToList()
            };

            return Ok(estadisticas);
        }

        // ---------------------------
        //  5) Search Contracts
        // ---------------------------
        [HttpGet]
        [Route("Buscar")]
        public IHttpActionResult Buscar(string query)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Query de búsqueda requerido");

            var searchQuery = query.Trim().ToUpper();

            var contratos = from c in _context.AsigContratos
                            join b in _context.Branch on c.BranchesId equals b.Id
                            where c.NumeroContrato.ToUpper().Contains(searchQuery) ||
                                  b.Name.ToUpper().Contains(searchQuery) ||
                                  b.Abr.ToUpper().Contains(searchQuery) ||
                                  c.PeriodoId.ToUpper().Contains(searchQuery)
                            select new
                            {
                                c.Id,
                                c.NumeroContrato,
                                SedeNombre = b.Name,
                                SedeAbr = b.Abr,
                                c.PeriodoId,
                                c.MontoTotal,
                                c.Estado
                            };

            var filtrado = auth.filerByRegional(contratos.AsQueryable(), user);

            var resultado = filtrado.Take(50).ToList();

            return Ok(resultado);
        }
    }
}