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
        public class ParaleloRow
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
            public int BranchesId { get; set; }

            // NUEVO (si aún no lo tenías)
            public string FullName { get; set; }

            // Ya lo tenías por el cambio anterior; lo puedes dejar, aunque no lo usemos todavía
            public int IsEnabled { get; set; }
        }



        private class ExcelError
        {
            public int RowNumber { get; set; }
            public List<string> Messages { get; set; }

            public ExcelError()
            {
                Messages = new List<string>();
            }
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
                        "Costo_Hora",
                        "Cantidad_Meses"
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
                    // NEW: Return Excel with missing columns highlighted
                    errorResponse = BuildMissingColumnsErrorResponse(workbook, missing, _headerRowIndex);
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
                    var primerApellido = row.Cell(columnIndexes["Primer_Apellido"]).GetString().Trim();
                    var segundoApellido = row.Cell(columnIndexes["Segundo_Apellido"]).GetString().Trim();
                    var tercerApellido = row.Cell(columnIndexes["Tercer_Apellido"]).GetString().Trim();
                    var nombres = row.Cell(columnIndexes["Nombres"]).GetString().Trim();


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
                                rowErrors.Add(string.Format("No existe ningún registro en Civil con NIT = {0}.", ciDocente));
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
                                                    : string.Format("Sede {0}", id))
                                            .ToList();

                                        return string.Format("{0} (Sedes: {1})", g.Key, string.Join(", ", sedesNombres));
                                    })
                                    .ToList();

                                rowErrors.Add(
                                    string.Format("No existe un NIT exactamente igual a {0} en Civil, ", ciDocente) +
                                    string.Format("pero se encontraron NIT similares: {0}. ", string.Join("; ", candidatosTexto)) +
                                    "Verifique si alguno de ellos corresponde al docente."
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
                                            : string.Format("Sede {0}", id))
                                    .ToList();

                                string sedeProcesoNombre =
                                    _branchesNames != null && _branchesNames.ContainsKey(branchIdProceso)
                                        ? _branchesNames[branchIdProceso]
                                        : string.Format("Sede {0}", branchIdProceso);

                                rowErrors.Add(
                                    string.Format("El CI {0} existe en Civil pero en otras sedes ({1}), no en la sede seleccionada ({2}).",
                                        ciDocente, string.Join(", ", otrasSedesNombres), sedeProcesoNombre));
                            }
                            else
                            {
                                // Aquí sí hay al menos un CivilRow con ese NIT y esa sede.
                                // Ahora validamos que el nombre coincida.

                                // Normalizador de nombres: mayúsculas + espacios colapsados
                                Func<string, string> normalize = s =>
                                {
                                    if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                                    return string.Join(" ",
                                        s.Trim()
                                         .ToUpperInvariant()
                                         .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                    );
                                };

                                // Excel: opción 1 -> Primer + Segundo + Tercer + Nombres
                                var excelFullName1 = normalize(
                                    string.Format("{0} {1} {2} {3}", primerApellido, segundoApellido, tercerApellido, nombres)
                                );

                                // Excel: opción 2 -> Segundo + Primer + Tercer + Nombres
                                var excelFullName2 = normalize(
                                    string.Format("{0} {1} {2} {3}", segundoApellido, primerApellido, tercerApellido, nombres)
                                );
                                // Excel: opción 3 -> Nombre + Primer + Segundo + Tercer
                                var excelFullName3 = normalize(
                                    string.Format("{0} {1} {2} {3}", nombres, primerApellido, segundoApellido, tercerApellido)
                                );


                                // Comparamos contra todos los Civil de esa sede
                                var coincidenciasNombre = civilesMismaSede
                                    .Where(c =>
                                    {
                                        var civilNameNorm = normalize(c.FullName ?? string.Empty);
                                        return civilNameNorm == excelFullName1
                                            || civilNameNorm == excelFullName2
                                            || civilNameNorm == excelFullName3;
                                    })
                                    .ToList();


                                if (!coincidenciasNombre.Any())
                                {
                                    // Nombres en Civil para ese CI + sede
                                    var nombresCivil = civilesMismaSede
                                        .Select(c => c.FullName)
                                        .Where(fn => !string.IsNullOrWhiteSpace(fn))
                                        .Distinct()
                                        .Take(3)
                                        .ToList();

                                    var listadoCivil = nombresCivil.Any()
                                        ? string.Join(", ", nombresCivil)
                                        : "(sin nombre registrado)";

                                    rowErrors.Add(
                                        string.Format("El CI {0} existe en Civil para la sede seleccionada, ", ciDocente) +
                                        string.Format("pero el nombre no coincide. En Civil figura como: {0}. ", listadoCivil) +
                                        "Verifique que el CI y los apellidos/nombres del Excel correspondan al mismo docente."
                                    );
                                }
                            }

                        }
                    }


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
                    // Inside the row iteration loop, add:
                    var cantidadMesesStr = row.Cell(columnIndexes["Cantidad_Meses"]).GetString().Trim();

                    // Validate it's a number
                    int cantidadMeses = 1; // default
                    if (string.IsNullOrEmpty(cantidadMesesStr))
                    {
                        rowErrors.Add("El campo Cantidad_Meses es obligatorio.");
                    }
                    else if (!int.TryParse(cantidadMesesStr, out cantidadMeses) || cantidadMeses < 1 || cantidadMeses > 12)
                    {
                        rowErrors.Add("El campo Cantidad_Meses debe ser un número entero entre 1 y 12.");
                    }
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
                                string.Format("No existe ningún registro en T_REG_PARALELOS_NS con Codigo_Paralelo = '{0}'.", codigoParalelo));
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
                                    string.Format("Para el Codigo_Paralelo '{0}' no existe ningún registro con Periodo = '{1}'.", codigoParalelo, periodo));
                            }

                            if (!siglaOk)
                            {
                                rowErrors.Add(
                                    string.Format("Para el Codigo_Paralelo '{0}' no existe ningún registro con Sigla = '{1}'.", codigoParalelo, sigla));
                            }

                            if (!paraleloOk)
                            {
                                rowErrors.Add(
                                    string.Format("Para el Codigo_Paralelo '{0}' no existe ningún registro con Paralelo = '{1}'.", codigoParalelo, paralelo));
                            }

                            // ======== VALIDACIÓN PERÍODO VS PERÍODO SELECCIONADO EN EL PROCESO ========
                            var periodoProceso = _proceso.PeriodoId; // ajusta el nombre si en tu modelo es distinto

                            if (!string.IsNullOrWhiteSpace(periodoProceso) &&
                                !string.IsNullOrWhiteSpace(periodo))
                            {
                                if (!string.Equals(periodo, periodoProceso, StringComparison.OrdinalIgnoreCase))
                                {
                                    rowErrors.Add(
                                        string.Format("El período '{0}' de la fila no coincide con el período seleccionado en el paso 1 ('{1}').", periodo, periodoProceso)
                                    );
                                }
                            }

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
            var sql = ""
                + "SELECT "
                + "\"NIT\", "
                + "\"BranchesId\", "
                + "\"FullName\", "
                + "\"IsEnabled\" "
                + "FROM ADMNALRRHH.\"Civil\"";

            _civilesDb = _context.Database.SqlQuery<CivilRow>(sql).ToList();
        }


        private void LoadBranchesNames()
        {
            // Ajusta "Branches" y "Name" si en tu modelo se llaman distinto
            _branchesNames = _context.Branch
                .ToDictionary(b => b.Id, b => b.Name);
        }
        // Asegura que las listas de DB estén cargadas
        private void EnsureDbCachesLoaded()
        {
            if (_paralelosDb == null)
            {
                LoadParalelosFromDb();
            }

            if (_civilesDb == null)
            {
                LoadCivilesFromDb();
            }

            if (_branchesNames == null)
            {
                LoadBranchesNames();
            }
        }

        // Helper público para reutilizar la misma lógica de búsqueda de paralelos
        public ParaleloRow FindParalelo(
            string codigoParalelo,
            string periodo,
            string sigla,
            string paralelo)
        {
            EnsureDbCachesLoaded();

            return _paralelosDb.FirstOrDefault(p =>
                p.CODIGOSAP == codigoParalelo &&
                p.PERIODOSAP == periodo &&
                p.SIGLA == sigla &&
                p.NUMPARALELO == paralelo
            );
        }


        private AsignacionCarga MapRowToAsignacion(IXLRow row, Dictionary<string, int> colIdx)
        {
            decimal horasSemana = 0;
            decimal horasMes = 0;
            decimal costoHora = 0;
            int cantidadMeses = 1;

            decimal.TryParse(row.Cell(colIdx["Horas_Semana"]).GetString(), out horasSemana);
            decimal.TryParse(row.Cell(colIdx["Horas_Mes"]).GetString(), out horasMes);
            decimal.TryParse(row.Cell(colIdx["Costo_Hora"]).GetString(), out costoHora);
            int.TryParse(row.Cell(colIdx["Cantidad_Meses"]).GetString(), out cantidadMeses);

            // Leemos los datos clave para buscar el paralelo en la tabla ADMNAL.T_REG_PARALELOS_NS
            var periodo = row.Cell(colIdx["Periodo"]).GetString().Trim();
            var sigla = row.Cell(colIdx["Sigla"]).GetString().Trim();
            var codigoParalelo = row.Cell(colIdx["Codigo_Paralelo"]).GetString().Trim();
            var paralelo = row.Cell(colIdx["Paralelo"]).GetString().Trim();

            // 1) Intento "ideal": match por código + periodo + sigla + paralelo
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

                // Estos ya NO vienen del Excel, los sacamos de la tabla de paralelos:
                UnidadOrganizacional = unidadOrgDb,
                Sede = sedeDb,

                HorasSemana = horasSemana,
                HorasMes = horasMes,
                CostoHora = costoHora,
                CantidadMeses = cantidadMeses,

                NumeroContrato = null
            };

            return asignacion;
        }

        // Dentro de AsignacionesExcel
        public List<string> ValidateRowValues(
    string ciDocente,
    string periodo,
    string sigla,
    string codigoParalelo,
    string paralelo,
    string primerApellido,
    string segundoApellido,
    string tercerApellido,
    string nombres
)
        {
            // Asegurar que las colecciones estén cargadas
            if (_paralelosDb == null)
                LoadParalelosFromDb();
            if (_civilesDb == null)
                LoadCivilesFromDb();
            if (_branchesNames == null || !_branchesNames.Any())
                LoadBranchesNames();
            var rowErrors = new List<string>();

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
                        rowErrors.Add(string.Format("No existe ningún registro en Civil con NIT = {0}.", ciDocente));
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
                                            : string.Format("Sede {0}", id))
                                    .ToList();

                                return string.Format("{0} (Sedes: {1})", g.Key, string.Join(", ", sedesNombres));
                            })
                            .ToList();

                        rowErrors.Add(
                            string.Format("No existe un NIT exactamente igual a {0} en Civil, ", ciDocente) +
                            string.Format("pero se encontraron NIT similares: {0}. ", string.Join("; ", candidatosTexto)) +
                            "Verifique si alguno de ellos corresponde al docente."
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
                                    : string.Format("Sede {0}", id))
                            .ToList();

                        string sedeProcesoNombre =
                            _branchesNames != null && _branchesNames.ContainsKey(branchIdProceso)
                                ? _branchesNames[branchIdProceso]
                                : string.Format("Sede {0}", branchIdProceso);

                        rowErrors.Add(
                            string.Format("El CI {0} existe en Civil pero en otras sedes ({1}), no en la sede seleccionada ({2}).",
                                ciDocente, string.Join(", ", otrasSedesNombres), sedeProcesoNombre));
                    }
                    else
                    {
                        // Aquí sí hay al menos un CivilRow con ese NIT y esa sede.
                        // Ahora validamos que el nombre coincida.

                        // Normalizador de nombres: mayúsculas + espacios colapsados
                        Func<string, string> normalize = s =>
                        {
                            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                            return string.Join(" ",
                                s.Trim()
                                 .ToUpperInvariant()
                                 .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            );
                        };

                        // Excel: opción 1 -> Primer + Segundo + Tercer + Nombres
                        var excelFullName1 = normalize(
                            string.Format("{0} {1} {2} {3}", primerApellido, segundoApellido, tercerApellido, nombres)
                        );

                        // Excel: opción 2 -> Segundo + Primer + Tercer + Nombres
                        var excelFullName2 = normalize(
                            string.Format("{0} {1} {2} {3}", segundoApellido, primerApellido, tercerApellido, nombres)
                        );

                        // Excel: opción 3 -> Nombres + Primer + Segundo + Tercer
                        var excelFullName3 = normalize(
                            string.Format("{0} {1} {2} {3}", nombres, primerApellido, segundoApellido, tercerApellido)
                        );

                        var coincidenciasNombre = civilesMismaSede
                            .Where(c =>
                            {
                                var civilNameNorm = normalize(c.FullName ?? string.Empty);
                                return civilNameNorm == excelFullName1
                                    || civilNameNorm == excelFullName2
                                    || civilNameNorm == excelFullName3;
                            })
                            .ToList();

                        if (!coincidenciasNombre.Any())
                        {
                            // Nombres en Civil para ese CI + sede
                            var nombresCivil = civilesMismaSede
                                .Select(c => c.FullName)
                                .Where(fn => !string.IsNullOrWhiteSpace(fn))
                                .Distinct()
                                .Take(3)
                                .ToList();

                            var listadoCivil = nombresCivil.Any()
                                ? string.Join(", ", nombresCivil)
                                : "(sin nombre registrado)";

                            rowErrors.Add(
                                string.Format("El CI {0} existe en Civil para la sede seleccionada, ", ciDocente) +
                                string.Format("pero el nombre no coincide. En Civil figura como: {0}. ", listadoCivil) +
                                "Verifique que el CI y los apellidos/nombres del Excel correspondan al mismo docente."
                            );
                        }
                    }
                }
            }

            // ======== VALIDACIÓN CAMPOS DE PARALELO / PERÍODO ========
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

            // Si faltan campos, no tiene sentido validar en la tabla de paralelos
            if (!faltanCamposParalelo)
            {
                // 1) Primero: validar que exista el Codigo_Paralelo (clave principal)
                var candidatosCodigo = _paralelosDb
                    .Where(p => p.CODIGOSAP == codigoParalelo)
                    .ToList();

                if (!candidatosCodigo.Any())
                {
                    rowErrors.Add(
                        string.Format("No existe ningún registro en T_REG_PARALELOS_NS con Codigo_Paralelo = '{0}'.", codigoParalelo));
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
                            string.Format("Para el Codigo_Paralelo '{0}' no existe ningún registro con Periodo = '{1}'.", codigoParalelo, periodo));
                    }

                    if (!siglaOk)
                    {
                        rowErrors.Add(
                            string.Format("Para el Codigo_Paralelo '{0}' no existe ningún registro con Sigla = '{1}'.", codigoParalelo, sigla));
                    }

                    if (!paraleloOk)
                    {
                        rowErrors.Add(
                            string.Format("Para el Codigo_Paralelo '{0}' no existe ningún registro con Paralelo = '{1}'.", codigoParalelo, paralelo));
                    }

                    // ======== VALIDACIÓN PERÍODO VS PERÍODO SELECCIONADO EN EL PROCESO ========
                    var periodoProceso = _proceso.PeriodoId; // ajusta el nombre si en tu modelo es distinto

                    if (!string.IsNullOrWhiteSpace(periodoProceso) &&
                        !string.IsNullOrWhiteSpace(periodo))
                    {
                        if (!string.Equals(periodo, periodoProceso, StringComparison.OrdinalIgnoreCase))
                        {
                            rowErrors.Add(
                                string.Format("El período '{0}' de la fila no coincide con el período seleccionado en el paso 1 ('{1}').", periodo, periodoProceso)
                            );
                        }
                    }
                }
            }

            return rowErrors;
        }




        private HttpResponseMessage BuildSimpleErrorResponse(string msg)
        {
            // Instead of returning plain text, return an Excel with the error message
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.AddWorksheet("Errores");

                // Header
                ws.Cell(1, 1).Value = "Error";
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.Red;
                ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;

                // Error message
                ws.Cell(2, 1).Value = msg;
                ws.Cell(2, 1).Style.Fill.BackgroundColor = XLColor.LightPink;

                // Auto-fit column
                ws.Column(1).Width = 80;

                var ms = new MemoryStream();
                workbook.SaveAs(ms);
                ms.Position = 0;

                var resp = new HttpResponseMessage(HttpStatusCode.BadRequest);

                var errorsJson = new JObject
                {
                    ["Error"] = msg
                };

                resp.Headers.Add("UploadErrors", errorsJson.ToString(Newtonsoft.Json.Formatting.None));
                resp.Content = new StreamContent(ms);
                resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                resp.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                {
                    FileName = "Errores_Columnas.xlsx"
                };

                return resp;
            }
        }

        private HttpResponseMessage BuildMissingColumnsErrorResponse(XLWorkbook originalWorkbook, List<string> missingColumns, int headerRowIndex)
        {
            var ws = originalWorkbook.Worksheet(1);

            // Add missing columns at the end
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;

            foreach (var colName in missingColumns)
            {
                lastCol++;
                var cell = ws.Cell(headerRowIndex, lastCol);
                cell.Value = colName;
                cell.Style.Fill.BackgroundColor = XLColor.Yellow;
                cell.Style.Font.Bold = true;

                // Add note in second row
                ws.Cell(headerRowIndex + 1, lastCol).Value = "← COLUMNA FALTANTE";
                ws.Cell(headerRowIndex + 1, lastCol).Style.Fill.BackgroundColor = XLColor.LightYellow;
            }

            // Add error message in a separate sheet
            var errorSheet = originalWorkbook.AddWorksheet("INSTRUCCIONES");
            errorSheet.Cell(1, 1).Value = "ERRORES ENCONTRADOS";
            errorSheet.Cell(1, 1).Style.Font.Bold = true;
            errorSheet.Cell(1, 1).Style.Font.FontSize = 14;
            errorSheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.Red;
            errorSheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;

            errorSheet.Cell(3, 1).Value = "Faltan las siguientes columnas obligatorias:";
            errorSheet.Cell(3, 1).Style.Font.Bold = true;

            for (int i = 0; i < missingColumns.Count; i++)
            {
                errorSheet.Cell(4 + i, 1).Value = string.Format("• {0}", missingColumns[i]);
                errorSheet.Cell(4 + i, 1).Style.Fill.BackgroundColor = XLColor.LightYellow;
            }

            errorSheet.Cell(6 + missingColumns.Count, 1).Value = "Las columnas faltantes han sido agregadas en amarillo en la hoja original.";
            errorSheet.Cell(7 + missingColumns.Count, 1).Value = "Por favor, complete los datos y vuelva a cargar el archivo.";

            errorSheet.Column(1).Width = 60;

            // Save to stream
            var ms = new MemoryStream();
            originalWorkbook.SaveAs(ms);
            ms.Position = 0;

            var resp = new HttpResponseMessage(HttpStatusCode.BadRequest);

            var errorsJson = new JObject
            {
                ["Error"] = "Faltan columnas obligatorias",
                ["MissingColumns"] = JToken.FromObject(missingColumns)
            };

            resp.Headers.Add("UploadErrors", errorsJson.ToString(Newtonsoft.Json.Formatting.None));
            resp.Content = new StreamContent(ms);
            resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            resp.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
            {
                FileName = "Errores_Columnas_Faltantes.xlsx"
            };

            return resp;
        }

        /// Devuelve un Excel con todas las filas erróneas marcadas y
        /// con una columna "Errores" que concatena todos los mensajes de la fila.

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


