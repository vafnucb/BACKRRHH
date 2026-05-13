using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Http;
using UcbBack.Logic;
using UcbBack.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;

using ClosedXML.Excel;


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
                    new { Message = string.Format("Hay {0} pago(s) sin tipo de docente asignado", sinTipo.Count) }
                );
            }

            // Validate none are already sent
            var yaEnviados = pagos.Where(p => p.Estado == "ENVIADO" || p.Estado == "APROBADO").ToList();
            if (yaEnviados.Any())
            {
                return Content(
                    HttpStatusCode.BadRequest,
                    new { Message = string.Format("Hay {0} pago(s) que ya fueron enviados o aprobados", yaEnviados.Count) }
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


        // 2) Get Payments Pending Approval
        [HttpGet]
        [Route("GetPagosPendientes")]
        public IHttpActionResult GetPagosPendientes(int? branchId = null, string periodoId = null, int? mes = null, int? anio = null)
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
                            pp.MontoOriginal,

                            // Assignment
                            a.CiDocente,
                            a.PrimerApellido,
                            a.SegundoApellido,
                            a.TercerApellido,
                            a.Nombres,
                            a.NumeroContrato,
                            a.Sigla,
                            a.Paralelo,

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
            if (mes.HasValue)
            {
                query = query.Where(q => q.MesPago == mes.Value);
            }
            if (anio.HasValue)
            {
                query = query.Where(q => q.AnioPago == anio.Value);
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
                p.Sigla,
                p.Paralelo,
                p.BranchesId,
                p.PeriodoId,
                p.MontoOriginal,
                MontoModificado = p.MontoOriginal != null && p.MontoOriginal != p.MontoBruto
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
                            pp.AsignacionCargaId,
                            pp.MesPago,
                            pp.AnioPago,
                            MontoBruto = pp.Monto,
                            pp.MontoOriginal,
                            pp.Observaciones,
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

            // Get all scheduled payments for this assignment
            var todosLosPagos = _context.PagosProgramados
                .Where(p => p.AsignacionCargaId == pago.AsignacionCargaId)
                .OrderBy(p => p.AnioPago)
                .ThenBy(p => p.MesPago)
                .Select(p => new
                {
                    p.Id,
                    p.MesPago,
                    p.AnioPago,
                    p.Monto,
                    p.Porcentaje,
                    p.EsExcepcion,
                    p.Estado
                })
                .ToList();

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
                pago.AprobadoPor,
                pago.CreatedAt,

                // PagoProgramado details
                pago.MesPago,
                pago.AnioPago,
                pago.MontoBruto,
                pago.MontoOriginal,
                pago.Observaciones,
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
                NombreMateria = GetNombreMateria(pago.CodigoParalelo),

                // Process details
                pago.ProcesoId,
                pago.BranchesId,
                pago.SedeName,
                pago.PeriodoId,

                // Payment schedule for entire assignment
                CalendarioPagos = todosLosPagos
            };

            return Ok(result);
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

        //  4) Approve Payment
  
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

            // Update EjecucionPago to rejected
            pago.Estado = "RECHAZADO";
            pago.MotivoRechazo = model.MotivoRechazo;

            // Update PagoProgramado estado to allow re-editing
            var pagoProgramado = _context.PagosProgramados.FirstOrDefault(p => p.Id == pago.PagoProgramadoId);
            if (pagoProgramado != null)
            {
                pagoProgramado.Estado = "RECHAZADO";

                // ADD REJECTION REASON TO OBSERVACIONES with timestamp
                var timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                var rejectionNote = string.Format("[{0}] PAGO RECHAZADO: {1}", timestamp, model.MotivoRechazo);

                if (string.IsNullOrWhiteSpace(pagoProgramado.Observaciones))
                {
                    pagoProgramado.Observaciones = rejectionNote;
                }
                else
                {
                    pagoProgramado.Observaciones += "\n" + rejectionNote;
                }
            }

            _context.SaveChanges();

            return Ok(new
            {
                Message = "Pago rechazado correctamente",
                PagoEjecutadoId = pago.Id
            });
        }

        //  6) Approve Multiple Payments in Batch and Generate Excel
        public class AprobarPagosLoteRequest
        {
            public List<int> PagosIds { get; set; }
        }

        [HttpPost]
        [Route("AprobarPagosLote")]
        public HttpResponseMessage AprobarPagosLote([FromBody] AprobarPagosLoteRequest model)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Request.CreateResponse(HttpStatusCode.Unauthorized);

            if (model == null || model.PagosIds == null || !model.PagosIds.Any())
                return Request.CreateResponse(HttpStatusCode.BadRequest, new { Message = "No se enviaron pagos para aprobar" });

            // Get payments to approve
            var pagos = _context.EjecucionPagos
                .Where(ep => model.PagosIds.Contains(ep.Id) && ep.Estado == "PENDIENTE_APROBACION")
                .ToList();

            if (!pagos.Any())
                return Request.CreateResponse(HttpStatusCode.BadRequest, new { Message = "No hay pagos pendientes para aprobar con los IDs proporcionados" });

            // Approve all
            foreach (var pago in pagos)
            {
                pago.Estado = "APROBADO";
                pago.FechaAprobacion = DateTime.Now;
                pago.AprobadoPor = user.Id;

                // Update related PagoProgramado
                var pagoProgramado = _context.PagosProgramados.FirstOrDefault(p => p.Id == pago.PagoProgramadoId);
                if (pagoProgramado != null)
                {
                    pagoProgramado.Estado = "APROBADO";
                }
            }

            _context.SaveChanges();

            // Generate Excel
            var excelStream = GenerarExcelPagos(pagos);

            var response = Request.CreateResponse(HttpStatusCode.OK);
            response.Content = new StreamContent(excelStream);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = string.Format("Pagos_Aprobados_{0}.xlsx", DateTime.Now.ToString("yyyyMMdd_HHmmss"))
            };

            return response;
        }

        // ---------------------------
        //  7) Generate Excel Helper Method
        // ---------------------------
        [NonAction]
        private MemoryStream GenerarExcelPagos(List<EjecucionPago> pagos)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Pagos Aprobados");

                // Headers
                worksheet.Cell("A1").Value = "Codigo_Socio";
                worksheet.Cell("B1").Value = "Nombre_Socio";
                worksheet.Cell("C1").Value = "Cod_Dependencia";
                worksheet.Cell("D1").Value = "PEI_PO";
                worksheet.Cell("E1").Value = "Nombre_del_Servicio";
                worksheet.Cell("F1").Value = "Periodo_Academico";
                worksheet.Cell("G1").Value = "Sigla_Asignatura";
                worksheet.Cell("H1").Value = "Paralelo";
                worksheet.Cell("I1").Value = "Código_Paralelo_SAP";
                worksheet.Cell("J1").Value = "Cuenta_Asignada";
                worksheet.Cell("K1").Value = "Monto_Contrato";
                worksheet.Cell("L1").Value = "Monto_IUE";
                worksheet.Cell("M1").Value = "Monto_IT";
                worksheet.Cell("N1").Value = "IUEExterior";
                worksheet.Cell("O1").Value = "Monto_a_Pagar";
                worksheet.Cell("P1").Value = "Observaciones";

                // Style headers
                var headerRange = worksheet.Range("A1:P1");
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Data rows
                int row = 2;
                foreach (var pago in pagos)
                {
                    var pagoProgramado = _context.PagosProgramados.FirstOrDefault(p => p.Id == pago.PagoProgramadoId);
                    var asignacion = pagoProgramado != null
                        ? _context.AsignacionesCarga.FirstOrDefault(a => a.Id == pagoProgramado.AsignacionCargaId)
                        : null;
                    var proceso = asignacion != null
                        ? _context.AsigProcesos.FirstOrDefault(ap => ap.Id == asignacion.AsigProcesoId)
                        : null;
                    var programacion = pagoProgramado != null && pagoProgramado.ProgramacionPagosId.HasValue
                        ? _context.ProgramacionPagos.FirstOrDefault(pg => pg.Id == pagoProgramado.ProgramacionPagosId.Value)
                        : null;

                    // Get Codigo_Socio from T_REG_CIVIL
                    string codigoSocio = GetCodigoSocio(asignacion);

                    // Build Nombre_Socio
                    string nombreSocio = asignacion != null
                        ? string.Join(" ", new[] {
                    asignacion.PrimerApellido,
                    asignacion.SegundoApellido,
                    asignacion.TercerApellido,
                    asignacion.Nombres
                          }.Where(s => !string.IsNullOrWhiteSpace(s)))
                        : "";

                    // Get Cod_Dependencia
                    string codDependencia = GetCodDependencia(
                        asignacion?.UnidadOrganizacional,
                        proceso?.BranchesId
                    );

                    // Calculate tax amounts using ROUNDED 2 decimals
                    decimal montoIUE = RoundTo2Decimals(CalculateMontoIUE(pago.MontoContrato, pago.TipoDocente));
                    decimal montoIT = RoundTo2Decimals(CalculateMontoIT(pago.MontoContrato, pago.TipoDocente));
                    decimal iueExterior = RoundTo2Decimals(CalculateIUEExterior(pago.MontoContrato, pago.TipoDocente));

                    // A - Codigo_Socio
                    worksheet.Cell(row, 1).Value = codigoSocio;

                    // B - Nombre_Socio
                    worksheet.Cell(row, 2).Value = nombreSocio;

                    // C - Cod_Dependencia
                    worksheet.Cell(row, 3).Value = codDependencia;

                    // D - PEI_PO
                    worksheet.Cell(row, 4).Value = "PO";

                    // E - Nombre_del_Servicio (NombrePlantilla from programacion)
                    worksheet.Cell(row, 5).Value = programacion?.NombrePlantilla ?? "";

                    // F - Periodo_Academico
                    worksheet.Cell(row, 6).Value = proceso?.PeriodoId ?? "";

                    // G - Sigla_Asignatura
                    worksheet.Cell(row, 7).Value = asignacion?.Sigla ?? "";

                    // H - Paralelo
                    worksheet.Cell(row, 8).Value = asignacion?.Paralelo ?? "";

                    // I - Código_Paralelo_SAP
                    worksheet.Cell(row, 9).Value = asignacion?.CodigoParalelo ?? "";

                    // J - Cuenta_Asignada
                    worksheet.Cell(row, 10).Value = "CC_TEMPORAL";

                    // K - Monto_Contrato (before taxes) - ROUNDED
                    worksheet.Cell(row, 11).Value = RoundTo2Decimals(pago.MontoContrato);

                    // L - Monto_IUE - ROUNDED
                    worksheet.Cell(row, 12).Value = montoIUE;

                    // M - Monto_IT - ROUNDED
                    worksheet.Cell(row, 13).Value = montoIT;

                    // N - IUEExterior - ROUNDED
                    worksheet.Cell(row, 14).Value = iueExterior;

                    // O - Monto_a_Pagar (after taxes) - ROUNDED
                    worksheet.Cell(row, 15).Value = RoundTo2Decimals(pago.MontoReal);

                    // P - Observaciones (from PagoProgramado + Bank Info)
                    worksheet.Cell(row, 16).Value = GetObservacionesWithBankInfo(pagoProgramado?.Observaciones, asignacion);

                    row++;
                }

                // Format number columns
                worksheet.Range(string.Format("K2:O{0}", row - 1)).Style.NumberFormat.Format = "#,##0.00";

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;
                return stream;
            }
        }

        // Helper: Round to 2 decimals
        [NonAction]
        private decimal RoundTo2Decimals(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        // Helper: Calculate IUE
        [NonAction]
        private decimal CalculateMontoIUE(decimal montoBruto, string tipoDocente)
        {
            if (tipoDocente?.ToUpper() == "INDEPENDIENTE_SIN_FACTURA")
            {
                return montoBruto * 0.13m;
            }
            return 0.00m;
        }

        // Helper: Calculate IT
        [NonAction]
        private decimal CalculateMontoIT(decimal montoBruto, string tipoDocente)
        {
            if (tipoDocente?.ToUpper() == "INDEPENDIENTE_SIN_FACTURA")
            {
                return montoBruto * 0.03m;
            }
            return 0.00m;
        }

        // Helper: Calculate IUE Exterior
        [NonAction]
        private decimal CalculateIUEExterior(decimal montoBruto, string tipoDocente)
        {
            if (tipoDocente?.ToUpper() == "EXTRANJERO")
            {
                return montoBruto * 0.125m;
            }
            return 0.00m;
        }

        [NonAction]
        private string GetCodigoSocio(AsignacionCarga asignacion)
        {
            if (asignacion == null)
                return "";

            // Try 1: Search by CI (NIT in Civil)
            var civilPorCI = _context.Database.SqlQuery<CivilRow>(
                "SELECT \"SAPId\", \"NIT\", \"FullName\" FROM ADMNALRRHH.\"Civil\" WHERE \"NIT\" = :ci",
                new Sap.Data.Hana.HanaParameter("ci", asignacion.CiDocente ?? "")
            ).FirstOrDefault();

            if (civilPorCI != null && !string.IsNullOrWhiteSpace(civilPorCI.SAPId))
            {
                return civilPorCI.SAPId;
            }

            // Build full name variations
            var nombreCompleto1 = string.Join(" ", new[] {
        asignacion.PrimerApellido,
        asignacion.SegundoApellido,
        asignacion.TercerApellido,
        asignacion.Nombres
    }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim().ToUpper();

            var nombreCompleto2 = string.Join(" ", new[] {
        asignacion.Nombres,
        asignacion.PrimerApellido,
        asignacion.SegundoApellido,
        asignacion.TercerApellido
    }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim().ToUpper();

            // Try 2: Search by FullName (variation 1)
            var civilPorNombre1 = _context.Database.SqlQuery<CivilRow>(
                "SELECT \"SAPId\", \"NIT\", \"FullName\" FROM ADMNALRRHH.\"Civil\" WHERE UPPER(\"FullName\") = :nombre",
                new Sap.Data.Hana.HanaParameter("nombre", nombreCompleto1)
            ).FirstOrDefault();

            if (civilPorNombre1 != null && !string.IsNullOrWhiteSpace(civilPorNombre1.SAPId))
            {
                return civilPorNombre1.SAPId;
            }

            // Try 3: Search by FullName (variation 2)
            var civilPorNombre2 = _context.Database.SqlQuery<CivilRow>(
                "SELECT \"SAPId\", \"NIT\", \"FullName\" FROM ADMNALRRHH.\"Civil\" WHERE UPPER(\"FullName\") = :nombre",
                new Sap.Data.Hana.HanaParameter("nombre", nombreCompleto2)
            ).FirstOrDefault();

            if (civilPorNombre2 != null && !string.IsNullOrWhiteSpace(civilPorNombre2.SAPId))
            {
                return civilPorNombre2.SAPId;
            }

            // Not found
            return "";
        }

        [NonAction]
        private string GetCodDependencia(string unidadOrganizacional, int? branchesId)
        {
            if (string.IsNullOrWhiteSpace(unidadOrganizacional) || !branchesId.HasValue)
                return "";

            try
            {
                // Step 1: Get OrganizationalUnit Id by Code
                var orgUnit = _context.Database.SqlQuery<OrganizationalUnitRow>(
                    "SELECT \"Id\" FROM ADMNALRRHH.\"OrganizationalUnit\" WHERE \"Cod\" = :code",
                    new Sap.Data.Hana.HanaParameter("code", unidadOrganizacional)
                ).FirstOrDefault();


                if (orgUnit == null)
                    return "";

                // Step 2: Get Dependency Cod using OrganizationalUnitId and BranchesId
                var dependency = _context.Database.SqlQuery<DependencyRow>(
                    "SELECT \"Cod\" FROM ADMNALRRHH.\"Dependency\" WHERE \"OrganizationalUnitId\" = :orgUnitId AND \"BranchesId\" = :branchId",
                    new Sap.Data.Hana.HanaParameter("orgUnitId", orgUnit.Id),
                    new Sap.Data.Hana.HanaParameter("branchId", branchesId.Value)
                ).FirstOrDefault();

                return dependency?.Cod ?? "";
            }
            catch (Exception ex)
            {
                // Log error if needed
                System.Diagnostics.Debug.WriteLine(string.Format("Error getting Cod_Dependencia: {0}", ex.Message));
                return "";
            }
        }

        // Helper classes for queries
        private class CivilRow
        {
            public string SAPId { get; set; }
            public string NIT { get; set; }
            public string FullName { get; set; }
        }

        private class OrganizationalUnitRow
        {
            public int Id { get; set; }
        }

        private class DependencyRow
        {
            public string Cod { get; set; }
        }

        //  8) Get Payments with Filters (for tabs in frontend)
        [HttpGet]
        [Route("GetPagosPorFiltros")]
        public IHttpActionResult GetPagosPorFiltros(
            int? branchesId = null,
            string periodoId = null,
            int? mes = null,
            int? anio = null,
            string tipoDocente = null)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            // Base query (same as GetPagosPendientes but with more filters)
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
                            a.Sigla,
                            a.Paralelo,

                            // Process
                            proc.BranchesId,
                            proc.PeriodoId
                        };

            // Apply filters
            if (branchesId.HasValue)
                query = query.Where(q => q.BranchesId == branchesId.Value);

            if (!string.IsNullOrWhiteSpace(periodoId))
                query = query.Where(q => q.PeriodoId == periodoId);

            if (mes.HasValue)
                query = query.Where(q => q.MesPago == mes.Value);

            if (anio.HasValue)
                query = query.Where(q => q.AnioPago == anio.Value);

            // Filter by TipoDocente if provided
            if (!string.IsNullOrWhiteSpace(tipoDocente))
                query = query.Where(q => q.TipoDocente == tipoDocente);

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
                p.Sigla,
                p.Paralelo,
                p.BranchesId,
                p.PeriodoId
            }).ToList();

            return Ok(result);
        }

        // ---------------------------
        //  9) Get Approved Payments (Historic)
        // ---------------------------
        [HttpGet]
        [Route("GetPagosAprobados")]
        public IHttpActionResult GetPagosAprobados(
            int? branchesId = null,
            string periodoId = null,
            int? mes = null,
            int? anio = null,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            string search = null,
            int page = 1,
            int pageSize = 50)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            // Base query - same as GetPagosPendientes but for APROBADO
            var query = from pe in _context.EjecucionPagos
                        join pp in _context.PagosProgramados on pe.PagoProgramadoId equals pp.Id
                        join a in _context.AsignacionesCarga on pp.AsignacionCargaId equals a.Id
                        join proc in _context.AsigProcesos on a.AsigProcesoId equals proc.Id
                        where pe.Estado == "APROBADO"
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
                            pe.FechaAprobacion,
                            pe.AprobadoPor,

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
                            a.Sigla,
                            a.Paralelo,

                            // Process
                            proc.BranchesId,
                            proc.PeriodoId
                        };

            // Apply filters
            if (branchesId.HasValue)
                query = query.Where(q => q.BranchesId == branchesId.Value);

            if (!string.IsNullOrWhiteSpace(periodoId))
                query = query.Where(q => q.PeriodoId == periodoId);

            if (mes.HasValue)
                query = query.Where(q => q.MesPago == mes.Value);

            if (anio.HasValue)
                query = query.Where(q => q.AnioPago == anio.Value);

            if (fechaDesde.HasValue)
                query = query.Where(q => q.FechaAprobacion >= fechaDesde.Value);

            if (fechaHasta.HasValue)
                query = query.Where(q => q.FechaAprobacion <= fechaHasta.Value);
            // Apply regional filtering and materialize
            var filteredQuery = auth.filerByRegional(query.AsQueryable(), user).ToList();

            // Apply search filter in memory
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchUpper = search.Trim().ToUpper();
                filteredQuery = filteredQuery.Where(q =>
                    (q.CiDocente != null && q.CiDocente.ToUpper().Contains(searchUpper)) ||
                    (q.PrimerApellido != null && q.PrimerApellido.ToUpper().Contains(searchUpper)) ||
                    (q.SegundoApellido != null && q.SegundoApellido.ToUpper().Contains(searchUpper)) ||
                    (q.TercerApellido != null && q.TercerApellido.ToUpper().Contains(searchUpper)) ||
                    (q.Nombres != null && q.Nombres.ToUpper().Contains(searchUpper)) ||
                    (q.NumeroContrato != null && q.NumeroContrato.ToUpper().Contains(searchUpper)) ||
                    (q.Sigla != null && q.Sigla.ToUpper().Contains(searchUpper)) ||
                    (q.TipoDocente != null && q.TipoDocente.ToUpper().Contains(searchUpper))
                ).ToList();
            }

            // Order by FechaAprobacion descending (most recent first) - in memory
            var ordered = filteredQuery.OrderByDescending(q => q.FechaAprobacion).ToList();

            // Count total
            var total = ordered.Count();

            // Paginate
            var pagos = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

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
                p.FechaAprobacion,
                p.AprobadoPor,
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
                p.Sigla,
                p.Paralelo,
                p.BranchesId,
                p.PeriodoId
            }).ToList();

            return Ok(new
            {
                Items = result,
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        }

        //  10) Get Excel Values for Approved Payment
        public class ExcelValoresResponse
        {
            public string CodigoSocio { get; set; }
            public string NombreSocio { get; set; }
            public string CodDependencia { get; set; }
            public string PEIPO { get; set; }
            public string NombreDelServicio { get; set; }
            public string PeriodoAcademico { get; set; }
            public string SiglaAsignatura { get; set; }
            public string Paralelo { get; set; }
            public string CodigoParaleloSAP { get; set; }
            public string CuentaAsignada { get; set; }
            public decimal MontoContrato { get; set; }
            public decimal MontoIUE { get; set; }
            public decimal MontoIT { get; set; }
            public decimal IUEExterior { get; set; }
            public decimal MontoAPagar { get; set; }
            public string Observaciones { get; set; }
            public string NombreMateria { get; set; }
            public string UnidadOrganizacional { get; set; }
        }

        [HttpGet]
        [Route("GetValoresExcel/{pagoEjecutadoId}")]
        public IHttpActionResult GetValoresExcel(int pagoEjecutadoId)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var pago = _context.EjecucionPagos.FirstOrDefault(p => p.Id == pagoEjecutadoId);
            if (pago == null)
                return NotFound();

            var pagoProgramado = _context.PagosProgramados.FirstOrDefault(p => p.Id == pago.PagoProgramadoId);
            var asignacion = pagoProgramado != null
                ? _context.AsignacionesCarga.FirstOrDefault(a => a.Id == pagoProgramado.AsignacionCargaId)
                : null;
            var proceso = asignacion != null
                ? _context.AsigProcesos.FirstOrDefault(ap => ap.Id == asignacion.AsigProcesoId)
                : null;
            var programacion = pagoProgramado != null && pagoProgramado.ProgramacionPagosId.HasValue
                ? _context.ProgramacionPagos.FirstOrDefault(pg => pg.Id == pagoProgramado.ProgramacionPagosId.Value)
                : null;

            // Calculate all Excel values
            var codigoSocio = GetCodigoSocio(asignacion);
            var nombreSocio = asignacion != null
                ? string.Join(" ", new[] {
            asignacion.PrimerApellido,
            asignacion.SegundoApellido,
            asignacion.TercerApellido,
            asignacion.Nombres
                  }.Where(s => !string.IsNullOrWhiteSpace(s)))
                : "";
            var codDependencia = GetCodDependencia(asignacion?.UnidadOrganizacional, proceso?.BranchesId);

            var montoIUE = RoundTo2Decimals(CalculateMontoIUE(pago.MontoContrato, pago.TipoDocente));
            var montoIT = RoundTo2Decimals(CalculateMontoIT(pago.MontoContrato, pago.TipoDocente));
            var iueExterior = RoundTo2Decimals(CalculateIUEExterior(pago.MontoContrato, pago.TipoDocente));

            var response = new ExcelValoresResponse
            {
                CodigoSocio = codigoSocio,
                NombreSocio = nombreSocio,
                CodDependencia = codDependencia,
                PEIPO = "PO",
                NombreDelServicio = programacion?.NombrePlantilla ?? "",
                PeriodoAcademico = proceso?.PeriodoId ?? "",
                SiglaAsignatura = asignacion?.Sigla ?? "",
                Paralelo = asignacion?.Paralelo ?? "",
                CodigoParaleloSAP = asignacion?.CodigoParalelo ?? "",
                CuentaAsignada = "CC_TEMPORAL",
                MontoContrato = RoundTo2Decimals(pago.MontoContrato),
                MontoIUE = montoIUE,
                MontoIT = montoIT,
                IUEExterior = iueExterior,
                MontoAPagar = RoundTo2Decimals(pago.MontoReal),
                Observaciones = pagoProgramado?.Observaciones ?? ""
            };

            return Ok(response);
        }

        // 11) Get Excel Values for Multiple Payments (for PDF generation)
        [HttpPost]
        [Route("GetValoresExcelLote")]
        public IHttpActionResult GetValoresExcelLote([FromBody] AprobarPagosLoteRequest model)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            if (model == null || model.PagosIds == null || !model.PagosIds.Any())
                return BadRequest("No se enviaron IDs de pagos");

            var pagos = _context.EjecucionPagos
                .Where(ep => model.PagosIds.Contains(ep.Id))
                .ToList();

            if (!pagos.Any())
                return BadRequest("No se encontraron pagos con los IDs proporcionados");

            var result = new List<ExcelValoresResponse>();

            foreach (var pago in pagos)
            {
                var pagoProgramado = _context.PagosProgramados.FirstOrDefault(p => p.Id == pago.PagoProgramadoId);
                var asignacion = pagoProgramado != null
                    ? _context.AsignacionesCarga.FirstOrDefault(a => a.Id == pagoProgramado.AsignacionCargaId)
                    : null;
                var proceso = asignacion != null
                    ? _context.AsigProcesos.FirstOrDefault(ap => ap.Id == asignacion.AsigProcesoId)
                    : null;
                var programacion = pagoProgramado != null && pagoProgramado.ProgramacionPagosId.HasValue
                    ? _context.ProgramacionPagos.FirstOrDefault(pg => pg.Id == pagoProgramado.ProgramacionPagosId.Value)
                    : null;

                var codigoSocio = GetCodigoSocio(asignacion);
                var nombreSocio = asignacion != null
                    ? string.Join(" ", new[] {
                        asignacion.PrimerApellido,
                        asignacion.SegundoApellido,
                        asignacion.TercerApellido,
                        asignacion.Nombres
                    }.Where(s => !string.IsNullOrWhiteSpace(s)))
                    : "";
                var codDependencia = GetCodDependencia(asignacion?.UnidadOrganizacional, proceso?.BranchesId);

                var montoIUE = RoundTo2Decimals(CalculateMontoIUE(pago.MontoContrato, pago.TipoDocente));
                var montoIT = RoundTo2Decimals(CalculateMontoIT(pago.MontoContrato, pago.TipoDocente));
                var iueExterior = RoundTo2Decimals(CalculateIUEExterior(pago.MontoContrato, pago.TipoDocente));

                result.Add(new ExcelValoresResponse
                {
                    CodigoSocio = codigoSocio,
                    NombreSocio = nombreSocio,
                    CodDependencia = codDependencia,
                    PEIPO = "PO",
                    NombreDelServicio = programacion?.NombrePlantilla ?? "",
                    PeriodoAcademico = proceso?.PeriodoId ?? "",
                    SiglaAsignatura = asignacion?.Sigla ?? "",
                    Paralelo = asignacion?.Paralelo ?? "",
                    CodigoParaleloSAP = asignacion?.CodigoParalelo ?? "",
                    CuentaAsignada = "CC_TEMPORAL",
                    MontoContrato = RoundTo2Decimals(pago.MontoContrato),
                    MontoIUE = montoIUE,
                    MontoIT = montoIT,
                    IUEExterior = iueExterior,
                    MontoAPagar = RoundTo2Decimals(pago.MontoReal),
                    Observaciones = GetObservacionesWithBankInfo(pagoProgramado?.Observaciones, asignacion),
                    NombreMateria = GetNombreMateria(asignacion?.CodigoParalelo),
                    UnidadOrganizacional = asignacion != null && !string.IsNullOrWhiteSpace(asignacion.UnidadOrganizacional)
                        ? (_context.OrganizationalUnits.Where(ou => ou.Cod == asignacion.UnidadOrganizacional).Select(ou => ou.Name).FirstOrDefault() ?? asignacion.UnidadOrganizacional)
                        : ""
                });
            }

            // Get branch name from the first payment's process
            var branchName = "";
            var firstPago = pagos.FirstOrDefault();
            if (firstPago != null)
            {
                var firstPP = _context.PagosProgramados.FirstOrDefault(p => p.Id == firstPago.PagoProgramadoId);
                var firstAsig = firstPP != null ? _context.AsignacionesCarga.FirstOrDefault(a => a.Id == firstPP.AsignacionCargaId) : null;
                var firstProc = firstAsig != null ? _context.AsigProcesos.FirstOrDefault(ap => ap.Id == firstAsig.AsigProcesoId) : null;
                var firstBranch = firstProc != null ? _context.Branch.FirstOrDefault(b => b.Id == firstProc.BranchesId) : null;
                if (firstBranch != null) branchName = firstBranch.Name;
            }

            return Ok(new { BranchName = branchName, Pagos = result });
        }

        [NonAction]
        private string GetObservacionesWithBankInfo(string observaciones, AsignacionCarga asignacion)
        {
            string obs = observaciones ?? "";
            if (asignacion == null)
                return obs;

            string sapId = GetCodigoSocio(asignacion);
            if (string.IsNullOrWhiteSpace(sapId))
                return obs;

            var civil = _context.Civils.FirstOrDefault(c => c.SAPId == sapId);
            if (civil == null)
                return obs;

            var civilExtra = _context.CivilExtras.FirstOrDefault(ce => ce.CivilId == civil.Id);
            if (civilExtra == null || (string.IsNullOrWhiteSpace(civilExtra.BankName) && string.IsNullOrWhiteSpace(civilExtra.BankAccountNumber)))
                return obs;

            string bankInfo = "";
            if (!string.IsNullOrWhiteSpace(civilExtra.BankName))
                bankInfo += "Banco: " + civilExtra.BankName;
            if (!string.IsNullOrWhiteSpace(civilExtra.BankAccountNumber))
                bankInfo += (bankInfo.Length > 0 ? " | " : "") + "Cuenta: " + civilExtra.BankAccountNumber;

            return string.IsNullOrWhiteSpace(obs)
                ? bankInfo
                : obs + " | " + bankInfo;
        }
    }
}