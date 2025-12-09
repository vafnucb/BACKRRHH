using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using ClosedXML.Excel;
using Newtonsoft.Json.Linq;
using UcbBack.Models;
using UcbBack.Models.Auth;

namespace UcbBack.Logic.ExcelFiles.Asignaciones
{
    public class AsignacionesExcel
    {
        private readonly Stream _excelStream;
        private readonly ApplicationDbContext _context;
        private readonly string _fileName;
        private readonly AsigProceso _proceso;
        private readonly CustomUser _user;

        private readonly int _headerRowIndex;
        private readonly int _sheets;

        // For validations
        private List<ParaleloRow> _paralelosDb;
        private List<CivilRow> _civilesDb;
        private Dictionary<int, string> _branchesNames;


        public AsignacionesExcel(
            Stream excelStream,
            ApplicationDbContext context,
            string fileName,
            AsigProceso proceso,
            CustomUser user,
            int headerin = 1,
            int sheets = 1)
        {
            _excelStream = excelStream;
            _context = context;
            _fileName = fileName;
            _proceso = proceso;
            _user = user;
            _headerRowIndex = headerin;
            _sheets = sheets;
        }

        /// <summary>
        /// Representa una fila de ADMNAL.T_REG_PARALELOS_NS usada solo para validación.
        /// </summary>
        private class ParaleloRow
        {
            public string CODIGOSAP { get; set; }
            public string SIGLA { get; set; }
            public string NUMPARALELO { get; set; }
            public string CODUNIDADORGANIZACIONAL { get; set; }
            public string SEDE { get; set; }
            public string PERIODOSAP { get; set; }
        }

        /// <summary>
        /// Representa una fila de ADMNALRRHH."Civil" usada para validar Ci_Docente.
        /// </summary>
        private class CivilRow
        {
            public string NIT { get; set; }
            public int BranchesId { get; set; }  // id numérico de la sede
        }


        private class ExcelError
        {
            public int RowNumber { get; set; }
            public List<string> Messages { get; set; } = new List<string>();
        }

        public bool ValidateFile(out HttpResponseMessage errorResponse)
        {
            errorResponse = null;

            // 1) Load DB data for validation
            LoadParalelosFromDb();
            LoadCivilesFromDb();
            LoadBranchesNames();

            using (var workbook = new XLWorkbook(_excelStream))
            {
                var ws = workbook.Worksheet(1);

                // 2) Read header row
                var headerRow = ws.Row(_headerRowIndex);

                var requiredColumns = new[]
                {
                        "Ci_Docente",
                        "Primer_Apellido",
                        "Segundo_Apellido",
                        "Tercer_Apellido",
                        "Nombres",
                        "Periodo",
                        "Sigla",
                        "Codigo_Paralelo",
                        "Paralelo",
                        "Horas_Semana",
                        "Horas_Mes",
                        "Costo_Hora"
                };

                var columnIndexes = new Dictionary<string, int>();

                foreach (var cell in headerRow.CellsUsed())
                {
                    var headerText = cell.GetString().Trim();
                    if (!string.IsNullOrEmpty(headerText) && !columnIndexes.ContainsKey(headerText))
                    {
                        columnIndexes[headerText] = cell.Address.ColumnNumber;
                    }
                }

                // 3) Check missing columns
                var missing = requiredColumns
                    .Where(col => !columnIndexes.ContainsKey(col))
                    .ToList();

                if (missing.Any())
                {
                    var msg = "Faltan las siguientes columnas obligatorias: " + string.Join(", ", missing);
                    errorResponse = BuildSimpleErrorResponse(msg);
                    return false;
                }

                var errors = new List<ExcelError>();

                // 4) Iterate rows
                var lastRowUsed = ws.LastRowUsed().RowNumber();

                for (int rowNum = _headerRowIndex + 1; rowNum <= lastRowUsed; rowNum++)
                {
                    var row = ws.Row(rowNum);

                    // Empty row? skip
                    if (row.IsEmpty())
                        continue;

                    var rowErrors = new List<string>();

                    // Read values
                    var ciDocente = row.Cell(columnIndexes["Ci_Docente"]).GetString().Trim();
                    var periodo = row.Cell(columnIndexes["Periodo"]).GetString().Trim();
                    var sigla = row.Cell(columnIndexes["Sigla"]).GetString().Trim();
                    var codigoParalelo = row.Cell(columnIndexes["Codigo_Paralelo"]).GetString().Trim();
                    var paralelo = row.Cell(columnIndexes["Paralelo"]).GetString().Trim();
                  //  var unidadOrg = row.Cell(columnIndexes["Unidad_Organizacional"]).GetString().Trim();
                   // var sede = row.Cell(columnIndexes["Sede"]).GetString().Trim();

                    // ======== VALIDACIÓN CI / CIVIL ========
                    int branchIdProceso = _proceso.BranchesId;

                    if (string.IsNullOrEmpty(ciDocente))
                    {
                        rowErrors.Add("El CI del docente (Ci_Docente) es obligatorio.");
                    }
                    else
                    {
                        // 1) Buscar coincidencias exactas de NIT = Ci_Docente
                        var civilesMismoCi = _civilesDb
                            .Where(c => !string.IsNullOrEmpty(c.NIT) &&
                                        string.Equals(c.NIT, ciDocente, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (!civilesMismoCi.Any())
                        {
                            // 2) No hay match exacto -> buscamos NIT similares (prefijo)
                            var similares = _civilesDb
                                .Where(c =>
                                    !string.IsNullOrEmpty(c.NIT) &&
                                    !string.IsNullOrEmpty(ciDocente) &&
                                    (c.NIT.StartsWith(ciDocente) || ciDocente.StartsWith(c.NIT)))
                                .ToList();

                            if (!similares.Any())
                            {
                                rowErrors.Add($"No existe ningún registro en Civil con NIT = {ciDocente}.");
                            }
                            else
                            {
                                var candidatosTexto = similares
                                    .GroupBy(c => c.NIT)
                                    .Select(g =>
                                    {
                                        var sedesIds = g
                                            .Select(x => x.BranchesId)
                                            .Distinct()
                                            .OrderBy(x => x)
                                            .ToList();

                                        var sedesNombres = sedesIds
                                            .Select(id =>
                                                _branchesNames != null && _branchesNames.ContainsKey(id)
                                                    ? _branchesNames[id]
                                                    : $"Sede {id}")
                                            .ToList();

                                        return $"{g.Key} (Sedes: {string.Join(", ", sedesNombres)})";
                                    })
                                    .ToList();

                                rowErrors.Add(
                                    $"No existe un NIT exactamente igual a {ciDocente} en Civil, " +
                                    $"pero se encontraron NIT similares: {string.Join("; ", candidatosTexto)}. " +
                                    $"Verifique si alguno de ellos corresponde al docente."
                                );
                            }
                        }
                        else
                        {
                            // Hay coincidencias exactas de NIT = Ci_Docente en Civil
                            var civilesMismaSede = civilesMismoCi
                                .Where(c => c.BranchesId == branchIdProceso)
                                .ToList();

                            if (!civilesMismaSede.Any())
                            {
                                var otrasSedesNombres = civilesMismoCi
                                    .Select(c => c.BranchesId)
                                    .Distinct()
                                    .OrderBy(x => x)
                                    .Select(id =>
                                        _branchesNames != null && _branchesNames.ContainsKey(id)
                                            ? _branchesNames[id]
                                            : $"Sede {id}")
                                    .ToList();

                                string sedeProcesoNombre =
                                    _branchesNames != null && _branchesNames.ContainsKey(branchIdProceso)
                                        ? _branchesNames[branchIdProceso]
                                        : $"Sede {branchIdProceso}";

                                rowErrors.Add(
                                    $"El CI {ciDocente} existe en Civil pero en otras sedes ({string.Join(", ", otrasSedesNombres)}), " +
                                    $"no en la sede seleccionada ({sedeProcesoNombre}).");
                            }
                        }
                    }

                    // ======== VALIDACIÓN CAMPOS BÁSICOS DE PARALELO (campo por campo) ========
                    bool faltanCamposParalelo = false;

                    if (string.IsNullOrEmpty(codigoParalelo))
                    {
                        rowErrors.Add("El campo Codigo_Paralelo es obligatorio.");
                        faltanCamposParalelo = true;
                    }
                    if (string.IsNullOrEmpty(periodo))
                    {
                        rowErrors.Add("El campo Periodo es obligatorio.");
                        faltanCamposParalelo = true;
                    }
                    if (string.IsNullOrEmpty(sigla))
                    {
                        rowErrors.Add("El campo Sigla es obligatorio.");
                        faltanCamposParalelo = true;
                    }
                    if (string.IsNullOrEmpty(paralelo))
                    {
                        rowErrors.Add("El campo Paralelo es obligatorio.");
                        faltanCamposParalelo = true;
                    }
                    
                    // if (string.IsNullOrEmpty(unidadOrg)) { ... }
                    // if (string.IsNullOrEmpty(sede)) { ... }

                    // Si faltan campos, no tiene sentido validar en la tabla de paralelos
                    if (!faltanCamposParalelo)
                    {
                        // 1) Primero: validar que exista el Codigo_Paralelo (clave principal)
                        var candidatosCodigo = _paralelosDb
                            .Where(p => p.CODIGOSAP == codigoParalelo)
                            .ToList();

                        if (!candidatosCodigo.Any())
                        {
                            // Si no existe el código, ahí mismo paramos
                            rowErrors.Add(
                                $"No existe ningún registro en T_REG_PARALELOS_NS con Codigo_Paralelo = '{codigoParalelo}'.");
                        }
                        else
                        {
                            // 2) Validar cada campo (Periodo / Sigla / Paralelo) contra los registros de ese código
                            bool periodoOk = candidatosCodigo.Any(p => p.PERIODOSAP == periodo);
                            bool siglaOk = candidatosCodigo.Any(p => p.SIGLA == sigla);
                            bool paraleloOk = candidatosCodigo.Any(p => p.NUMPARALELO == paralelo);

                            if (!periodoOk)
                            {
                                rowErrors.Add(
                                    $"Para el Codigo_Paralelo '{codigoParalelo}' no existe ningún registro con Periodo = '{periodo}'.");
                            }

                            if (!siglaOk)
                            {
                                rowErrors.Add(
                                    $"Para el Codigo_Paralelo '{codigoParalelo}' no existe ningún registro con Sigla = '{sigla}'.");
                            }

                            if (!paraleloOk)
                            {
                                rowErrors.Add(
                                    $"Para el Codigo_Paralelo '{codigoParalelo}' no existe ningún registro con Paralelo = '{paralelo}'.");
                            }

                            /*
                            // 3) Validar la combinación completa (Periodo + Sigla + Paralelo)
                            var match = candidatosCodigo.FirstOrDefault(p =>
                                p.PERIODOSAP == periodo &&
                                p.SIGLA == sigla &&
                                p.NUMPARALELO == paralelo
                            );

                            if (match == null && periodoOk && siglaOk && paraleloOk)
                            {
                                rowErrors.Add(
                                    $"Para el Codigo_Paralelo '{codigoParalelo}' existen registros con el Periodo, la Sigla y el Paralelo indicados, " +
                                    "pero no en una misma combinación. Verifique que Periodo, Sigla y Paralelo correspondan al mismo paralelo en Saraí.");
                            }*/
                        }
                    }

                    // Si hay errores en esta fila -> los anotamos y NO agregamos la entidad
                    if (rowErrors.Any())
                    {
                        errors.Add(new ExcelError
                        {
                            RowNumber = rowNum,
                            Messages = rowErrors
                        });
                        continue;
                    }

                    // Si llegamos aquí, la fila es válida -> preparar entidad para DB
                    var asignacion = MapRowToAsignacion(row, columnIndexes);
                    asignacion.Id = AsignacionCarga.GetNextId(_context);
                    asignacion.AsigProcesoId = _proceso.Id;

                    _context.AsignacionesCarga.Add(asignacion);

                }

                // If we had errors, we do NOT add valid rows to DB
                if (errors.Any())
                {
                    foreach (var entry in _context.ChangeTracker.Entries()
                                 .Where(e => e.State == System.Data.Entity.EntityState.Added))
                    {
                        entry.State = System.Data.Entity.EntityState.Detached;
                    }

                    errorResponse = BuildExcelErrorResponse(workbook, errors);
                    return false;
                }

                // No errors -> save valid rows
                _context.SaveChanges();
                return true;
            }
        }



        private void LoadParalelosFromDb()
        {
            var sql = "";
            sql += "SELECT CODIGOSAP, SIGLA, NUMPARALELO, ";
            sql += "       CODUNIDADORGANIZACIONAL, SEDE, PERIODOSAP ";
            sql += "FROM ADMNAL.T_REG_PARALELOS_NS";

            _paralelosDb = _context.Database.SqlQuery<ParaleloRow>(sql).ToList();
        }

        private void LoadCivilesFromDb()
        {
            var sql = "";
            sql += "SELECT \"NIT\", \"BranchesId\" ";
            sql += "FROM ADMNALRRHH.\"Civil\"";

            _civilesDb = _context.Database.SqlQuery<CivilRow>(sql).ToList();
        }
        private void LoadBranchesNames()
        {
            // Ajusta "Branches" y "Name" si en tu modelo se llaman distinto
            _branchesNames = _context.Branch
                .ToDictionary(b => b.Id, b => b.Name);
        }



        private AsignacionCarga MapRowToAsignacion(IXLRow row, Dictionary<string, int> colIdx)
        {
            decimal horasSemana = 0;
            decimal horasMes = 0;
            decimal costoHora = 0;

            decimal.TryParse(row.Cell(colIdx["Horas_Semana"]).GetString(), out horasSemana);
            decimal.TryParse(row.Cell(colIdx["Horas_Mes"]).GetString(), out horasMes);
            decimal.TryParse(row.Cell(colIdx["Costo_Hora"]).GetString(), out costoHora);

            // Leemos los datos clave para buscar el paralelo en la tabla ADMNAL.T_REG_PARALELOS_NS
            var periodo = row.Cell(colIdx["Periodo"]).GetString().Trim();
            var sigla = row.Cell(colIdx["Sigla"]).GetString().Trim();
            var codigoParalelo = row.Cell(colIdx["Codigo_Paralelo"]).GetString().Trim();
            var paralelo = row.Cell(colIdx["Paralelo"]).GetString().Trim();

            // 1) Intento “ideal”: match por código + periodo + sigla + paralelo
            var paraleloDb = _paralelosDb.FirstOrDefault(p =>
                p.CODIGOSAP == codigoParalelo &&
                p.PERIODOSAP == periodo &&
                p.SIGLA == sigla &&
                p.NUMPARALELO == paralelo
            );

            // 2) Si por alguna razón no lo encuentra, hacemos un fallback solo por codigoParalelo
            if (paraleloDb == null)
            {
                paraleloDb = _paralelosDb.FirstOrDefault(p =>
                    p.CODIGOSAP == codigoParalelo
                );
            }

            string unidadOrgDb = paraleloDb?.CODUNIDADORGANIZACIONAL;
            string sedeDb = paraleloDb?.SEDE;

            var asignacion = new AsignacionCarga
            {
                // Id y AsigProcesoId los seteamos afuera, en ValidateFile
                CiDocente = row.Cell(colIdx["Ci_Docente"]).GetString().Trim(),
                PrimerApellido = row.Cell(colIdx["Primer_Apellido"]).GetString().Trim(),
                SegundoApellido = row.Cell(colIdx["Segundo_Apellido"]).GetString().Trim(),
                TercerApellido = row.Cell(colIdx["Tercer_Apellido"]).GetString().Trim(),
                Nombres = row.Cell(colIdx["Nombres"]).GetString().Trim(),

                Periodo = periodo,
                Sigla = sigla,
                CodigoParalelo = codigoParalelo,
                Paralelo = paralelo,

                // 🔹 Estos ya NO vienen del Excel, los sacamos de la tabla de paralelos:
                UnidadOrganizacional = unidadOrgDb,
                Sede = sedeDb,

                HorasSemana = horasSemana,
                HorasMes = horasMes,
                CostoHora = costoHora,

                NumeroContrato = null
            };

            return asignacion;
        }


        private HttpResponseMessage BuildSimpleErrorResponse(string msg)
        {
            var resp = new HttpResponseMessage(HttpStatusCode.BadRequest);
            var errorsJson = new JObject
            {
                ["Error"] = msg
            };
            resp.Headers.Add("UploadErrors", errorsJson.ToString(Newtonsoft.Json.Formatting.None));
            resp.Content = new StringContent(msg);
            return resp;
        }

        /// <summary>
        /// Devuelve un Excel con todas las filas erróneas marcadas y
        /// con una columna "Errores" que concatena todos los mensajes de la fila.
        /// </summary>
        private HttpResponseMessage BuildExcelErrorResponse(XLWorkbook originalWorkbook, List<ExcelError> errors)
        {
            var ws = originalWorkbook.Worksheet(1);

            // Asegurar columna de errores
            var lastCol = ws.LastColumnUsed().ColumnNumber() + 1;
            ws.Cell(_headerRowIndex, lastCol).Value = "Errores";

            // Marcar filas con error
            foreach (var e in errors)
            {
                var row = ws.Row(e.RowNumber);
                row.Style.Fill.BackgroundColor = XLColor.LightPink;

                var joined = string.Join(" | ", e.Messages);
                ws.Cell(e.RowNumber, lastCol).Value = joined;
            }

            var ms = new MemoryStream();
            originalWorkbook.SaveAs(ms);
            ms.Position = 0;

            var resp = new HttpResponseMessage(HttpStatusCode.BadRequest);
            var errorsJson = new JObject
            {
                ["Errores"] = JToken.FromObject(
                    errors.Select(x => new { x.RowNumber, x.Messages }))
            };

            resp.Headers.Add("UploadErrors", errorsJson.ToString(Newtonsoft.Json.Formatting.None));
            resp.Content = new StreamContent(ms);
            resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            resp.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
            {
                FileName = "Errores_Asignaciones.xlsx"
            };

            return resp;
        }
    }
}


