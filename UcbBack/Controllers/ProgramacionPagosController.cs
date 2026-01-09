using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using UcbBack.Logic;
using UcbBack.Models;
using UcbBack.Models.Auth;

namespace UcbBack.Controllers
{
    [RoutePrefix("api/ProgramacionPagos")]
    public class ProgramacionPagosController : ApiController
    {
        private readonly ApplicationDbContext _context;
        private readonly ValidateAuth auth;

        public ProgramacionPagosController()
        {
            _context = new ApplicationDbContext();
            auth = new ValidateAuth();
        }

        // ---------------------------
        //  DTOs
        // ---------------------------
        public class CrearProgramacionRequest
        {
            public int BranchesId { get; set; }
            public string PeriodoId { get; set; }
            public string NombrePlantilla { get; set; }
            public string Descripcion { get; set; }
            public bool EsPlantilla { get; set; }
            public List<MesPagoDto> Meses { get; set; }
        }

        public class MesPagoDto
        {
            public int Mes { get; set; }  // 1-12
            public int Anio { get; set; }
            public int Orden { get; set; }
            public decimal PorcentajePorDefecto { get; set; }
            public string Descripcion { get; set; }
        }

        public class AsignacionParaProgramarDto
        {
            public int Id { get; set; }
            public string CiDocente { get; set; }
            public string NombreCompleto { get; set; }
            public string Sigla { get; set; }
            public string Paralelo { get; set; }
            public string NumeroContrato { get; set; }
            public decimal MontoTotal { get; set; }
            public bool TienePagosProgramados { get; set; }
            public List<PagoCalculadoDto> PagosCalculados { get; set; }
        }

        public class PagoCalculadoDto
        {
            public int Mes { get; set; }
            public int Anio { get; set; }
            public string MesNombre { get; set; }
            public decimal Monto { get; set; }
            public decimal Porcentaje { get; set; }
            public int Orden { get; set; }
        }

        public class GenerarPagosAsignacionesRequest
        {
            public int ProgramacionId { get; set; }
            public List<int> AsignacionesIds { get; set; }  // Can exclude some
            public List<ExcepcionAsignacion> Excepciones { get; set; }
        }

        public class ExcepcionAsignacion
        {
            public int AsignacionId { get; set; }
            public List<PagoCustomDto> PagosCustom { get; set; }
        }

        public class PagoCustomDto
        {
            public int Mes { get; set; }
            public int Anio { get; set; }
            public decimal Monto { get; set; }
        }

        // ---------------------------
        //  1) Get Assignments to Program
        // ---------------------------
        [HttpGet]
        [Route("GetAsignacionesParaProgramar")]
        public IHttpActionResult GetAsignacionesParaProgramar(int branchesId, string periodoId)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            // Get finalized processes for this branch + period
            var procesosIds = _context.AsigProcesos
                .Where(p => p.BranchesId == branchesId
                         && p.PeriodoId == periodoId
                         && p.State == "FINALIZADO")
                .Select(p => p.Id)
                .ToList();

            if (!procesosIds.Any())
                return Ok(new { Asignaciones = new List<AsignacionParaProgramarDto>(), Total = 0, MontoTotal = 0 });

            // Get assignments with contract numbers
            var asignaciones = _context.AsignacionesCarga
                .Where(a => procesosIds.Contains(a.AsigProcesoId)
                         && a.NumeroContrato != null
                         && a.NumeroContrato != "")
                .ToList();  // MATERIALIZE HERE

            if (!asignaciones.Any())
                return Ok(new { Asignaciones = new List<AsignacionParaProgramarDto>(), Total = 0, MontoTotal = 0 });

            // Get all contracts for this branch + period (ONCE, not in loop)
            var contratosValidos = _context.AsigContratos
                .Where(c => c.BranchesId == branchesId && c.PeriodoId == periodoId)
                .Select(c => c.NumeroContrato)
                .ToList();  // MATERIALIZE HERE

            // Filter assignments with valid contracts (IN MEMORY)
            var asignacionesValidas = asignaciones
                .Where(a => contratosValidos.Contains(a.NumeroContrato))
                .ToList();

            if (!asignacionesValidas.Any())
                return Ok(new { Asignaciones = new List<AsignacionParaProgramarDto>(), Total = 0, MontoTotal = 0 });

            // Check which have programmed payments (ONCE, not in loop)
            var asignacionesIds = asignacionesValidas.Select(a => a.Id).ToList();
            var asignacionesConPagos = _context.PagosProgramados
                .Where(p => asignacionesIds.Contains(p.AsignacionCargaId))
                .Select(p => p.AsignacionCargaId)
                .Distinct()
                .ToList();  // MATERIALIZE HERE

            // Build response (ALL IN MEMORY)
            var resultado = asignacionesValidas.Select(a => new AsignacionParaProgramarDto
            {
                Id = a.Id,
                CiDocente = a.CiDocente,
                NombreCompleto = string.Join(" ",
                    new[] { a.PrimerApellido, a.SegundoApellido, a.TercerApellido, a.Nombres }
                    .Where(s => !string.IsNullOrWhiteSpace(s))),
                Sigla = a.Sigla,
                Paralelo = a.Paralelo,
                NumeroContrato = a.NumeroContrato,
                MontoTotal = a.HorasMes * a.CostoHora * a.CantidadMeses,
                TienePagosProgramados = asignacionesConPagos.Contains(a.Id),
                PagosCalculados = new List<PagoCalculadoDto>()
            }).ToList();

            var montoTotal = resultado.Sum(a => a.MontoTotal);

            return Ok(new
            {
                Asignaciones = resultado,
                Total = resultado.Count,
                MontoTotal = montoTotal
            });
        }

        // ---------------------------
        //  2) Create Schedule (same as before but for months)
        // ---------------------------
        [HttpPost]
        [Route("CrearProgramacion")]
        public IHttpActionResult CrearProgramacion([FromBody] CrearProgramacionRequest model)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            if (model == null || model.Meses == null || !model.Meses.Any())
                return BadRequest("Debe especificar al menos un mes de pago");

            var programacionExistente = _context.ProgramacionPagos
                .FirstOrDefault(p => p.BranchesId == model.BranchesId && p.PeriodoId == model.PeriodoId);

            ProgramacionPago programacion;

            if (programacionExistente != null)
            {
                programacion = programacionExistente;
                programacion.NombrePlantilla = model.NombrePlantilla;
                programacion.Descripcion = model.Descripcion;
                programacion.EsPlantilla = model.EsPlantilla;
                programacion.UpdatedAt = DateTime.Now;
                programacion.UpdatedBy = user.Id;

                var fechasViejas = _context.FechasPago
                    .Where(f => f.ProgramacionPagosId == programacion.Id)
                    .ToList();
                _context.FechasPago.RemoveRange(fechasViejas);
            }
            else
            {
                programacion = new ProgramacionPago
                {
                    BranchesId = model.BranchesId,
                    PeriodoId = model.PeriodoId,
                    NombrePlantilla = model.NombrePlantilla,
                    Descripcion = model.Descripcion,
                    Estado = "BORRADOR",
                    EsPlantilla = model.EsPlantilla,
                    TotalContratos = 0,
                    MontoTotal = 0,
                    CreatedAt = DateTime.Now,
                    CreatedBy = user.Id
                };

                _context.ProgramacionPagos.Add(programacion);
            }

            _context.SaveChanges();

            foreach (var mesDto in model.Meses)
            {
                var lastDayOfMonth = new DateTime(mesDto.Anio, mesDto.Mes,
                    DateTime.DaysInMonth(mesDto.Anio, mesDto.Mes));

                var fecha = new FechaPago
                {
                    ProgramacionPagosId = programacion.Id,
                    FechaPagos = lastDayOfMonth,
                    Orden = mesDto.Orden,
                    PorcentajePorDefecto = mesDto.PorcentajePorDefecto,
                    Descripcion = mesDto.Descripcion,
                    Mes = mesDto.Mes,
                    Anio = mesDto.Anio
                };
                _context.FechasPago.Add(fecha);
            }

            _context.SaveChanges();

            return Ok(new
            {
                Message = programacionExistente != null ? "Programación actualizada correctamente" : "Programación creada correctamente",
                ProgramacionId = programacion.Id
            });
        }

        // ---------------------------
        //  3) Generate Payments for Assignments
        // ---------------------------
        [HttpPost]
        [Route("GenerarPagosAsignaciones")]
        public IHttpActionResult GenerarPagosAsignaciones([FromBody] GenerarPagosAsignacionesRequest model)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var programacion = _context.ProgramacionPagos.FirstOrDefault(p => p.Id == model.ProgramacionId);
            if (programacion == null)
                return NotFound();

            var meses = _context.FechasPago
                .Where(f => f.ProgramacionPagosId == programacion.Id)
                .OrderBy(f => f.Orden)
                .ToList();

            if (!meses.Any())
                return BadRequest("No hay meses de pago configurados");

            // Get assignments
            var asignaciones = _context.AsignacionesCarga
                .Where(a => model.AsignacionesIds.Contains(a.Id))
                .ToList();

            if (!asignaciones.Any())
                return BadRequest("No hay asignaciones para programar");

            // Delete existing payments for these assignments
            var pagosExistentes = _context.PagosProgramados
                .Where(p => model.AsignacionesIds.Contains(p.AsignacionCargaId))
                .ToList();

            _context.PagosProgramados.RemoveRange(pagosExistentes);
            _context.SaveChanges();

            int totalPagosGenerados = 0;

            // Build exception dictionary
            var excepcionesDic = model.Excepciones != null
                ? model.Excepciones.ToDictionary(e => e.AsignacionId, e => e.PagosCustom)
                : new Dictionary<int, List<PagoCustomDto>>();

            foreach (var asignacion in asignaciones)
            {
                var montoTotal = asignacion.HorasMes * asignacion.CostoHora * asignacion.CantidadMeses;

                // Check if exception
                if (excepcionesDic.ContainsKey(asignacion.Id))
                {
                    // Custom payments
                    var pagosCustom = excepcionesDic[asignacion.Id];
                    foreach (var pagoCustom in pagosCustom)
                    {
                        var pago = new PagoProgramado
                        {
                            AsignacionCargaId = asignacion.Id,
                            FechaPagoId = null,
                            MesPago = pagoCustom.Mes,
                            AnioPago = pagoCustom.Anio,
                            Monto = pagoCustom.Monto,
                            Porcentaje = (pagoCustom.Monto / montoTotal) * 100,
                            Estado = "PROGRAMADO",
                            EsExcepcion = true,
                            CreatedAt = DateTime.Now,
                            CreatedBy = user.Id
                        };

                        _context.PagosProgramados.Add(pago);
                        totalPagosGenerados++;
                    }
                }
                else
                {
                    // Standard payments - calculate with 2 decimals precision
                    var pagos = CalculatePrecisePayments(montoTotal, meses.Count);

                    for (int i = 0; i < meses.Count; i++)
                    {
                        var mes = meses[i];
                        var pago = new PagoProgramado
                        {
                            AsignacionCargaId = asignacion.Id,
                            FechaPagoId = mes.Id,
                            MesPago = mes.Mes.HasValue ? mes.Mes.Value : mes.FechaPagos.Month,
                            AnioPago = mes.Anio.HasValue ? mes.Anio.Value : mes.FechaPagos.Year,
                            Monto = pagos[i],
                            Porcentaje = mes.PorcentajePorDefecto,
                            Estado = "PROGRAMADO",
                            EsExcepcion = false,
                            CreatedAt = DateTime.Now,
                            CreatedBy = user.Id
                        };

                        _context.PagosProgramados.Add(pago);
                        totalPagosGenerados++;
                    }
                }
            }

            programacion.Estado = "PROGRAMADO";
            programacion.TotalContratos = asignaciones.Count;
            programacion.MontoTotal = asignaciones.Sum(a => a.HorasMes * a.CostoHora * a.CantidadMeses);
            programacion.UpdatedAt = DateTime.Now;
            programacion.UpdatedBy = user.Id;

            _context.SaveChanges();

            return Ok(new
            {
                Message = "Pagos generados correctamente",
                TotalPagosGenerados = totalPagosGenerados,
                TotalAsignaciones = asignaciones.Count,
                TotalMeses = meses.Count
            });
        }

        // ---------------------------
        //  Helper: Calculate Precise Payments (2 decimals)
        // ---------------------------
        [NonAction]
        private List<decimal> CalculatePrecisePayments(decimal totalAmount, int numberOfPayments)
        {
            var payments = new List<decimal>();

            // Calculate per-payment amount (floor to 2 decimals)
            var perPayment = Math.Floor((totalAmount / numberOfPayments) * 100) / 100;

            // Calculate remainder
            var totalDistributed = perPayment * numberOfPayments;
            var remainder = totalAmount - totalDistributed;

            // Distribute
            for (int i = 0; i < numberOfPayments; i++)
            {
                if (i == numberOfPayments - 1)
                {
                    // Last payment gets remainder
                    payments.Add(perPayment + remainder);
                }
                else
                {
                    payments.Add(perPayment);
                }
            }

            return payments;
        }

        //  4) Get Programmed Payments
        [HttpGet]
        [Route("GetPagosProgramados")]
        public IHttpActionResult GetPagosProgramados(int branchId, string periodoId, int mes, int anio)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            // Get all programmed payments for the specified filters
            var pagosQuery = from p in _context.PagosProgramados
                             join a in _context.AsignacionesCarga on p.AsignacionCargaId equals a.Id
                             join proc in _context.AsigProcesos on a.AsigProcesoId equals proc.Id
                             join ou in _context.OrganizationalUnits on a.UnidadOrganizacional equals ou.Cod into ouLeft
                             from ou in ouLeft.DefaultIfEmpty()  // LEFT JOIN
                             where proc.BranchesId == branchId
                                && proc.PeriodoId == periodoId
                                && p.MesPago == mes
                                && p.AnioPago == anio
                             select new
                             {
                                 // Payment info
                                 PagoId = p.Id,
                                 p.AsignacionCargaId,
                                 p.MesPago,
                                 p.AnioPago,
                                 p.Monto,
                                 p.Porcentaje,
                                 p.Estado,
                                 p.EsExcepcion,

                                 // Assignment info
                                 a.CiDocente,
                                 a.PrimerApellido,
                                 a.SegundoApellido,
                                 a.TercerApellido,
                                 a.Nombres,
                                 a.Sigla,
                                 a.Paralelo,
                                 a.NumeroContrato,

                                 // NEW: Organizational Unit info
                                 CodUnidadOrganizacional = a.UnidadOrganizacional,
                                 NombreUnidadOrganizacional = ou != null ? ou.Name : "",

                                 // Process info
                                 proc.BranchesId
                             };

            // Apply regional filtering
            var filteredPagos = auth.filerByRegional(pagosQuery.AsQueryable(), user);

            // Materialize
            var pagosList = filteredPagos.ToList();

            // Build response with NombreCompleto
            var result = pagosList.Select(p => new
            {
                PagoId = p.PagoId,
                AsignacionCargaId = p.AsignacionCargaId,
                MesPago = p.MesPago,
                AnioPago = p.AnioPago,
                Monto = p.Monto,
                Porcentaje = p.Porcentaje,
                Estado = p.Estado ?? "PENDIENTE",
                EsExcepcion = p.EsExcepcion,

                CiDocente = p.CiDocente,
                NombreCompleto = string.Join(" ", new[] {
            p.PrimerApellido,
            p.SegundoApellido,
            p.TercerApellido,
            p.Nombres
        }.Where(s => !string.IsNullOrWhiteSpace(s))),
                Sigla = p.Sigla,
                Paralelo = p.Paralelo,
                NumeroContrato = p.NumeroContrato,

                // NEW: Organizational Unit fields
                CodUnidadOrganizacional = p.CodUnidadOrganizacional ?? "",
                UnidadOrganizacional = p.NombreUnidadOrganizacional ?? ""
            }).ToList();

            return Ok(result);
        }


        //  Helper: Get Month Name

        [NonAction]
        private string GetMonthName(int mes)
        { 
            var meses = new[] { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                               "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
            return mes >= 1 && mes <= 12 ? meses[mes] : "";
        }

    /*
        [Route("GetPlantillas")]
        public IHttpActionResult GetPlantillas()
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var plantillas = _context.ProgramacionPagos
                .Where(p => p.EsPlantilla == true)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.NombrePlantilla,
                    p.Descripcion,
                    TotalMeses = _context.FechasPago.Count(f => f.ProgramacionPagosId == p.Id)
                })
                .ToList();

            return Ok(plantillas);
        }*/

        //  6) Get Existing Schedule
        [HttpGet]
        [Route("GetProgramacion")]
        public IHttpActionResult GetProgramacion(int branchesId, string periodoId)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var programacion = _context.ProgramacionPagos
                .FirstOrDefault(p => p.BranchesId == branchesId && p.PeriodoId == periodoId);

            if (programacion == null)
                return NotFound();

            var branch = _context.Branch.FirstOrDefault(b => b.Id == branchesId);

            var meses = _context.FechasPago
                .Where(f => f.ProgramacionPagosId == programacion.Id)
                .OrderBy(f => f.Orden)
                .Select(f => new MesPagoDto
                {
                    Mes = f.Mes.HasValue ? f.Mes.Value : f.FechaPagos.Month,
                    Anio = f.Anio.HasValue ? f.Anio.Value : f.FechaPagos.Year,
                    Orden = f.Orden,
                    PorcentajePorDefecto = f.PorcentajePorDefecto,
                    Descripcion = f.Descripcion
                })
                .ToList();

            return Ok(new
            {
                programacion.Id,
                programacion.BranchesId,
                SedeNombre = branch != null ? branch.Name : "",
                programacion.PeriodoId,
                programacion.NombrePlantilla,
                programacion.Descripcion,
                programacion.Estado,
                programacion.EsPlantilla,
                programacion.TotalContratos,
                programacion.MontoTotal,
                programacion.CreatedAt,
                Meses = meses
            });
        }
    }
}