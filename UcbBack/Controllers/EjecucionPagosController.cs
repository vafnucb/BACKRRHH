using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Http;
using UcbBack.Logic;
using UcbBack.Models;


namespace UcbBack.Controllers
{
    [RoutePrefix("api/EjecucionPagos")]
    public class EjecucionPagosController : ApiController
    {
        private readonly ApplicationDbContext _context;
        private readonly ValidateAuth auth;

        public EjecucionPagosController()
        {
            _context = new ApplicationDbContext();
            auth = new ValidateAuth();
        }

        //  DTOs

        public class EnviarPagosRequest
        {
            public List<int> PagosIds { get; set; }
        }

 
        //  1) Send Payments for Approval

        [HttpPost]
        [Route("EnviarParaAprobacion")]
        public IHttpActionResult EnviarParaAprobacion([FromBody] EnviarPagosRequest model)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            if (model == null || model.PagosIds == null || !model.PagosIds.Any())
                return BadRequest("Debe seleccionar al menos un pago");

            // Get payments
            var pagos = _context.PagosProgramados
                .Where(p => model.PagosIds.Contains(p.Id))
                .ToList();

            if (!pagos.Any())
                return BadRequest("No se encontraron pagos con los IDs proporcionados");

            // Validate all have TipoDocente
            var sinTipo = pagos.Where(p => string.IsNullOrWhiteSpace(p.TipoDocente)).ToList();
            if (sinTipo.Any())
            {
                return Content(
                    HttpStatusCode.BadRequest,
                    new { Message = $"Hay {sinTipo.Count} pago(s) sin tipo de docente asignado" }
                );
            }

            // Validate none are already sent
            var yaEnviados = pagos.Where(p => p.Estado == "ENVIADO" || p.Estado == "APROBADO").ToList();
            if (yaEnviados.Any())
            {
                return Content(
                    HttpStatusCode.BadRequest,
                    new { Message = $"Hay {yaEnviados.Count} pago(s) que ya fueron enviados o aprobados" }
                );
            }

            int pagosCreados = 0;

            foreach (var pago in pagos)
            {
                // Calculate retention and contract amounts
                var porcentajeRetencion = EjecucionPago.GetPorcentajeRetencion(pago.TipoDocente);
                var montoRetencion = EjecucionPago.CalculateMontoRetencion(pago.Monto, pago.TipoDocente);
                var montoReal = EjecucionPago.CalculateMontoContrato(pago.Monto, pago.TipoDocente);


                var pagoEjecutado = new EjecucionPago
                {

                    PagoProgramadoId = pago.Id,
                    TipoDocente = pago.TipoDocente,
                    PorcentajeRetencion = porcentajeRetencion,
                    MontoRetencion = montoRetencion,
                    MontoContrato = pago.Monto,
                    MontoReal = montoReal,
                    Estado = "PENDIENTE_APROBACION",
                    FechaEnvio = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    CreatedBy = user.Id
                };

                _context.EjecucionPagos.Add(pagoEjecutado);

                // Update PagoProgramado estado
                pago.Estado = "ENVIADO";

                pagosCreados++;
            }

            _context.SaveChanges();

            return Ok(new
            {
                Message = "Pagos enviados para aprobación correctamente",
                TotalEnviados = pagosCreados
            });
        }


        //  2) Get Payments Pending Approval

        [HttpGet]
        [Route("GetPagosPendientes")]
        public IHttpActionResult GetPagosPendientes(int? branchId = null, string periodoId = null)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            // Base query
            var query = from pe in _context.EjecucionPagos
                        join pp in _context.PagosProgramados on pe.PagoProgramadoId equals pp.Id
                        join a in _context.AsignacionesCarga on pp.AsignacionCargaId equals a.Id
                        join proc in _context.AsigProcesos on a.AsigProcesoId equals proc.Id
                        where pe.Estado == "PENDIENTE_APROBACION"
                        select new
                        {
                            // EjecucionPago
                            PagoEjecutadoId = pe.Id,
                            pe.PagoProgramadoId,
                            pe.TipoDocente,
                            pe.PorcentajeRetencion,
                            pe.MontoRetencion,
                            pe.MontoContrato,
                            pe.MontoReal,
                            pe.Estado,
                            pe.FechaEnvio,

                            // PagoProgramado
                            pp.MesPago,
                            pp.AnioPago,
                            MontoBruto = pp.Monto,

                            // Assignment
                            a.CiDocente,
                            a.PrimerApellido,
                            a.SegundoApellido,
                            a.TercerApellido,
                            a.Nombres,
                            a.NumeroContrato,

                            // Process
                            proc.BranchesId,
                            proc.PeriodoId
                        };

            // Apply filters
            if (branchId.HasValue)
            {
                query = query.Where(q => q.BranchesId == branchId.Value);
            }

            if (!string.IsNullOrWhiteSpace(periodoId))
            {
                query = query.Where(q => q.PeriodoId == periodoId);
            }

            // Apply regional filtering
            var filteredQuery = auth.filerByRegional(query.AsQueryable(), user);

            // Materialize
            var pagos = filteredQuery.ToList();

            // Build response
            var result = pagos.Select(p => new
            {
                p.PagoEjecutadoId,
                p.PagoProgramadoId,
                p.TipoDocente,
                p.PorcentajeRetencion,
                p.MontoRetencion,
                p.MontoContrato,
                p.MontoReal,
                p.Estado,
                p.FechaEnvio,
                p.MesPago,
                p.AnioPago,
                p.MontoBruto,
                p.CiDocente,
                NombreCompleto = string.Join(" ", new[] {
                    p.PrimerApellido,
                    p.SegundoApellido,
                    p.TercerApellido,
                    p.Nombres
                }.Where(s => !string.IsNullOrWhiteSpace(s))),
                p.NumeroContrato,
                p.BranchesId,
                p.PeriodoId
            }).ToList();

            return Ok(result);
        }

        // ---------------------------
        //  3) Get EjecucionPago Detail
        // ---------------------------
        [HttpGet]
        [Route("GetDetalle/{pagoEjecutadoId}")]
        public IHttpActionResult GetDetalle(int pagoEjecutadoId)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var query = from pe in _context.EjecucionPagos
                        join pp in _context.PagosProgramados on pe.PagoProgramadoId equals pp.Id
                        join a in _context.AsignacionesCarga on pp.AsignacionCargaId equals a.Id
                        join proc in _context.AsigProcesos on a.AsigProcesoId equals proc.Id
                        join b in _context.Branch on proc.BranchesId equals b.Id
                        where pe.Id == pagoEjecutadoId
                        select new
                        {
                            // EjecucionPago
                            PagoEjecutadoId = pe.Id,
                            pe.PagoProgramadoId,
                            pe.TipoDocente,
                            pe.PorcentajeRetencion,
                            pe.MontoRetencion,
                            pe.MontoContrato,
                            pe.MontoReal,
                            pe.Estado,
                            pe.ObservacionesEjecucion,
                            pe.MotivoRechazo,
                            pe.FechaEnvio,
                            pe.FechaAprobacion,
                            pe.AprobadoPor,
                            pe.CreatedAt,

                            // PagoProgramado
                            pp.MesPago,
                            pp.AnioPago,
                            MontoBruto = pp.Monto,
                            pp.Porcentaje,
                            pp.EsExcepcion,

                            // Assignment
                            a.CiDocente,
                            a.PrimerApellido,
                            a.SegundoApellido,
                            a.TercerApellido,
                            a.Nombres,
                            a.Sigla,
                            a.Paralelo,
                            a.CodigoParalelo,
                            a.NumeroContrato,
                            a.HorasMes,
                            a.CostoHora,
                            a.CantidadMeses,

                            // Process
                            ProcesoId = proc.Id,
                            proc.BranchesId,
                            SedeName = b.Name,
                            proc.PeriodoId
                        };

            var pago = query.FirstOrDefault();

            if (pago == null)
                return NotFound();

            var result = new
            {
                // EjecucionPago details
                pago.PagoEjecutadoId,
                pago.PagoProgramadoId,
                pago.TipoDocente,
                pago.PorcentajeRetencion,
                pago.MontoRetencion,
                pago.MontoContrato,
                pago.MontoReal,
                pago.Estado,
                pago.ObservacionesEjecucion,
                pago.MotivoRechazo,
                pago.FechaEnvio,
                pago.FechaAprobacion,
                pago.CreatedAt,

                // PagoProgramado details
                pago.MesPago,
                pago.AnioPago,
                pago.MontoBruto,
                pago.Porcentaje,
                pago.EsExcepcion,

                // Assignment details
                pago.CiDocente,
                NombreCompleto = string.Join(" ", new[] {
                    pago.PrimerApellido,
                    pago.SegundoApellido,
                    pago.TercerApellido,
                    pago.Nombres
                }.Where(s => !string.IsNullOrWhiteSpace(s))),
                pago.Sigla,
                pago.Paralelo,
                pago.CodigoParalelo,
                pago.NumeroContrato,
                pago.HorasMes,
                pago.CostoHora,
                pago.CantidadMeses,
                MontoTotalAsignacion = pago.HorasMes * pago.CostoHora * pago.CantidadMeses,

                // Process details
                pago.ProcesoId,
                pago.BranchesId,
                pago.SedeName,
                pago.PeriodoId
            };

            return Ok(result);
        }

        // ---------------------------
        //  4) Approve Payment
        // ---------------------------
        public class AprobarPagoRequest
        {
            public int PagoEjecutadoId { get; set; }
            public decimal? MontoReal { get; set; }  // Optional: can adjust amount
            public string ObservacionesEjecucion { get; set; }
        }

        [HttpPost]
        [Route("AprobarPago")]
        public IHttpActionResult AprobarPago([FromBody] AprobarPagoRequest model)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            if (model == null || model.PagoEjecutadoId <= 0)
                return BadRequest("Datos inválidos");

            var pago = _context.EjecucionPagos.FirstOrDefault(p => p.Id == model.PagoEjecutadoId);
            if (pago == null)
                return NotFound();

            if (pago.Estado != "PENDIENTE_APROBACION")
            {
                return Content(
                    HttpStatusCode.BadRequest,
                    new { Message = "Solo se pueden aprobar pagos que están pendientes de aprobación" }
                );
            }

            // Update to approved
            pago.Estado = "APROBADO";
            pago.FechaAprobacion = DateTime.Now;
            pago.AprobadoPor = user.Id;

            // Update MontoReal if provided
            if (model.MontoReal.HasValue && model.MontoReal.Value != pago.MontoReal)
            {
                pago.MontoReal = model.MontoReal.Value;
            }

            // Add observations if provided
            if (!string.IsNullOrWhiteSpace(model.ObservacionesEjecucion))
            {
                if (string.IsNullOrWhiteSpace(pago.ObservacionesEjecucion))
                {
                    pago.ObservacionesEjecucion = model.ObservacionesEjecucion;
                }
                else
                {
                    pago.ObservacionesEjecucion += "\n" + model.ObservacionesEjecucion;
                }
            }

            // Update PagoProgramado estado
            var pagoProgramado = _context.PagosProgramados.FirstOrDefault(p => p.Id == pago.PagoProgramadoId);
            if (pagoProgramado != null)
            {
                pagoProgramado.Estado = "APROBADO";
            }

            _context.SaveChanges();

            return Ok(new
            {
                Message = "Pago aprobado correctamente",
                PagoEjecutadoId = pago.Id,
                MontoReal = pago.MontoReal
            });
        }

        // ---------------------------
        //  5) Reject Payment
        // ---------------------------
        public class RechazarPagoRequest
        {
            public int PagoEjecutadoId { get; set; }
            public string MotivoRechazo { get; set; }
        }

        [HttpPost]
        [Route("RechazarPago")]
        public IHttpActionResult RechazarPago([FromBody] RechazarPagoRequest model)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            if (model == null || model.PagoEjecutadoId <= 0)
                return BadRequest("Datos inválidos");

            if (string.IsNullOrWhiteSpace(model.MotivoRechazo))
                return BadRequest("Debe proporcionar un motivo de rechazo");

            var pago = _context.EjecucionPagos.FirstOrDefault(p => p.Id == model.PagoEjecutadoId);
            if (pago == null)
                return NotFound();

            if (pago.Estado != "PENDIENTE_APROBACION")
            {
                return Content(
                    HttpStatusCode.BadRequest,
                    new { Message = "Solo se pueden rechazar pagos que están pendientes de aprobación" }
                );
            }

            // Update to rejected
            pago.Estado = "RECHAZADO";
            pago.MotivoRechazo = model.MotivoRechazo;

            // Update PagoProgramado estado to allow re-editing
            var pagoProgramado = _context.PagosProgramados.FirstOrDefault(p => p.Id == pago.PagoProgramadoId);
            if (pagoProgramado != null)
            {
                pagoProgramado.Estado = "RECHAZADO";
            }

            _context.SaveChanges();

            return Ok(new
            {
                Message = "Pago rechazado correctamente",
                PagoEjecutadoId = pago.Id
            });
        }
    }
}