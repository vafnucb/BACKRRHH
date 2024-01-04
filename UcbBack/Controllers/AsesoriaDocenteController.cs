using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Data;
using System.Data.Entity;
using UcbBack.Logic;
using UcbBack.Models;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using UcbBack.Models.Not_Mapped.ViewMoldes;
using ClosedXML.Excel;
using System.Net.Http.Headers;
using UcbBack.Models.Not_Mapped;
using System.IO;
using UcbBack.Logic.B1;
using Newtonsoft.Json.Linq;
using System.Configuration;
using UcbBack.Logic.ExcelFiles;
using UcbBack.Models.Dist;
using System.Diagnostics;
using UcbBack.Logic.ExcelFiles.Serv;
using System.Globalization;

namespace UcbBack.Controllers
{
    public class AsesoriaDocenteController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidateAuth auth;
        private B1Connection B1;

        public AsesoriaDocenteController()
        {
            _context = new ApplicationDbContext();
            B1 = B1Connection.Instance();
            auth = new ValidateAuth();
        }
        // convertir a mes literal
        public List<AsesoriaDocenteViewModel> mesLiteral(string query)
        {
            string[] _months = {
                        "ENE",
                        "FEB",
                        "MAR",
                        "ABR",
                        "MAY",
                        "JUN",
                        "JUL",
                        "AGO",
                        "SEP",
                        "OCT",
                        "NOV",
                        "DIC"
                    };
            // Mes a literal
            var rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(query).ToList();
            List<AsesoriaDocenteViewModel> list = new List<AsesoriaDocenteViewModel>();
            foreach (var element in rawresult)
            {
                if (element.Mes >= 1 && element.Mes <= _months.Length)
                {
                    element.MesLiteral = _months[element.Mes - 1];
                }
                else
                {
                    // Manejar el caso cuando Mes está fuera del rango válido
                    element.MesLiteral = "Mes no válido";
                }
                list.Add(element);
            };
            return list;
        }

        //registro por Id
        [HttpGet]
        [Route("api/AsesoriaDocente/{id}")]
        public IHttpActionResult getIndividualRecord(int id)
        {
            //datos para la tabla histórica
            var uniqueRecord = _context.AsesoriaDocente.FirstOrDefault(x => x.Id == id);
            if (uniqueRecord == null)
            {
                return BadRequest("Ese registro no existe");
            }
            else
            {
                return Ok(uniqueRecord);
            }
        }

        // obtener registros de tutorias segun su estado
        [HttpGet]
        [Route("api/AsesoriaDocente")]
        public IHttpActionResult getAsesoria([FromUri] string by)
        {
            // datos para la tabla histórica
            string query = "select a.\"Id\",a.\"TeacherFullName\", a.\"TeacherCUNI\", a.\"TeacherBP\", a.\"Categoría\", " +
                                "case when (a.\"Acta\") is null or (a.\"Acta\")='' then 'S/N' when (a.\"Acta\") is not null then a.\"Acta\" end as \"Acta\", a.\"ActaFecha\", a.\"BranchesId\", br.\"Abr\" as \"Regional\", a.\"Carrera\", a.\"DependencyCod\", a.\"Horas\", " +
                                "a.\"MontoHora\", a.\"TotalNeto\", a.\"TotalBruto\", a.\"StudentFullName\", a.\"Mes\", a.\"Gestion\", " +
                                "a.\"Observaciones\", a.\"Deduccion\", t.\"Abr\" as \"TipoTarea\", tm.\"Abr\" as \"Modalidad\", null as \"MesLiteral\", a.\"Origen\"," +
                                " case when a.\"Ignore\" = true then 'D' when a.\"Ignore\" = false then '' end as \"Ignore\"" +
                                "from " + CustomSchema.Schema + ".\"AsesoriaDocente\" a " +
                                "inner join " + CustomSchema.Schema + ".\"TipoTarea\" t " +
                                "on a.\"TipoTareaId\"=t.\"Id\" " +
                                "inner join " + CustomSchema.Schema + ".\"Modalidades\" tm " +
                                "on a.\"ModalidadId\"=tm.\"Id\" " +
                                "inner join " + CustomSchema.Schema + ".\"Branches\" br " +
                                "on a.\"BranchesId\"=br.\"Id\" ";
            string orderBy = "order by a.\"Gestion\" desc, a.\"Mes\" desc, a.\"Id\" asc, a.\"Carrera\" asc, a.\"TeacherCUNI\" asc ";
            var rawresult = new List<AsesoriaDocenteViewModel>();
            var user = auth.getUser(Request);

            if (by.Equals("APROBADO"))
            {
                string customQuery = query + "where a.\"Estado\"='APROBADO' " + orderBy;
                // Mes a literal
                rawresult = mesLiteral(customQuery);
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new { x.Id, x.Origen, x.Acta, x.Carrera, Profesor = x.TeacherFullName, Estudiante = x.StudentFullName, Tarea = x.TipoTarea, x.Gestion, x.Mes });
                return Ok(filteredList);

            }
            else if (by.Equals("PRE-APROBADO"))
            {
                string customQuery = query + "where a.\"Estado\"='PRE-APROBADO' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Acta,
                        x.Carrera,
                        x.StudentFullName,
                        x.TipoTarea,
                        x.Modalidad,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        Duplicado = x.Ignore
                    }); ;
                return Ok(filteredList);

            }
            else if (by.Equals("REGISTRADO-DEP"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Origen\"='DEPEN' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Acta,
                        x.Carrera,
                        x.StudentFullName,
                        x.TipoTarea,
                        x.Modalidad,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        Duplicado = x.Ignore
                    }); ; ;
                return Ok(filteredList);

            }
            else if (by.Equals("REGISTRADO-INDEP"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Origen\"='INDEP' and a.\"Factura\"=false " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Acta,
                        x.Carrera,
                        x.StudentFullName,
                        x.TipoTarea,
                        x.Modalidad,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        Duplicado = x.Ignore
                    }); ; ;
                return Ok(filteredList);
            }
            else if (by.Equals("REGISTRADO-OR"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Origen\"='OR' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Acta,
                        x.Carrera,
                        x.StudentFullName,
                        x.TipoTarea,
                        x.Modalidad,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        Duplicado = x.Ignore
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("REGISTRADO-FAC"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Factura\"= true " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Acta,
                        x.Carrera,
                        x.StudentFullName,
                        x.TipoTarea,
                        x.Modalidad,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        Duplicado = x.Ignore
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("REGISTRADO-EXT"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Origen\"='EXT' and a.\"Factura\"=false " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Acta,
                        x.Carrera,
                        x.StudentFullName,
                        x.TipoTarea,
                        x.Modalidad,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        Duplicado = x.Ignore
                    }); ; ;
                return Ok(filteredList);
            }
            else if (by.Equals("VERIFICADO-INDEP"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='VERIFICADO' " + "and a.\"Origen\"='INDEP' and a.\"Factura\"=false " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Acta,
                        x.Carrera,
                        x.StudentFullName,
                        x.TipoTarea,
                        x.Modalidad,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        Duplicado = x.Ignore
                    }); ; ;
                return Ok(filteredList);
            }
            else if (by.Equals("VERIFICADO-OR"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='VERIFICADO' " + "and a.\"Origen\"='OR' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Acta,
                        x.Carrera,
                        x.StudentFullName,
                        x.TipoTarea,
                        x.Modalidad,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        Duplicado = x.Ignore
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("VERIFICADO-DEP"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='VERIFICADO' " + "and a.\"Origen\"='DEPEN' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Acta,
                        x.Carrera,
                        x.StudentFullName,
                        x.TipoTarea,
                        x.Modalidad,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        Duplicado = x.Ignore
                    }); ; ;
                return Ok(filteredList);

            }
            else if (by.Equals("VERIFICADO-FAC"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='VERIFICADO' " + "and a.\"Factura\"= true " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Acta,
                        x.Carrera,
                        x.StudentFullName,
                        x.TipoTarea,
                        x.Modalidad,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        Duplicado = x.Ignore
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("VERIFICADO-EXT"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='VERIFICADO' " + "and a.\"Origen\"='EXT' and a.\"Factura\"=false " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Acta,
                        x.Carrera,
                        x.StudentFullName,
                        x.TipoTarea,
                        x.Modalidad,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        Duplicado = x.Ignore
                    }); ; ;
                return Ok(filteredList);
            }
            else
            {
                // Si no es ninguno de los estados existentes sale error
                return BadRequest();
            }
        }

        // conseguir los registros del docente por nombre completo, esto se debe a que no todos los registros tienen cuni o socio de negocio
        [HttpGet]
        [Route("api/TeacherStudent/{id}")]
        public IHttpActionResult TeachingRecords(int id)
        {
            var record = _context.AsesoriaDocente.FirstOrDefault(x => x.Id == id).TeacherFullName;
            //muestra los registros aprobados del docente X
            var query = "select a.*, t.\"Abr\" as \"TipoTarea\", tm.\"Abr\" as \"Modalidad\" from " + CustomSchema.Schema + ".\"AsesoriaDocente\" a " +
                        "inner join " + CustomSchema.Schema + ".\"TipoTarea\" t " +
                             "on a.\"TipoTareaId\"=t.\"Id\" " +
                        "inner join " + CustomSchema.Schema + ".\"Modalidades\" tm " +
                             "on a.\"ModalidadId\"=tm.\"Id\" " +
                        "where " +
                        "  \"TeacherFullName\"= '" + record + "' " +
                        "   and \"Estado\"='APROBADO' " +
                        "order by a.\"Gestion\" desc, a.\"Mes\" desc, a.\"Carrera\" asc, a.\"TeacherCUNI\" asc ";
            var allTeachingRecords = mesLiteral(query).Select(x => new { x.Id, x.Origen, x.Modalidad, x.TipoTarea, x.Carrera, x.Horas, x.MontoHora, x.TotalBruto, x.Deduccion, x.TotalNeto, Estudiante = x.StudentFullName, x.MesLiteral, x.Gestion });

            return Ok(allTeachingRecords);
        }

        // lista de docentes para el registro
        [HttpGet]
        [Route("api/DocentesList")]
        public IHttpActionResult DocentesList()
        {
            // Hacer un union con los docentes que no sean indepedientes, es decir que sean de civil nomas, por su jobTitle
            var activeDocentes = _context.Database.SqlQuery<AsesoriaTeachers>("(select lc.\"CUNI\", fn.\"FullName\", lc.\"BranchesId\", true as \"TipoPago\", pe.\"Categoria\", b.\"Abr\" \"Regional\", ca.\"Precio\"" +
            "\r\nfrom " + CustomSchema.Schema + ".\"LASTCONTRACTS\" lc " +
            "\r\ninner join " + CustomSchema.Schema + ".\"FullName\" fn on fn.\"CUNI\" = lc.\"CUNI\" " +
            "\r\ninner join " + CustomSchema.Schema + ".\"People\" pe on pe.\"CUNI\" = lc.\"CUNI\"" +
            "\r\ninner join " + CustomSchema.Schema + ".\"Categoria\" ca on ca.\"Cat\" = pe.\"Categoria\"" +
            "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" b on b.\"Id\" = lc.\"BranchesId\"\r\nwhere pe.\"Categoria\" is not null " +
            "\r\ngroup by lc.\"CUNI\", fn.\"FullName\",lc.\"FullName\", lc.\"BranchesId\",pe.\"Categoria\",b.\"Abr\",ca.\"Precio\")" +
            // aquí juntamos a las personas de ADMNALRHH con los profesores independientes, es decir que estan como socios de negocio
            " UNION ALL " +
            " (select cv.\"SAPId\" as \"CUNI\",ocrd.\"CardName\" \"FullName\", br.\"Id\" as \"BranchesId\",  false as \"TipoPago\", cv.\"Categoria\", br.\"Abr\" \"Regional\", 0 \"Precio\"" +
            "\r\nfrom " + CustomSchema.Schema + ".\"Civil\" cv " +
            "\r\ninner join  " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".OCRD on cv.\"SAPId\" = ocrd.\"CardCode\" " +
            "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on cv.\"BranchesId\" = br.\"Id\"" +
            "\r\nwhere ocrd.\"frozenFor\" = 'N'" +
            "\r\ngroup by cv.\"SAPId\",ocrd.\"CardName\", br.\"Id\", cv.\"Categoria\",br.\"Abr\" )" +
            "\r\norder by \"FullName\""
            // "where oh.\"jobTitle\" like '%DOCENTE%' "
            ).ToList();


            var user = auth.getUser(Request);

            var filteredList = auth.filerByRegional(activeDocentes.AsQueryable(), user);

            return Ok(filteredList);
        }

        [HttpGet]
        [Route("api/DocentesListAll")]
        public IHttpActionResult DocentesListAll()
        {
            //Hacer un union con los docentes que no sean indepedientes, es decir que sean de civil nomas, por su jobTitle
            var activeDocentes = _context.Database.SqlQuery<AsesoriaTeachers>("(select lc.\"CUNI\", fn.\"FullName\",lc.\"StartDate\", lc.\"EndDate\", lc.\"BranchesId\", true as \"TipoPago\", pe.\"Categoria\", br.\"Abr\" \"Regional\"  " +
            "from " + CustomSchema.Schema + ".\"ContractDetail\" lc " +
            "inner join " + CustomSchema.Schema + ".\"FullName\" fn " +
            "on fn.\"CUNI\"=lc.\"CUNI\" " +
            "inner join " + CustomSchema.Schema + ".\"People\" pe  " +
            "on pe.\"CUNI\"=lc.\"CUNI\" " +
            "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" b on b.\"Id\" = lc.\"BranchesId\"  " +
            "where pe.\"Categoria\" is not null " +
            //aquí juntamos a las personas de ADMNALRHH con los profesores independientes, es decir que estan como socios de negocio
            "UNION ALL " +
            "(select cv.\"SAPId\" as \"CUNI\",ocrd.\"CardName\" \"FullName\"," +
            " null as \"StartDate\", null as \"EndDate\", br.\"Id\" as \"BranchesId\", " +
            "false as \"TipoPago\", cv.\"Categoria\" , br.\"Abr\" \"Regional\"  " +
            "\r\nfrom " + CustomSchema.Schema + ".\"Civil\" cv " +
            "\r\ninner join  " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".CRD8 \r\non cv.\"SAPId\" = crd8.\"CardCode\" " +
            "\r\ninner join  " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".OCRD ocrd\r\non cv.\"SAPId\" = ocrd.\"CardCode\" " +
            "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" b on b.\"Id\" = cv.\"BranchesId\"  " +
            "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br \r\non crd8.\"BPLId\"=br.\"CodigoSAP\"" +
            "\r\nwhere ocrd.\"frozenFor\" = 'N') " +
            "order by \"FullName\" "
            //"where oh.\"jobTitle\" like '%DOCENTE%' "
            ).ToList();


            return Ok(activeDocentes);
        }

        [HttpGet]
        [Route("api/PDFReportBody")]
        public IHttpActionResult PDFReport([FromUri] string part)
        {
            string query = "";
            var report = new List<AsesoriaDocenteViewModel>();
            string[] data = part.Split(';');
            string section = data[0];
            string state = data[1];
            string origin = data[2];
            string qOrigen = "";
            // query para generar todos los datos de cada docente, ordenado por carrera y docente
            if (origin == "FAC")
            {
                qOrigen = " and a.\"Origen\" = 'INDEP' and a.\"Factura\" = true ";
            }
            else
            {
                qOrigen = " and a.\"Origen\" like '%" + origin + "%' and a.\"Factura\" = false ";
            }
            switch (section)
            {
                case "Body":
                    // obtiene el cuerpo de la tabla para el PDF
                    // join para el nombre de la carrera
                    query = "select " +
                            "\"TeacherFullName\", \"Origen\", " +
                            "m.\"Abr\" as \"Modalidad\", " +
                            "t.\"Abr\" as \"TipoTarea\", " +
                            "a.\"Carrera\" ||" + " ' ' " + "|| op.\"PrcName\" as \"Carrera\" " + ", \"StudentFullName\" , " +
                            "\"Acta\", \"ActaFecha\" , " +
                            "\"Horas\", \"MontoHora\", " +
                            "\"TotalBruto\" , " +
                            "\"Deduccion\" , " +
                            "\"NumeroContrato\" , " +
                            "tp.\"Nombre\" as \"TipoPago\", " +
                            "case when \"IUE\" is null then 0 else \"IUE\" end as \"IUE\", " +
                            "case when \"IT\" is null then 0 else \"IT\" end as \"IT\", " +
                            "case when \"IUEExterior\" is null then 0 else \"IUEExterior\" end as \"IUEExterior\", " +
                            "\"TotalNeto\" , " +
                            "\"Observaciones\", \"BranchesId\", " +
                            "case when a.\"Ignore\" = true then 'D' when a.\"Ignore\" = false then '' end as \"Ignore\" " +
                        "from " +
                            CustomSchema.Schema + ".\"AsesoriaDocente\" a " +
                        "inner join " +
                            CustomSchema.Schema + ".\"TipoTarea\" t " +
                            "on a.\"TipoTareaId\"=t.\"Id\" " +
                        "inner join " +
                            CustomSchema.Schema + ".\"TipoPago\" tp " +
                            "on a.\"TipoPago\"=tp.\"Id\" " +
                        "inner join " +
                            CustomSchema.Schema + ".\"Modalidades\" m " +
                            "on a.\"ModalidadId\"=m.\"Id\" " +
                        "inner join " +
                            ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"OPRC\" op " +
                            "on a.\"Carrera\"= op.\"PrcCode\" " +
                        "where " +
                            "a.\"Estado\"='" + state + "' " +
                            qOrigen +
                            "and op.\"DimCode\" = 3 " +
                        "order by \"Carrera\", a.\"Id\", \"TeacherFullName\" asc";
                    Debug.WriteLine("FinalResult Query: " + query);
                    report = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(query).ToList();
                    break;

                case "Results":
                    // obtiene los resultados al pie de cada tabla, por carrera
                    query = "select " +
                            "(a.\"Carrera\" ||" + " ' ' " + "|| op.\"PrcName\") as \"Carrera\", " +
                            "sum(\"TotalBruto\") as \"TotalBruto\", " +
                            "sum(\"Deduccion\") as \"Deduccion\", " +
                            "case when sum(\"IUE\") is null then 0 else sum(\"IUE\") end as \"IUE\",  " +
                            "case when sum(\"IT\") is null then 0 else sum(\"IT\") end as \"IT\", " +
                            "case when sum(\"IUEExterior\") is null then 0 else sum(\"IUEExterior\") end as \"IUEExterior\", " +
                            "sum(\"TotalNeto\") as \"TotalNeto\", \"BranchesId\" " +
                        "from " +
                            CustomSchema.Schema + ".\"AsesoriaDocente\" a " +
                        "inner join " +
                            ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"OPRC\" op " +
                            "on a.\"Carrera\"= op.\"PrcCode\" " +
                        "where " +
                            "\"Estado\"='" + state + "' " +
                            qOrigen +
                        "group by \"Carrera\", \"PrcName\", \"BranchesId\" " +
                        "order by \"Carrera\" ";
                    Debug.WriteLine("FinalResultr Query: " + query);
                    report = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(query).ToList();
                    break;

                case "FinalResult":
                    // obtiene los resultados al pie de cada tabla, por carrera
                    query = "select " +
                            "sum(\"TotalBruto\") as \"TotalBruto\", " +
                            "sum(\"Deduccion\") as \"Deduccion\", " +
                            "case when sum(\"IUE\") is null then 0 else sum(\"IUE\") end as \"IUE\",  " +
                            "case when sum(\"IT\") is null then 0 else sum(\"IT\") end as \"IT\", " +
                            "case when sum(\"IUEExterior\") is null then 0 else sum(\"IUEExterior\") end as \"IUEExterior\", " +
                            "sum(\"TotalNeto\") as \"TotalNeto\", \"BranchesId\" " +
                        "from " +
                            CustomSchema.Schema + ".\"AsesoriaDocente\" a " +

                            "inner join " + CustomSchema.Schema + ".\"Branches\" b " +
                            "on b.\"Id\" = a.\"BranchesId\" " +
                        "where " +
                            "\"Estado\"='" + state + "' " +
                            qOrigen +
                        "group by \"BranchesId\", \"Origen\" ";
                    report = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(query).ToList();
                    break;

                default:
                    return BadRequest();
            }
            //Filtro de datos por regional
            var user = auth.getUser(Request);
            if (section.Equals("Body"))
            {
                var filteredListBody = auth.filerByRegional(report.AsQueryable(), user).ToList().Select(x => new
                {
                    Carrera = x.Carrera,
                    Docente = x.TeacherFullName,
                    Origen = x.Origen,
                    Categ = x.Categoría,
                    Modal = x.Modalidad,
                    Tarea = x.TipoTarea,
                    Alumno = x.StudentFullName,
                    Acta = x.Acta,
                    Fecha = x.ActaFecha != null ? x.ActaFecha.ToString("dd-MM-yyyy") : null,
                    x.NumeroContrato,
                    x.TipoPago,
                    Horas = x.Horas,
                    Costo_Hora = x.MontoHora,
                    Total_Bruto = x.TotalBruto,
                    Deduccion = x.Deduccion,
                    IUE = x.IUE,
                    IT = x.IT,
                    IUEExt = x.IUEExterior,
                    Total_Neto = x.TotalNeto,
                    Observaciones = x.Observaciones,
                    Dup = x.Ignore
                });

                return Ok(filteredListBody);
            }
            else if (section.Equals("Results"))
            {
                var filteredListResult = auth.filerByRegional(report.AsQueryable(), user).ToList().Select(x => new
                {
                    Carrera = x.Carrera,
                    Total_Bruto = x.TotalBruto,
                    Deduccion = x.Deduccion,
                    IUE = x.IUE,
                    IT = x.IT,
                    IUEExt = x.IUEExterior,
                    Total_Neto = x.TotalNeto,
                    x.BranchesId
                });
                return Ok(filteredListResult);
            }
            else
            {
                var filteredListResult = auth.filerByRegional(report.AsQueryable(), user).ToList().Select(x => new
                {
                    Total_Bruto = x.TotalBruto,
                    Deduccion = x.Deduccion,
                    IUE = x.IUE,
                    IT = x.IT,
                    IUEExt = x.IUEExterior,
                    Total_Neto = x.TotalNeto,
                    Origen = x.Origen
                });
                return Ok(filteredListResult);
            }
        }

        //para obtener el cuerpo del reporte PDF
        [HttpGet]
        [Route("api/PDFReportDocente")]
        public IHttpActionResult PDFReportDocente([FromUri] string part)
        {
            string query = "";
            var report = new List<AsesoriaDocenteViewModel>();
            string[] data = part.Split(';');
            string section = data[0];
            string state = data[1];
            string origin = data[2];
            //query para generar todos los datos de cada docente, ordenado por carrera y docente
            switch (section)
            {
                case "Body":
                    //obtiene el cuerpo de la tabla para el PDF
                    //join para el nombre de la carrera
                    query = "select " +
                            "\"TeacherFullName\", " +
                            "m.\"Abr\" as \"Modalidad\", " +
                            "t.\"Abr\" as \"TipoTarea\", " +
                            "a.\"Carrera\" ||" + " ' ' " + "|| op.\"PrcName\" as \"Carrera\" " + ", \"StudentFullName\" , " +
                            "\"Acta\", \"ActaFecha\" , " +
                            "\"Horas\", \"MontoHora\", " +
                            "\"TotalBruto\" , " +
                            "\"Deduccion\" , " +
                            "\"IUE\" , " +
                            "\"IT\" , " +
                            "\"IUEExterior\" , " +
                            "\"TotalNeto\" , " +
                            "\"Observaciones\", \"BranchesId\", " +
                            " case when a.\"Ignore\" = true then 'D' when a.\"Ignore\" = false then '' end as \"Ignore\"" +
                        "from " +
                            CustomSchema.Schema + ".\"AsesoriaDocente\" a " +
                        "inner join " +
                            CustomSchema.Schema + ".\"TipoTarea\" t " +
                            "on a.\"TipoTareaId\"=t.\"Id\" " +
                        "inner join " +
                            CustomSchema.Schema + ".\"Modalidades\" m " +
                            "on a.\"ModalidadId\"=m.\"Id\" " +
                        "inner join " +
                            ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"OPRC\" op " +
                            "on a.\"Carrera\"= op.\"PrcCode\" " +
                        "where " +
                            "a.\"Estado\"='" + state + "' " +
                            "and a.\"Origen\" like '%" + origin + "%'" +
                            "and op.\"DimCode\" = 3 " +
                        "order by \"TeacherFullName\",  \"Carrera\" ";
                    report = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(query).ToList();
                    break;

                case "Results":
                    //obtiene los resultados al pie de cada tabla, por carrera
                    query = "select " +
                            "a.\"TeacherFullName\", " +
                            "sum(\"TotalBruto\") as \"TotalBruto\", " +
                            "sum(\"Deduccion\") as \"Deduccion\", " +
                            "sum(\"IUE\") as \"IUE\", " +
                            "sum(\"IT\") as \"IT\", " +
                            "sum(\"IUEExterior\") as \"IUEExterior\", " +
                            "sum(\"TotalNeto\") as \"TotalNeto\", \"BranchesId\" " +
                        "from " +
                            CustomSchema.Schema + ".\"AsesoriaDocente\" a " +
                        "inner join " +
                            ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"OPRC\" op " +
                            "on a.\"Carrera\"= op.\"PrcCode\" " +
                        "where " +
                            "\"Estado\"='" + state + "' " +
                            "and a.\"Origen\" like '%" + origin + "%'" +
                        "group by a.\"TeacherFullName\", \"BranchesId\" " +
                        "order by a.\"TeacherFullName\" ";
                    report = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(query).ToList();
                    break;

                case "FinalResult":
                    //obtiene los resultados al pie de cada tabla, por carrera
                    query = "select " +
                            "sum(\"TotalBruto\") as \"TotalBruto\", " +
                            "sum(\"Deduccion\") as \"Deduccion\", " +
                            "sum(\"IUE\") as \"IUE\", " +
                            "sum(\"IT\") as \"IT\", " +
                            "sum(\"IUEExterior\") as \"IUEExterior\", " +
                            "sum(\"TotalNeto\") as \"TotalNeto\", \"BranchesId\" " +
                        "from " +
                            CustomSchema.Schema + ".\"AsesoriaDocente\" a " +
                        "where " +
                            "\"Estado\"='" + state + "' " +
                            "and a.\"Origen\" like '%" + origin + "%'" +
                        "group by \"BranchesId\" ";
                    report = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(query).ToList();
                    break;

                default:
                    return BadRequest();
            }
            //Filtro de datos por regional
            var user = auth.getUser(Request);
            if (section.Equals("Body"))
            {
                var filteredListBody = auth.filerByRegional(report.AsQueryable(), user).ToList().Select(x => new
                {
                    Carrera = x.Carrera,
                    Docente = x.TeacherFullName,
                    Categ = x.Categoría,
                    Modal = x.Modalidad,
                    Tarea = x.TipoTarea,
                    Alumno = x.StudentFullName,
                    Acta = x.Acta,
                    Fecha = x.ActaFecha != null ? x.ActaFecha.ToString("dd-MM-yyyy") : null,
                    Horas = x.Horas,
                    Costo_Hora = x.MontoHora,
                    Total_Bruto = x.TotalBruto,
                    Deduccion = x.Deduccion,
                    IUE = x.IUE,
                    IT = x.IT,
                    IUEExt = x.IUEExterior,
                    Total_Neto = x.TotalNeto,
                    Observaciones = x.Observaciones,
                    x.Ignore
                });

                return Ok(filteredListBody);
            }
            else if (section.Equals("Results"))
            {
                var filteredListResult = auth.filerByRegional(report.AsQueryable(), user).ToList().Select(x => new
                {
                    Docente = x.TeacherFullName,
                    Total_Bruto = x.TotalBruto,
                    Deduccion = x.Deduccion,
                    IUE = x.IUE,
                    IT = x.IT,
                    IUEExt = x.IUEExterior,
                    Total_Neto = x.TotalNeto,
                });
                return Ok(filteredListResult);
            }
            else
            {
                var filteredListResult = auth.filerByRegional(report.AsQueryable(), user).ToList().Select(x => new
                {
                    Total_Bruto = x.TotalBruto,
                    Deduccion = x.Deduccion,
                    IUE = x.IUE,
                    IT = x.IT,
                    IUEExt = x.IUEExterior,
                    Total_Neto = x.TotalNeto,
                });
                return Ok(filteredListResult);
            }
        }

        //para generar el archivo PREGRADO de SALOMON
        [HttpGet]
        [Route("api/ToPregradoFile")]
        public HttpResponseMessage ToPregradoFile([FromUri] string data)
        {
            string[] info = data.Split(';');
            int segmentoId = Convert.ToInt16(info[0]);
            string segmento = _context.Branch.FirstOrDefault(x => x.Id == segmentoId).Abr;
            string mes = (info[1]);
            string gestion = info[2];
            var Auxdate = new DateTime(
                Int32.Parse(gestion),
                Int32.Parse(mes) > 12 ? (Int32.Parse(mes) - 12) : Int32.Parse(mes),
                DateTime.DaysInMonth(Int32.Parse(gestion), Int32.Parse(mes) > 12 ? (Int32.Parse(mes) - 12) : Int32.Parse(mes))
            );
            string valid = "select  \r\n\"Document\" ,\"FirstSurName\", \"SecondSurName\",  \"Names\", \"MariedSurName\", '' \"Version\",sum(\"TotalNeto\") as \"TotalNeto\", \"Dependency\", \"CUNI\", '' \"PeriodoAcademico\"" +
                                "\r\nfrom( \r\nselect \r\n    p.\"Document\" ,p.\"FirstSurName\", p.\"SecondSurName\", " +
                                "\r\n    p.\"Names\", p.\"MariedSurName\"," +
                                "\r\n    a.\"TotalNeto\", t.\"Tarea\" \"TipoTarea\",\r\n    a.\"TeacherCUNI\" as \"CUNI\", a.\"DependencyCod\" as \"Dependency\", a.\"BranchesId\" " +
                                "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaDocente\" a \r" +
                                "\n    inner join " + CustomSchema.Schema + ".\"People\" p " +
                                "\r\n    on a.\"TeacherCUNI\"=p.\"CUNI\" " +
                                "\r\n    inner join " + CustomSchema.Schema + ".\"LASTCONTRACTS\" lc " +
                                "\r\n    on a.\"TeacherCUNI\"=lc.\"CUNI\" " +
                                "\r\n    inner join " + CustomSchema.Schema + ".\"Branches\" br " +
                                "\r\n    on a.\"BranchesId\"=br.\"Id\" " +
                                "\r\n    inner join " + CustomSchema.Schema + ".\"TipoTarea\" t" +
                                "\r\n    on a.\"TipoTareaId\"=t.\"Id\" \r\nwhere " +
                                "\r\n    a.\"Estado\"='PRE-APROBADO' " +
                                "\r\n    and br.\"Abr\" ='" + segmento + "'" +
                                "\r\n    and a.\"Origen\"='DEPEN' " +
                                "\r\n   and (lc.\"EndDate\" is not null and lc.\"EndDate\" < '" + Auxdate.Year + "-" + Auxdate.Month + "-01')" +
                                "\r\norder by a.\"Id\" desc) " +
                                "\r\n group by \"Document\" ,\"FirstSurName\", \"SecondSurName\",  \"Names\", \"MariedSurName\", \"CUNI\", \"Dependency\", \"BranchesId\" , \"TipoTarea\"\r\n order by \"FirstSurName\"";

            var valido = _context.Database.SqlQuery<DistPregradoViewModel>(valid).ToList();
            string aux = "";
            if (valido.Count > 0)
            {
                for (int i = 0; i < valido.Count; i++)
                {
                    aux = aux + valido[i].Names + " " + valido[i].FirstSurName + " " + valido[i].SecondSurName + ",";
                }
                HttpResponseMessage response =
                    new HttpResponseMessage(HttpStatusCode.InternalServerError);
                response.Content = new StringContent("No se puede generar el archivo porque las siguientes personas no se encuentran activas para el mes y la gestión seleccionada: " + aux);
                response.RequestMessage = Request;
                return response;
            }
            else
            {
                var process = _context.DistProcesses.FirstOrDefault(x =>
                    x.mes.Equals(mes) && x.gestion.Equals(gestion) && x.Branches.Abr.Equals(segmento) &&
                    x.State.Equals("INSAP"));
                //validar que ese proceso en SALOMON sea válido para la generación de datos
                if (process != null)
                {
                    HttpResponseMessage response =
                        new HttpResponseMessage(HttpStatusCode.InternalServerError);
                    response.Content =
                        new StringContent(
                            "El periodo seleccionado no es válido para la generación del archivo PREGRADO en la regional " +
                            segmento);
                    response.RequestMessage = Request;
                    return response;
                }
                else
                {
                    var user = auth.getUser(Request);
                    //El query genera el archivo PREGRADO de SALOMON en base a los datos de las tutorías PRE-APROBADAS
                    string query = "select  " +
                                   "\"Document\" ,\"FirstSurName\", \"SecondSurName\",  \"Names\", \"MariedSurName\", sum(\"TotalNeto\") as \"TotalNeto\", \"Carrera\" , \"CUNI\", \"Dependency\" " +
                                   "from( " +
                                   "select " +
                                   "p.\"Document\" ,p.\"FirstSurName\", p.\"SecondSurName\", " +
                                   "p.\"Names\", p.\"MariedSurName\", " +
                                   "a.\"TotalNeto\", a.\"Carrera\" , " +
                                   "a.\"TeacherCUNI\" as \"CUNI\", a.\"DependencyCod\" as \"Dependency\", a.\"BranchesId\" " +
                                   "from " +
                                   CustomSchema.Schema + ".\"AsesoriaDocente\" a " +
                                   "inner join " + CustomSchema.Schema + ".\"People\" p " +
                                   "on a.\"TeacherCUNI\"=p.\"CUNI\" " +
                                   "inner join " + CustomSchema.Schema + ".\"LASTCONTRACTS\" lc " +
                                   "on a.\"TeacherCUNI\"=lc.\"CUNI\" " +
                                   "inner join " + CustomSchema.Schema + ".\"Branches\" br " +
                                   "on a.\"BranchesId\"=br.\"Id\" " +
                                   "where " +
                                   "a.\"Estado\"='PRE-APROBADO' " +
                                   "and br.\"Abr\" ='" + segmento + "' " +
                                   "and a.\"Origen\"='DEPEN' " +
                                   "order by a.\"Id\" desc) " +
                                   "group by \"Document\" ,\"FirstSurName\", \"SecondSurName\",  \"Names\", \"MariedSurName\", \"Carrera\" , \"CUNI\", \"Dependency\", \"BranchesId\" " +
                                   "order by \"Carrera\" asc, \"FirstSurName\"";

                    var excelContent = _context.Database.SqlQuery<DistPregradoViewModel>(query).ToList();

                    var filteredWithoutCol = excelContent.Select(x => new
                    {
                        x.Document,
                        x.FirstSurName,
                        x.SecondSurName,
                        x.Names,
                        x.MariedSurName,
                        x.TotalNeto,
                        x.Carrera,
                        x.CUNI,
                        x.Dependency
                    }).ToList();

                    //--------------------------------------------------------Generación del excel------------------------------------------------------------------------
                    //Para las columnas del excel
                    string[] header = new string[]
                    {
                        "Carnet Identidad", "Primer Apellido", "Segundo Apellido",
                        "Nombres", "Apellido Casada", "Total Neto Ganado", "Código de Carrera", "CUNI",
                        "Identificador de dependencia"
                    };
                    var workbook = new XLWorkbook();

                    //Se agrega la hoja de excel
                    var ws = workbook.Worksheets.Add("PREGRADO");
                    /*var range = workbook.Worksheets.Range("A1:B2");
                    range.Value = "Merged A1:B2";
                    range.Merge();
                    range.Style.Alignment.Vertical = AlignmentVerticalValues.Top;*/
                    // Título
                    ws.Cell("A1").Value = "PREGRADO";
                    ws.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Cell("A2").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    ws.Range("A1:I2").Merge();
                    //Formato Cabecera
                    ws.Cell(1, 1).Style.Font.Bold = true;
                    ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent1);
                    ws.Cell(1, 1).Style.Font.FontName = "Bahnschrift SemiLight";
                    ws.Cell(1, 1).Style.Font.FontSize = 20;
                    ws.Cell(1, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                    // Rango hoja excel
                    //1,1: es la posicion inicial; 2,header.Length: es el alto y el ancho
                    var rngTable = ws.Range(1, 1, 3, header.Length);

                    //Bordes para las columnas
                    var columns = ws.Range(3, 1, 3 + excelContent.Count, header.Length);
                    columns.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    columns.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;


                    //Para juntar celdas de la cabecera
                    rngTable.Row(3);

                    //auxiliar: desde qué línea ponemos los nombres de columna
                    var headerPos = 3;

                    //Ciclo para asignar los nombres a las columnas y darles formato
                    for (int i = 0; i < header.Length; i++)
                    {
                        ws.Column(i + 1).Width = 13;
                        ws.Cell(headerPos, i + 1).Value = header[i];
                        ws.Cell(headerPos, i + 1).Style.Alignment.WrapText = true;
                        ws.Cell(headerPos, i + 1).Style.Font.Bold = true;
                        ws.Cell(headerPos, i + 1).Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent1);
                    }

                    //Aquí hago el attachment del query a mi hoja de de excel
                    ws.Cell(4, 1).Value = filteredWithoutCol.AsEnumerable();

                    //Ajustar contenidos
                    ws.Columns().AdjustToContents();

                    //Carga el objeto de la respuesta
                    HttpResponseMessage response = new HttpResponseMessage();

                    //Array de bytes
                    var ms = new MemoryStream();
                    workbook.SaveAs(ms);
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new StreamContent(ms);
                    response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                    response.Content.Headers.ContentDisposition.FileName = segmento + gestion + mes + "PREG.xlsx";
                    response.Content.Headers.ContentType =
                        new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                    response.Content.Headers.ContentLength = ms.Length;
                    //La posicion para el comienzo del stream
                    ms.Seek(0, SeekOrigin.Begin);

                    //-----------------------------------------------------Cambios en PRE-APROBADOS ---------------------------------------------------------------------
                    //Actualizar con la fecha a los registros pre-aprobados
                    var docentesPorAprobar = _context.AsesoriaDocente.Where(x =>
                            x.Origen.Equals("DEPEN") && x.Estado.Equals("PRE-APROBADO") && x.BranchesId == segmentoId)
                        .ToList();
                    //Se sobrescriben los registros con la fecha actual y el nuevo estado
                    foreach (var docente in docentesPorAprobar)
                    {
                        docente.Mes = Convert.ToInt16(mes);
                        docente.Gestion = Convert.ToInt16(gestion);
                        docente.Estado = "APROBADO";
                        docente.UserAuth = user.Id;
                        docente.ToAuthAt = DateTime.Now;
                    }

                    _context.SaveChanges();

                    return response;
                }
            }
        }

        // para generar el archivo PREGRADO de SARAI
        [HttpGet]
        [Route("api/ToCarreraFile")]
        public HttpResponseMessage ToCarreraFile([FromUri] string data)
        {
            string[] info = data.Split(';');
            var user = auth.getUser(Request);
            int segmentoId = Convert.ToInt16(info[0]);
            string segmento = _context.Branch.FirstOrDefault(x => x.Id == segmentoId).Abr;
            // el mes y la gestion son necesarios para guardar el registro histórico ISAAC
            string mes = (info[1]);
            string gestion = info[2];
            // El query genera el archivo PREGRADO de SALOMON en base a los datos de las tutorías PRE-APROBADAS
            string query =
                "select " +
                    "a.\"TeacherBP\" as \"Codigo_Socio\", a.\"TeacherFullName\" as \"Nombre_Socio\", " +
                    "a.\"DependencyCod\" as \"Cod_Dependencia\", 'PO' as \"PEI_PO\", " +
                    "'Servicios de Tutoria Relatoria en Pregrado' \"Nombre_del_Servicio\", a.\"Carrera\" as \"Codigo_Carrera\" ,a.\"Acta\" as \"Documento_Base\", " +
                    "a.\"StudentFullName\" as \"Postulante\", t.\"Abr\" as \"Tipo_Tarea_Asignada\", 'CC_TEMPORAL' as \"Cuenta_Asignada\", " +
                    "a.\"TotalBruto\" as \"Monto_Contrato\", a.\"IUE\" as \"Monto_IUE\", a.\"IT\" as \"Monto_IT\", a.\"TotalNeto\" as \"Monto_a_Pagar\",  " +
                    "a.\"Observaciones\" " +
                "from " +
                    CustomSchema.Schema + ".\"AsesoriaDocente\" a " +
                    "inner join " + CustomSchema.Schema + ".\"Civil\" c " +
                    "on a.\"TeacherBP\"=c.\"SAPId\" " +
                    "inner join " + CustomSchema.Schema + ".\"TipoTarea\" t " +
                    "on a.\"TipoTareaId\"=t.\"Id\" " +
                    "inner join " + CustomSchema.Schema + ".\"Branches\" br " +
                    "on a.\"BranchesId\"=br.\"Id\" " +
                "where " +
                   "a.\"Estado\"='PRE-APROBADO' " +
                   "and br.\"Abr\" ='" + segmento + "' " +
                   "and a.\"Origen\"='INDEP' " +
                "order by a.\"Id\" asc";


            var excelContent = _context.Database.SqlQuery<Serv_PregradoViewModel>(query).ToList();

            // Para las columnas del excel
            string[] header = new string[]{"Codigo_Socio", "Nombre_Socio", "Cod_Dependencia",
                                "PEI_PO", "Nombre_del_Servicio", "Codigo_Carrera", "Documento_Base", "Postulante",
                                "Tipo_Tarea_Asignada", "Cuenta_Asignada",
                                "Monto_Contrato", "Monto_IUE", "Monto_IT", "Monto_a_Pagar", "Observaciones"};

            var workbook = new XLWorkbook();

            // Se agrega la hoja de excel
            var ws = workbook.Worksheets.Add("Plantilla_CARRERA");

            // Bordes para las columnas
            var columns = ws.Range(2, 1, excelContent.Count + 1, header.Length);
            columns.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            columns.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // auxiliar: desde qué línea ponemos los nombres de columna
            var headerPos = 1;

            ws.Cell(headerPos, 1).InsertTable(excelContent.AsEnumerable(), "Table");

            // Ajustar contenidos después de insertar la tabla
            ws.Tables.Table(0).ShowAutoFilter = false; // Puedes ajustar esto según tus necesidades
            ws.Tables.Table(0).Theme = XLTableTheme.TableStyleLight1;
            ws.Columns().AdjustToContents();

            // Carga el objeto de la respuesta
            HttpResponseMessage response = new HttpResponseMessage();

            // Array de bytes
            var ms = new MemoryStream();
            workbook.SaveAs(ms);
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StreamContent(ms);
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
            response.Content.Headers.ContentDisposition.FileName = segmento + "-CC_CARRERA.xlsx";
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Content.Headers.ContentLength = ms.Length;
            // La posicion para el comienzo del stream
            ms.Seek(0, SeekOrigin.Begin);

           

            return response;
        }

        [HttpPost]
        [Route("api/AsesoriaDocente")]
        public IHttpActionResult Post([FromBody] AsesoriaDocente asesoria)
        {
            var B1conn = B1Connection.Instance();
            var user = auth.getUser(Request);
            // Ver que la persona esté disponible en nuestra base de personas y de civiles    
            if ((asesoria.Origen.Equals("DEPEN") || asesoria.Origen.Equals("OR")) && !_context.Person.ToList().Any(x => x.CUNI == asesoria.TeacherCUNI))
            {
                return BadRequest("La persona no existe en BD");
            }

            if (asesoria.Origen.Equals("INDEP") && !_context.Civils.ToList().Any(x => x.SAPId == asesoria.TeacherBP))
            {
                return BadRequest("La persona no existe en BD Civil");
            }

            if (!_context.Modalidades.ToList().Any(x => x.Id == asesoria.ModalidadId))
            {
                return BadRequest("La modalidad no existe en BD");
            }

            if (!_context.TipoTarea.ToList().Any(x => x.Id == asesoria.TipoTareaId))
            {
                return BadRequest("El tipo de tarea no existe en BD");
            }
            if (!_context.TipoPago.ToList().Any(x => x.Id == asesoria.TipoPago))
            {
                return BadRequest("El tipo de pago no existe en BD");
            }

            // Validación de la carrera
            List<dynamic> careerList = B1conn.getCareers();
            // Validar el noombre del código de la carrera con el ingresado
            if (!careerList.Exists(x => x.cod == asesoria.Carrera))
            {
                return BadRequest("La carrera no existe en SAP, al menos para esa regional");
            }
            if (asesoria.TotalBruto <= 0 || asesoria.TotalNeto <= 0)
            {
                return BadRequest("No se pueden ingresar datos con valores negativos o iguales a 0");
            }
            if (asesoria.Factura == false && (asesoria.IUE < 0 || asesoria.IT < 0 || asesoria.IUEExterior < 0))
            {
                return BadRequest("No se pueden ingresar datos con valores negativos");
            }
            if (asesoria.Origen.Equals("DEPEN") && asesoria.Ignore == false && (_context.AsesoriaDocente.FirstOrDefault(x => x.StudentFullName.ToUpper() == asesoria.StudentFullName.ToUpper() && x.TeacherCUNI.ToUpper() == asesoria.TeacherCUNI.ToUpper()) != null))
            {
                return BadRequest("La combinación de docente y estudiante ya existe en la BD");

            }
            if (asesoria.Origen == "INDEP" && asesoria.Ignore == false && (_context.AsesoriaDocente.FirstOrDefault(x => x.StudentFullName.ToUpper() == asesoria.StudentFullName.ToUpper() && x.TeacherBP.ToUpper() == asesoria.TeacherBP.ToUpper()) != null))
            {
                return BadRequest("La combinación de docente y estudiante ya existe en la BD");
            }
            else
            {
                // El branchesId es del último puesto de quién registra
                var userCUNI = user.People.CUNI;
                var regional = asesoria.Carrera;
                string[] Abr = regional.Split('-');
                var regionalId = Abr[0].ToString();

                asesoria.BranchesId = _context.Branch.FirstOrDefault(x => x.Abr.Equals(regionalId)).Id;
                // el Id del siguiente registro
                asesoria.Id = AsesoriaDocente.GetNextId(_context);
                // asegura que no se junte el nuevo registro con los históricos
                asesoria.Estado = "REGISTRADO";
                // identifica la dependencia del registro en base al nombre de la carrera y la regional
                var dep = _context.Database.SqlQuery<int>("select de.\"Cod\" " +
                                        "from " +
                                        "   " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprc op " +
                                        "inner join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"@T_GEN_CARRERAS\" tg " +
                                        "    on op.\"PrcCode\" = tg.\"U_CODIGO_CARRERA\" " +
                                        "inner join " + CustomSchema.Schema + ".\"OrganizationalUnit\" ou " +
                                        "    on tg.\"U_CODIGO_DEPARTAMENTO\"=ou.\"Cod\" " +
                                        "inner join " + CustomSchema.Schema + ".\"Dependency\" de " +
                                        "    on ou.\"Id\"=de.\"OrganizationalUnitId\" " +
                                        "where " +
                                            "op.\"DimCode\"=3 " +
                                            "and op.\"PrcCode\" ='" + asesoria.Carrera + "' " +
                                            "and de.\"BranchesId\"=" + asesoria.BranchesId).FirstOrDefault().ToString();
                asesoria.DependencyCod = dep;
                asesoria.StudentFullName = asesoria.StudentFullName.ToUpper();
                asesoria.UserCreate = user.Id;
                // agregar el nuevo registro en el contexto
                _context.AsesoriaDocente.Add(asesoria);
                _context.SaveChanges();
                return Ok("Información registrada");
            }
        }

        // modificacion de la tutoria
        [HttpPut]
        [Route("api/AsesoriaDocente/{id}")]
        public IHttpActionResult Put(int id, [FromBody] AsesoriaDocente asesoria)
        {
            var B1conn = B1Connection.Instance();
            var user = auth.getUser(Request);
            List<dynamic> careerList = B1conn.getCareers();
            if (!_context.AsesoriaDocente.ToList().Any(x => x.Id == id))
            {
                return BadRequest("No existe el registro correspondiente");
            }
            if (!careerList.Exists(x => x.cod == asesoria.Carrera))
            {
                return BadRequest("La carrera no existe en SAP, al menos para esa regional");
            }
            if (asesoria.TotalBruto <= 0 || asesoria.TotalNeto <= 0)
            {
                return BadRequest("No se pueden ingresar datos con valores negativos");
            }
            if (asesoria.Factura == false && (asesoria.IUE < 0 || asesoria.IT < 0 || asesoria.IUEExterior < 0))
            {
                return BadRequest("No se pueden ingresar datos con valores negativos o iguales a 0");
            }
            if (asesoria.Origen.Equals("DEPEN") && asesoria.Ignore == false && (_context.AsesoriaDocente.FirstOrDefault(x => x.Id != id && x.StudentFullName.ToUpper() == asesoria.StudentFullName.ToUpper() && x.TeacherCUNI.ToUpper() == asesoria.TeacherCUNI.ToUpper()) != null))
            {
                return BadRequest("La combinación de docente y estudiante ya existe en la BD");

            }
            if (asesoria.Origen.Equals("INDEP") && asesoria.Ignore == false && (_context.AsesoriaDocente.FirstOrDefault(x => x.Id != id && x.StudentFullName.ToUpper() == asesoria.StudentFullName.ToUpper() && x.TeacherBP.ToUpper() == asesoria.TeacherBP.ToUpper()) != null))
            {
                return BadRequest("La combinación de docente y estudiante ya existe en la BD");
            }
            if (asesoria.Origen.Equals("EXT") && asesoria.Ignore == false && (_context.AsesoriaDocente.FirstOrDefault(x => x.Id != id && x.StudentFullName.ToUpper() == asesoria.StudentFullName.ToUpper() && x.TeacherBP.ToUpper() == asesoria.TeacherBP.ToUpper()) != null))
            {
                return BadRequest("La combinación de docente y estudiante ya existe en la BD");
            }
            else
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest("Datos inválidos para el registro");
                }
                var thisAsesoria = _context.AsesoriaDocente.FirstOrDefault(x => x.Id == id);
                bool origenCambiado = thisAsesoria.Origen != asesoria.Origen;
                //Temporalidad
                thisAsesoria.Mes = asesoria.Mes;
                thisAsesoria.Gestion = asesoria.Gestion;

                if (origenCambiado)
                {
                    // Reiniciar campos específicos que no son relevantes para el nuevo origen
                    thisAsesoria.IUE = 0;
                    thisAsesoria.IT = 0;
                    thisAsesoria.IUEExterior = 0;
                    thisAsesoria.Deduccion = 0;

                    switch (asesoria.Origen)
                    {
                        case "DEPEN":
                            // Reiniciar campos específicos para DEPEN
                            thisAsesoria.IUE = 0;
                            thisAsesoria.IT = 0;
                            thisAsesoria.IUEExterior = 0;
                            thisAsesoria.Deduccion = asesoria.Deduccion;
                            break;
                        case "INDEP":
                            // Reiniciar campos específicos para INDEP
                            thisAsesoria.IUEExterior = 0;
                            thisAsesoria.Deduccion = 0;
                            thisAsesoria.IUE = asesoria.IUE;
                            thisAsesoria.IT = asesoria.IT;
                            break;
                        case "EXT":
                            // Reiniciar campos específicos para EXT
                            thisAsesoria.IUE = 0;
                            thisAsesoria.IT = 0;
                            thisAsesoria.Deduccion = 0;
                            thisAsesoria.IUEExterior = asesoria.IUEExterior;
                            break;
                            // Añade más casos según los posibles orígenes
                    }
                }
                //Carrera y Dep
                thisAsesoria.DependencyCod = asesoria.DependencyCod;
                thisAsesoria.Carrera = asesoria.Carrera;
                //Docente
                thisAsesoria.TeacherCUNI = asesoria.TeacherCUNI;
                thisAsesoria.TeacherFullName = asesoria.TeacherFullName;
                thisAsesoria.TeacherBP = asesoria.TeacherBP;
                thisAsesoria.Categoría = asesoria.Categoría;
                thisAsesoria.Origen = asesoria.Origen;
                thisAsesoria.NumeroContrato = asesoria.NumeroContrato;
                //Estudiante
                thisAsesoria.StudentFullName = asesoria.StudentFullName.ToUpper();
                //Sobre la tutoria
                thisAsesoria.TipoTareaId = asesoria.TipoTareaId;
                thisAsesoria.ModalidadId = asesoria.ModalidadId;
                thisAsesoria.TipoPago = asesoria.TipoPago;
                thisAsesoria.Ignore = asesoria.Ignore;
                //Sobre costos
                thisAsesoria.Horas = asesoria.Horas;
                thisAsesoria.MontoHora = asesoria.MontoHora;
                thisAsesoria.TotalBruto = asesoria.TotalBruto;
                thisAsesoria.TotalNeto = asesoria.TotalNeto;
                // thisAsesoria.Deduccion = asesoria.Deduccion;
                thisAsesoria.Observaciones = asesoria.Observaciones;
                // thisAsesoria.IUE = asesoria.IUE;
                // thisAsesoria.IT = asesoria.IT;
                // thisAsesoria.IUEExterior = asesoria.IUEExterior;
                //Del Acta
                thisAsesoria.Acta = asesoria.Acta;
                thisAsesoria.ActaFecha = asesoria.ActaFecha;
                thisAsesoria.BranchesId = asesoria.BranchesId;
                thisAsesoria.UserUpdate = user.Id;
                thisAsesoria.UpdatedAt = DateTime.Now;
                //Modifica su estado
                thisAsesoria.Estado = asesoria.Estado;
                _context.SaveChanges();
                return Ok("Se actualizaron los datos correctamente");
            }
        }

        // para la instancia de el modulo de aprobacion Isaac, pasar a pre-aprobacion
        [HttpPut]
        [Route("api/ToPreAprobacion")]
        public IHttpActionResult ToPreAprobacion([FromUri] string myArray)
        {
            if (myArray == null)
            {
                return BadRequest("No se ha seleccionado ningún registro para aprobación");
            }
            else
            {
                var countRegister = 0;
                int[] array = Array.ConvertAll(myArray.Split(','), int.Parse);
                int[] failedUpdates = new int[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    int currentElement = array[i];
                    var thisAsesoria = _context.AsesoriaDocente.FirstOrDefault(x => x.Id == currentElement);
                    if (thisAsesoria != null)
                    {

                        thisAsesoria.Estado = "PRE-APROBADO";
                        _context.SaveChanges();
                    }
                    else
                    {
                        //Hubieron elementos del array que no se pudieron actualizar
                        failedUpdates[countRegister] = array[i];
                        countRegister += 1;
                    }
                }
                //Si tenemos todos los Ids
                if (countRegister == 0)
                {
                    return Ok("Se actualizaron los registros exitosamente");
                }
                //Si fallan todos los Ids
                else if (countRegister == array.Length)
                {
                    return BadRequest("No se pudo actualizar ningún registro");
                }
                //Si solo fallan algunos
                else
                {
                    return Ok("No se pudieron actualizar los siguientes registros:" + failedUpdates);
                }
            }
        }

        [HttpPut]
        [Route("api/ToVerificacion")]
        public IHttpActionResult ToVerificacion([FromUri] string myArray)
        {
            if (myArray == null)
            {
                return BadRequest("No se ha seleccionado ningún registro para aprobación");
            }
            else
            {
                var countRegister = 0;
                int[] array = Array.ConvertAll(myArray.Split(','), int.Parse);
                int[] failedUpdates = new int[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    int currentElement = array[i];
                    var thisAsesoria = _context.AsesoriaDocente.FirstOrDefault(x => x.Id == currentElement);
                    if (thisAsesoria != null)
                    {

                        thisAsesoria.Estado = "VERIFICADO";
                        _context.SaveChanges();
                    }
                    else
                    {
                        //Hubieron elementos del array que no se pudieron actualizar
                        failedUpdates[countRegister] = array[i];
                        countRegister += 1;
                    }
                }
                //Si tenemos todos los Ids
                if (countRegister == 0)
                {
                    return Ok("Se actualizaron los registros exitosamente");
                }
                //Si fallan todos los Ids
                else if (countRegister == array.Length)
                {
                    return BadRequest("No se pudo actualizar ningún registro");
                }
                //Si solo fallan algunos
                else
                {
                    return Ok("No se pudieron actualizar los siguientes registros:" + failedUpdates);//aquí meterle el concat por comas
                }
            }
        }

        //para la instancia de el modulo de aprobacion Isaac
        [HttpDelete]
        [Route("api/DeleteRecord/{id}")]
        public IHttpActionResult DeleteRecord(int id)
        {
            //solo borrarlo en la primera instancia, no se eliminan los aprobados
            var recordForDeletion = _context.AsesoriaDocente.FirstOrDefault(x => x.Id == id);
            if (recordForDeletion == null)
            {
                return BadRequest("El registro no existe en BD");
            }
            else
            {
                _context.AsesoriaDocente.Remove(recordForDeletion);
                _context.SaveChanges();
                return Ok("Se eliminó el registro exitosamente");
            }
        }

        [HttpPut]
        [Route("api/SendHistoric")]
        public IHttpActionResult SendHistoric([FromUri] string myArray, [FromUri] string data)
        {
            string[] info = data.Split(';');
            int segmentoId = Convert.ToInt16(info[0]);
            int mes = Convert.ToInt32((info[1]));
            var user = auth.getUser(Request);
            int gestion = Convert.ToInt32(info[2]);
            if (myArray == null)
            {
                return BadRequest("No se ha seleccionado ningún registro para aprobación");
            }
            else
            {
                var countRegister = 0;
                int[] array = Array.ConvertAll(myArray.Split(','), int.Parse);
                int[] failedUpdates = new int[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    int currentElement = array[i];
                    var thisAsesoria = _context.AsesoriaDocente.FirstOrDefault(x => x.Id == currentElement);
                    if (thisAsesoria != null)
                    {

                        thisAsesoria.Estado = "APROBADO";
                        thisAsesoria.Mes = mes;
                        thisAsesoria.Gestion = gestion;
                        thisAsesoria.BranchesId = segmentoId;
                        thisAsesoria.ToAuthAt = DateTime.Now;
                        thisAsesoria.UserAuth = user.Id;
                        _context.SaveChanges();
                    }
                    else
                    {
                        //Hubieron elementos del array que no se pudieron actualizar
                        failedUpdates[countRegister] = array[i];
                        countRegister += 1;
                    }
                }
                //Si tenemos todos los Ids
                if (countRegister == 0)
                {
                    return Ok("Se actualizaron los registros exitosamente");
                }
                //Si fallan todos los Ids
                else if (countRegister == array.Length)
                {
                    return BadRequest("No se pudo actualizar ningún registro");
                }
                //Si solo fallan algunos
                else
                {
                    return Ok("No se pudieron actualizar los siguientes registros:" + failedUpdates);//aquí meterle el concat por comas
                }
            }
        }

        // para la instancia de el modulo de aprobacion Isaac, pasar a pre-aprobacion
        [HttpPut]
        [Route("api/ToPreAprobacionOR")]
        public IHttpActionResult ToPreAprobacionOR([FromUri] string myArray, [FromUri] string data)
        {
            string[] info = data.Split(';');
            int segmentoId = Convert.ToInt16(info[0]);
            int mes = Convert.ToInt32((info[1]));
            int gestion = Convert.ToInt32(info[2]);
            if (myArray == null)
            {
                return BadRequest("No se ha seleccionado ningún registro para aprobación");
            }
            else
            {
                var countRegister = 0;
                int[] array = Array.ConvertAll(myArray.Split(','), int.Parse);
                int[] failedUpdates = new int[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    int currentElement = array[i];
                    var thisAsesoria = _context.AsesoriaDocente.FirstOrDefault(x => x.Id == currentElement);
                    if (thisAsesoria != null)
                    {
                        if (thisAsesoria.Origen.Equals("OR"))
                        {
                            thisAsesoria.Estado = "APROBADO"; thisAsesoria.Mes = mes;
                            thisAsesoria.Gestion = gestion;
                            thisAsesoria.BranchesId = segmentoId;
                            _context.SaveChanges();
                        }
                        else
                        {
                            BadRequest("Los registros no son de tipo OR. No se puede efectuar la acción.");
                        }
                    }
                    else
                    {
                        // Hubieron elementos del array que no se pudieron actualizar
                        failedUpdates[countRegister] = array[i];
                        countRegister += 1;
                    }
                }
                // Si tenemos todos los Ids
                if (countRegister == 0)
                {
                    return Ok("Se actualizaron los registros exitosamente");
                }
                // Si fallan todos los Ids
                else if (countRegister == array.Length)
                {
                    return BadRequest("No se pudo actualizar ningún registro");
                }
                // Si solo fallan algunos
                else
                {
                    return Ok("No se pudieron actualizar los siguientes registros:" + failedUpdates);
                }
            }
        }

        // REPORTE POR CARRERA
        // obtener carreras segun su estado en la lista
        [HttpGet]
        [Route("api/AseCarrera")]
        public IHttpActionResult AseCarrera([FromUri] string by)
        {
            string query = "select oprc.\"PrcCode\" \"Cod\", oprc.\"PrcName\" \"Carrera\", a.\"BranchesId\"" +
                           "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaDocente\" a " +
                           "\r\ninner join " + CustomSchema.Schema + ".\"TipoTarea\" t on a.\"TipoTareaId\"=t.\"Id\" " +
                           "\r\ninner join " + CustomSchema.Schema + ".\"Modalidades\" tm on a.\"ModalidadId\"=tm.\"Id\" " +
                           "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on a.\"BranchesId\"=br.\"Id\" " +
                           "\r\ninner join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"OPRC\" oprc on oprc.\"PrcCode\" = a.\"Carrera\"";
            string orderBy = " group by oprc.\"PrcCode\", oprc.\"PrcName\", a.\"BranchesId\" order by oprc.\"PrcCode\" asc";
            var rawresult = new List<AsesoriaDocenteViewModel>();
            var user = auth.getUser(Request);

            if (by.Equals("APROBADO"))
            {
                string customQuery = query + "where a.\"Estado\"='APROBADO' " + orderBy;
                // Mes a literal
                rawresult = mesLiteral(customQuery);
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new { x.Cod, x.Carrera });
                return Ok(filteredList);

            }
            else if (by.Equals("PRE-APROBADO"))
            {
                string customQuery = query + "where a.\"Estado\"='PRE-APROBADO' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Carrera
                    }); ;
                return Ok(filteredList);

            }
            else if (by.Equals("REGISTRADO-DEP"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Origen\"='DEPEN' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Carrera
                    });
                return Ok(filteredList);

            }
            else if (by.Equals("REGISTRADO-INDEP"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Origen\"='INDEP' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Carrera
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("REGISTRADO-OR"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Origen\"='OR' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Carrera
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("REGISTRADO-FAC"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Factura\"=true " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Carrera
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("REGISTRADO-EXT"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Origen\"='EXT' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Carrera
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("VERIFICADO-DEP"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='VERIFICADO' " + "and a.\"Origen\"='DEPEN' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Carrera
                    });
                return Ok(filteredList);

            }
            else if (by.Equals("VERIFICADO-INDEP"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='VERIFICADO' " + "and a.\"Origen\"='INDEP' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Carrera
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("VERIFICADO-OR"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='VERIFICADO' " + "and a.\"Origen\"='OR' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Carrera
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("VERIFICADO-FAC"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='VERIFICADO' " + "and a.\"Factura\"=true " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Carrera
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("VERIFICADO-EXT"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='VERIFICADO' " + "and a.\"Origen\"='EXT' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Carrera
                    });
                return Ok(filteredList);
            }
            else
            {
                return BadRequest();
            }

        }

        [HttpGet]
        [Route("api/PDFReportBodyXCarrera")]
        public IHttpActionResult PDFReportXCarrera([FromUri] string part)
        {
            string query = "";
            var report = new List<AsesoriaDocenteViewModel>();
            string[] data = part.Split(';');
            string section = data[0];
            string state = data[1];
            string origin = data[2];
            string carrera = data[3];
            string qOrigen = "";
            if (origin == "FAC")
            {
                qOrigen = " and a.\"Origen\" = 'INDEP' and a.\"Factura\" = true ";
            }
            else
            {
                qOrigen = " and a.\"Origen\" like '%" + origin + "%' and a.\"Factura\" = false ";
            }
            //query para generar todos los datos de cada carrera, ordenado por carrera y docente
            switch (section)
            {
                case "Body":
                    //obtiene el cuerpo de la tabla para el PDF
                    //join para el nombre de la carrera
                    query = "select " +
                            "\"TeacherFullName\", \"Origen\", \"Categoría\", " +
                            "m.\"Abr\" as \"Modalidad\", " +
                            "t.\"Abr\" as \"TipoTarea\", " +
                            "a.\"Carrera\" ||" + " ' ' " + "|| op.\"PrcName\" as \"Carrera\" " + ", \"StudentFullName\" , " +
                            "case when \"Acta\" is null then '' else \"Acta\" end as \"Acta\"," +
                            " \"ActaFecha\" , " +
                            "\"Horas\", \"MontoHora\", " +
                            "\"TotalBruto\" , " +
                            "\"Deduccion\" , " +
                            "\"NumeroContrato\" , " +
                            "tp.\"Nombre\" as \"TipoPago\", " +
                            "case when \"IUE\" is null then 0 else \"IUE\" end as \"IUE\", " +
                            "case when \"IT\" is null then 0 else \"IT\" end as \"IT\", " +
                            "case when \"IUEExterior\" is null then 0 else \"IUEExterior\" end as \"IUEExterior\", " +
                            "\"TotalNeto\" , " +
                            "\"Observaciones\", \"BranchesId\", " +
                            " case when a.\"Ignore\" = true then 'D' when a.\"Ignore\" = false then '' end as \"Ignore\"" +
                        "from " +
                            CustomSchema.Schema + ".\"AsesoriaDocente\" a " +
                        "inner join " +
                            CustomSchema.Schema + ".\"TipoTarea\" t " +
                            "on a.\"TipoTareaId\"=t.\"Id\" " +
                        "inner join " +
                            CustomSchema.Schema + ".\"TipoPago\" tp " +
                            "on a.\"TipoPago\"=tp.\"Id\" " +
                        "inner join " +
                            CustomSchema.Schema + ".\"Modalidades\" m " +
                            "on a.\"ModalidadId\"=m.\"Id\" " +
                        "inner join " +
                            ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"OPRC\" op " +
                            "on a.\"Carrera\"= op.\"PrcCode\" " +
                        "where " +
                            "a.\"Estado\"='" + state + "' " +
                            qOrigen +
                            "and a.\"Carrera\" like '%" + carrera + "%'" +
                            "and op.\"DimCode\" = 3 " +
                        "order by \"Carrera\",  a.\"Id\", \"TeacherFullName\" asc";
                    report = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(query).ToList();
                    break;

                case "Results":
                    //obtiene los resultados al pie de cada tabla, por carrera
                    query = "select " +
                            "(a.\"Carrera\" ||" + " ' ' " + "|| op.\"PrcName\") as \"Carrera\", " +
                            "sum(\"TotalBruto\") as \"TotalBruto\", " +
                            "sum(\"Deduccion\") as \"Deduccion\", " +
                            "case when sum(\"IUE\") is null then 0 else sum(\"IUE\") end as \"IUE\",  " +
                            "case when sum(\"IT\") is null then 0 else sum(\"IT\") end as \"IT\", " +
                            "case when sum(\"IUEExterior\") is null then 0 else sum(\"IUEExterior\") end as \"IUEExterior\", " +
                            "sum(\"TotalNeto\") as \"TotalNeto\", \"BranchesId\" " +
                        "from " +
                            CustomSchema.Schema + ".\"AsesoriaDocente\" a " +
                        "inner join " +
                            ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"OPRC\" op " +
                            "on a.\"Carrera\"= op.\"PrcCode\" " +
                        "where " +
                            "\"Estado\"='" + state + "' " +
                            qOrigen +
                            "and a.\"Carrera\" like '%" + carrera + "%'" +
                        "group by \"Carrera\", \"PrcName\", \"BranchesId\" " +
                        "order by \"Carrera\" ";
                    report = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(query).ToList();
                    break;

                case "FinalResult":
                    //obtiene los resultados al pie de cada tabla, por carrera
                    query = "select " +
                            "sum(\"TotalBruto\") as \"TotalBruto\", " +
                            "sum(\"Deduccion\") as \"Deduccion\", " +
                            "case when sum(\"IUE\") is null then 0 else sum(\"IUE\") end as \"IUE\",  " +
                            "case when sum(\"IT\") is null then 0 else sum(\"IT\") end as \"IT\", " +
                            "case when sum(\"IUEExterior\") is null then 0 else sum(\"IUEExterior\") end as \"IUEExterior\", " +
                            "sum(\"TotalNeto\") as \"TotalNeto\", \"BranchesId\"" +
                        "from " +
                            CustomSchema.Schema + ".\"AsesoriaDocente\" a " +

                            "inner join " + CustomSchema.Schema + ".\"Branches\" b " +
                            "on b.\"Id\" = a.\"BranchesId\" " +
                        "where " +
                            "\"Estado\"='" + state + "' " +
                            qOrigen +
                            "and a.\"Carrera\" like '%" + carrera + "%'" +
                        "group by \"BranchesId\" ";
                    report = _context.Database.SqlQuery<AsesoriaDocenteViewModel>(query).ToList();
                    break;

                default:
                    return BadRequest();
            }
            // Filtro de datos por regional
            var user = auth.getUser(Request);
            if (section.Equals("Body"))
            {
                var filteredListBody = auth.filerByRegional(report.AsQueryable(), user).ToList().Select(x => new
                {
                    Carrera = x.Carrera,
                    Docente = x.TeacherFullName,
                    Origen = x.Origen,
                    Modal = x.Modalidad,
                    Tarea = x.TipoTarea,
                    Alumno = x.StudentFullName,
                    Acta = x.Acta,
                    Fecha = x.ActaFecha != null ? x.ActaFecha.ToString("dd-MM-yyyy") : null,
                    Horas = x.Horas,
                    x.NumeroContrato,
                    x.TipoPago,
                    Costo_Hora = x.MontoHora,
                    Total_Bruto = x.TotalBruto,
                    Deduccion = x.Deduccion,
                    IUE = x.IUE,
                    IT = x.IT,
                    IUEExt = x.IUEExterior,
                    Total_Neto = x.TotalNeto,
                    Observaciones = x.Observaciones,
                    Dup = x.Ignore
                });

                return Ok(filteredListBody);
            }
            else if (section.Equals("Results"))
            {
                var filteredListResult = auth.filerByRegional(report.AsQueryable(), user).ToList().Select(x => new
                {
                    Carrera = x.Carrera,
                    Total_Bruto = x.TotalBruto,
                    Deduccion = x.Deduccion,
                    IUE = x.IUE,
                    IT = x.IT,
                    IUEExt = x.IUEExterior,
                    Total_Neto = x.TotalNeto,
                    x.BranchesId
                });
                return Ok(filteredListResult);
            }
            else
            {
                var filteredListResult = auth.filerByRegional(report.AsQueryable(), user).ToList().Select(x => new
                {
                    Total_Bruto = x.TotalBruto,
                    Deduccion = x.Deduccion,
                    IUE = x.IUE,
                    IT = x.IT,
                    IUEExt = x.IUEExterior,
                    Total_Neto = x.TotalNeto,
                });
                return Ok(filteredListResult);
            }
        }
        //para generar el archivo PREGRADO de SALOMON
        [HttpGet]
        [Route("api/ToOtrasRegionalesFile")]
        public HttpResponseMessage ToOtrasRegionalesFile([FromUri] string data)
        {
            string[] info = data.Split(';');
            int segmentoId = Convert.ToInt16(info[0]);
            string segmento = _context.Branch.FirstOrDefault(x => x.Id == segmentoId).Abr;
            string mes = (info[1]);
            string gestion = info[2];
            string segmentoOrigen = info[3];

            var process = _context.DistProcesses.FirstOrDefault(x => x.mes.Equals(mes) && x.gestion.Equals(gestion) && x.Branches.Abr.Equals(segmento) && x.State.Equals("INSAP"));
            //validar que ese proceso en SALOMON sea válido para la generación de datos
            if (process != null)
            {
                HttpResponseMessage response =
                            new HttpResponseMessage(HttpStatusCode.InternalServerError);
                response.Content = new StringContent("El periodo seleccionado no es válido para la generación del archivo PREGRADO en la regional " + segmento);
                response.RequestMessage = Request;
                return response;
            }
            else
            {
                var user = auth.getUser(Request);
                //El query genera el archivo PREGRADO de SALOMON en base a los datos de las tutorías PRE-APROBADAS
                string query = "select\r\np.\"Document\" \"Carnet Identidad\"," +
                               "\r\np.\"FirstSurName\" \"PrimerApellido\"," +
                               "\r\np.\"SecondSurName\" \"Segundo Apellido\"," +
                               "\r\np.\"Names\" \"Nombres\"," +
                               "\r\np.\"MariedSurName\" \"Apellido Casada\"," +
                               "\r\nSUBSTRING (ad.\"Carrera\",1,3) \"SegmentoOrigen\"," +
                               "\r\nad.\"TotalNeto\" \"Total Neto Ganado\"," +
                               "\r\nad.\"TeacherCUNI\" \"CUNI\"," +
                               "\r\nou.\"Cod\" \"CCD1\"," +
                               "\r\n'PO' \"CCD2\"," +
                               "\r\nad.\"Carrera\" \"CCD3\"," +
                               "\r\n'' \"CCD4\"," +
                               "\r\n'' \"CCD5\"," +
                               "\r\n'' \"CCD6\"" +
                               "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaDocente\"  ad" +
                               "\r\ninner join " + CustomSchema.Schema + ".\"People\" p on p.\"CUNI\" = ad.\"TeacherCUNI\"" +
                               "\r\ninner join " + CustomSchema.Schema + ".\"Dependency\" dep on dep.\"Cod\" = ad.\"DependencyCod\"" +
                               "\r\ninner join " + CustomSchema.Schema + ".\"OrganizationalUnit\" ou on ou.\"Id\" = dep.\"OrganizationalUnitId\"" +
                               "\r\nwhere \"Origen\"= 'OR' and ad.\"Estado\" != 'APROBADO'" +
                               "\r\nand ad.\"BranchesId\" = " + segmentoOrigen;

                var excelContent = _context.Database.SqlQuery<DistORViewModel>(query).ToList();

                var filteredWithoutCol = excelContent.Select(x => new { x.Document, x.FirstSurName, x.SecondSurName, x.Names, x.MariedSurName, x.SegmentoOrigen, x.TotalNeto, x.CUNI, x.CCD1, x.CCD2, x.CCD3, x.CCD4 }).ToList();

                //--------------------------------------------------------Generación del excel------------------------------------------------------------------------
                //Para las columnas del excel
                string[] header = new string[]{"Carnet Identidad", "Primer Apellido", "Segundo Apellido",
                                            "Nombres", "Apellido Casada", "Segmento origen", "Total Neto Ganado", "CUNI",
                                            "CCD1", "CDD2", "CDD3", "CDD4"};
                var workbook = new XLWorkbook();

                //Se agrega la hoja de excel
                var ws = workbook.Worksheets.Add("OtrasRegionales");
                /*var range = workbook.Worksheets.Range("A1:B2");
                range.Value = "Merged A1:B2";
                range.Merge();
                range.Style.Alignment.Vertical = AlignmentVerticalValues.Top;*/
                // Título
                ws.Cell("A1").Value = "OTRASREGIONALES";
                ws.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell("A2").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Range("A1:I2").Merge();
                //Formato Cabecera
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent1);
                ws.Cell(1, 1).Style.Font.FontName = "Bahnschrift SemiLight";
                ws.Cell(1, 1).Style.Font.FontSize = 20;
                ws.Cell(1, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                // Rango hoja excel
                //1,1: es la posicion inicial; 2,header.Length: es el alto y el ancho
                var rngTable = ws.Range(1, 1, 3, header.Length);

                //Bordes para las columnas
                var columns = ws.Range(3, 1, 3 + excelContent.Count, header.Length);
                columns.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                columns.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;


                //Para juntar celdas de la cabecera
                rngTable.Row(3);

                //auxiliar: desde qué línea ponemos los nombres de columna
                var headerPos = 3;

                //Ciclo para asignar los nombres a las columnas y darles formato
                for (int i = 0; i < header.Length; i++)
                {
                    ws.Column(i + 1).Width = 13;
                    ws.Cell(headerPos, i + 1).Value = header[i];
                    ws.Cell(headerPos, i + 1).Style.Alignment.WrapText = true;
                    ws.Cell(headerPos, i + 1).Style.Font.Bold = true;
                    ws.Cell(headerPos, i + 1).Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent1);
                }

                //Aquí hago el attachment del query a mi hoja de de excel
                ws.Cell(4, 1).Value = filteredWithoutCol.AsEnumerable();

                //Ajustar contenidos
                ws.Columns().AdjustToContents();

                //Carga el objeto de la respuesta
                HttpResponseMessage response = new HttpResponseMessage();

                //Array de bytes
                var ms = new MemoryStream();
                workbook.SaveAs(ms);
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StreamContent(ms);
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                response.Content.Headers.ContentDisposition.FileName = segmentoOrigen + gestion + mes + "REGI.xlsx";
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                response.Content.Headers.ContentLength = ms.Length;
                //La posicion para el comienzo del stream
                ms.Seek(0, SeekOrigin.Begin);

                //-----------------------------------------------------Cambios en PRE-APROBADOS ---------------------------------------------------------------------
                //Actualizar con la fecha a los registros pre-aprobados
                var docentesPorAprobar = _context.AsesoriaDocente.Where(x => x.Origen.Equals("OR") && x.Estado.Equals("REGISTRADO") && x.BranchesId == segmentoId).ToList();
                //Se sobrescriben los registros con la fecha actual y el nuevo estado
                foreach (var docente in docentesPorAprobar)
                {
                    docente.Mes = Convert.ToInt16(mes);
                    docente.Gestion = Convert.ToInt16(gestion);
                    docente.Estado = "APROBADO";
                }

                _context.SaveChanges();

                return response;
            }
        }

        [HttpGet]
        [Route("api/AsesoriaDocente/segmentoOrigen")]
        public IHttpActionResult segmentoOrigen()
        {
            //Hacer un union con los docentes que no sean indepedientes, es decir que sean de civil nomas, por su jobTitle
            var activeDocentes = _context.Database.SqlQuery<AuxiliarBranches>("select" +
                                                                              "\r\nSUBSTRING (ad.\"Carrera\",1,3) \"segmentoOrigen\", br.\"Id\" \"BranchesId\"" +
                                                                              "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaDocente\"  ad" +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on br.\"Abr\" = SUBSTRING (ad.\"Carrera\",1,3)" +
                                                                              "\r\nwhere \"Origen\"= 'OR' and ad.\"Estado\" != 'APROBADO'"
            ).ToList();

            var user = auth.getUser(Request);

            var filteredList = auth.filerByRegional(activeDocentes.AsQueryable(), user);

            return Ok(filteredList);
        }

        //Funciones para la búsqueda avanzada
        [HttpGet]
        [Route("api/AsesoriaDocente/dependenciasUsadas")]
        public IHttpActionResult dependenciasUsadas()
        {
            //Hacer un union con los docentes que no sean indepedientes, es decir que sean de civil nomas, por su jobTitle
            var activeDocentes = _context.Database.SqlQuery<DependencyUsed>("select ad.\"DependencyCod\" \"Cod\", d.\"Name\", d.\"BranchesId\"" +
                                                                              "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaDocente\" ad" +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"Dependency\" d on d.\"Cod\" = ad.\"DependencyCod\"" +
                                                                              "\r\ngroup by ad.\"DependencyCod\", d.\"Name\", d.\"BranchesId\"" +
                                                                              "\r\norder by ad.\"DependencyCod\""
            ).ToList();

            var user = auth.getUser(Request);

            var filteredList = auth.filerByRegional(activeDocentes.AsQueryable(), user);

            return Ok(filteredList);
        }

        // Lista de alumnos.
        [HttpGet]
        [Route("api/AlumnosListBusqueda")]
        public IHttpActionResult AlumnosListBusqueda()
        {
            // Utiliza un query SQL para obtener la lista distinta de estudiantes
            var alumnos = _context.Database.SqlQuery<string>(
                "SELECT \"StudentFullName\" " +
                "FROM \"ADMNALRRHH\".\"AsesoriaDocente\" " +
                "GROUP BY \"StudentFullName\" " +
                "ORDER BY \"StudentFullName\""
            ).ToList();

            var user = auth.getUser(Request);

            var filteredList = auth.filerByRegional(alumnos.AsQueryable(), user);

            return Ok(filteredList);
        }

        [HttpGet]
        [Route("api/AlumnosListBusqueda2/{teacherName}")]
        public IHttpActionResult AlumnosListBusqueda(string teacherName)
        {
            // Utiliza un query SQL para obtener la lista distinta de estudiantes filtrada por el nombre del docente
            var query = string.Format(
                "SELECT \"StudentFullName\" " +
                "FROM \"ADMNALRRHH\".\"AsesoriaDocente\" " +
                "WHERE \"TeacherFullName\" = '{0}' " +
                "GROUP BY \"StudentFullName\" " +
                "ORDER BY \"StudentFullName\"",
                teacherName
            );

            var alumnos = _context.Database.SqlQuery<string>(query).ToList();

            var user = auth.getUser(Request);

            var filteredList = auth.filerByRegional(alumnos.AsQueryable(), user);

            return Ok(filteredList);
        }

        [HttpGet]
        [Route("api/Extranjeros")]
        public IHttpActionResult Extranjeros()
        {
            var extranjeros = _context.Database.SqlQuery<string>(
                "SELECT * " +
                "FROM \"ADMNALRRHH\".\"AsesoriaDocente\" " +
                "WHERE \"Extranjero\" = 0"
            ).ToList();

            return Ok(extranjeros);
        }




        // lista de docentes para el registro
        [HttpGet]
        [Route("api/DocentesListBusqueda")]
        public IHttpActionResult DocentesListBusqueda()
        {
            //Hacer un union con los docentes que no sean indepedientes, es decir que sean de civil nomas, por su jobTitle
            var activeDocentes = _context.Database.SqlQuery<AsesoriaTeachers>("select \"FullName\", \"BranchesId\",\"Regional\" from " +
                                                                              "(\r\n(select fn.\"FullName\", lc.\"BranchesId\",\r\nb.\"Abr\" \"Regional\"" +
                                                                              "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaDocente\" ad" +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"FullName\" fn on fn.cuni = ad.\"TeacherCUNI\"" +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".LASTCONTRACTS lc on lc.cuni = ad.\"TeacherCUNI\"" +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" b on b.\"Id\" = lc.\"BranchesId\"" +
                                                                              "\r\nwhere ad.\"Estado\"= 'APROBADO' " +
                                                                              "\r\ngroup by fn.cuni,ad.\"TeacherCUNI\", fn.\"FullName\", lc.\"BranchesId\",b.\"Abr\")" +
                                                                              "\r\nunion all\r\n(select ocrd.\"CardName\" \"FullName\", br.\"Id\" as \"BranchesId\",  " +
                                                                              "\r\n br.\"Abr\" \"Regional\"" +
                                                                              "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaDocente\" cv " +
                                                                              "\r\ninner join   " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".OCRD on cv.\"TeacherBP\" = ocrd.\"CardCode\" " +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on cv.\"BranchesId\" = br.\"Id\"" +
                                                                              "\r\nwhere cv.\"Estado\"= 'APROBADO' " +
                                                                              "\r\ngroup by cv.\"TeacherBP\",ocrd.\"CardName\", br.\"Id\", br.\"Abr\"))" +
                                                                              "\r\ngroup by \"FullName\", \"BranchesId\",\"Regional\"" +
                                                                              "\r\norder by \"FullName\""
            ).ToList();


            var user = auth.getUser(Request);

            var filteredList = auth.filerByRegional(activeDocentes.AsQueryable(), user);

            return Ok(filteredList);
        }

        // lista de docentes para el registro
        [HttpGet]
        [Route("api/CarrerasListBusqueda")]
        public IHttpActionResult CarrerasListBusqueda()
        {
            //Hacer un union con los docentes que no sean indepedientes, es decir que sean de civil nomas, por su jobTitle
            var activeDocentes = _context.Database.SqlQuery<AuxiliarBranches>("select ad.\"Carrera\" \"Cod\", oprc.\"PrcName\" \"Name\", br.\"Id\" \"BranchesId\"" +
                                                                              "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaDocente\" ad" +
                                                                              "\r\ninner join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprc on oprc.\"PrcCode\" = ad.\"Carrera\"" +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on br.\"Abr\" = substring(oprc.\"PrcCode\",1,3)" +
                                                                              "\r\nwhere ad.\"Estado\"= 'APROBADO' " +
                                                                              "\r\ngroup by ad.\"Carrera\", oprc.\"PrcName\", br.\"Id\"" +
                                                                              "\r\norder by ad.\"Carrera\""
            //"where oh.\"jobTitle\" like '%DOCENTE%' "
            ).ToList();


            var user = auth.getUser(Request);

            var filteredList = auth.filerByRegional(activeDocentes.AsQueryable(), user);

            return Ok(filteredList);
        }
        [HttpGet]
        [Route("api/BusquedaAvanzadaIsaac/{carrera}/{docente}/{modalidad}/{tarea}/{estudiante}/{mes}/{gestion}/{origenFiltro}/{tPag}")]
        public IHttpActionResult BusquedaAvanzadaIsaac(string carrera, string docente, string modalidad, string tarea, string estudiante, string mes, string gestion, string origenFiltro, string tPag)
        {
            try
            {
                // Siguientes lineas para realizar el filtro por regional directamente dentro del query segun las regionales a las que tenga acceso el usuario
                var user = auth.getUser(Request);
                ADClass ad = new ADClass();
                List<Branches> bre = ad.getUserBranches(user);
                Branches[] auxi = bre.ToArray();
                string regionalesUser = "and ad.\"BranchesId\" in (";
                for (int i = 0; i < auxi.Length - 1; i++)
                {
                    regionalesUser = regionalesUser + auxi[i].Id + ",";
                }
                regionalesUser = regionalesUser + auxi[auxi.Length - 1].Id + ")";


                var report = new List<AsesoriaDocenteReportViewModel>();
                string car = "";
                string doc = "";
                string mod = "";
                string tar = "";
                string est = "";
                string mesO = "";
                string ges = "";
                string orgF = "";
                string pag = "";
                var cabecera =
                    "select 1 \"Id\", ad.\"TeacherFullName\", ad.\"TeacherCUNI\", ad.\"TeacherBP\", \r\nad.\"DependencyCod\", d.\"Name\" \"Dependencia\",ad.\"Carrera\", ad.\"Origen\", m.\"Modalidad\" \"Modalidad\",\r\ntt.\"Tarea\" \"TipoTarea\",\r\ntp.\"Nombre\" \"TipoPago\", ad.\"StudentFullName\", ad.\"Categoría\", ad.\"Acta\", ad.\"ActaFecha\", ad.\"Observaciones\",\r\nad.\"TotalBruto\", case when ad.\"IUE\" is null then 0 else ad.\"IUE\" end as \"IUE\", case when ad.\"IT\" is null then 0 else ad.\"IT\" end as \"IT\",  ad.\"Deduccion\", ad.\"TotalNeto\", \r\ncase when ad.\"Mes\" = 1 then 'ENE'\r\nwhen ad.\"Mes\" = 2 then 'FEB'\r\nwhen ad.\"Mes\" = 3 then 'MAR'\r\nwhen ad.\"Mes\" = 4 then 'ABR'\r\nwhen ad.\"Mes\" = 5 then 'MAY'\r\nwhen ad.\"Mes\" = 6 then 'JUN'\r\nwhen ad.\"Mes\" = 7 then 'JUL'\r\nwhen ad.\"Mes\" = 8 then 'AGO'\r\nwhen ad.\"Mes\" = 9 then 'SEP'\r\nwhen ad.\"Mes\" = 10 then 'OCT'\r\nwhen ad.\"Mes\" = 11 then 'NOV'\r\nwhen ad.\"Mes\" = 12 then 'DIC'\r\nelse ''\r\nend as \"MesLiteral\", ad.\"Mes\"\r\n,ad.\"Gestion\", br.\"Abr\" \"Regional\", ad.\"BranchesId\", case when ad.\"Ignore\" = true then 'D' when ad.\"Ignore\" = false then '' end as \"Ignore\"";
                
                var cabeceraSubTotal = "select 8 \"Id\",'' \"TeacherFullName\",  '' \"TeacherCUNI\",  '' \"TeacherBP\",  null \"DependencyCod\", '' \"Dependencia\" ,'' \"Carrera\", \r\n '' \"Origen\", '' \"Modalidad\",  '' \"TipoTarea\",  '' \"TipoPago\",  ''  \"StudentFullName\",  ''  \"Categoría\",  '' \"Acta\",  \r\n null \"ActaFecha\",  '' \"Observaciones\",sum(ad.\"TotalBruto\") \"TotalBruto\", case when sum(ad.\"IUE\") is null then 0 else sum(ad.\"IUE\") end as \"IUE\",  case when sum(ad.\"IT\") is null then 0 else sum(ad.\"IT\") end as \"IT\",\r\n  sum(ad.\"Deduccion\") \"Deduccion\", sum(ad.\"TotalNeto\") \"TotalNeto\", '' \"MesLiteral\", null \"Mes\", null \"Gestion\", '' \"Regional\", 17 \"BranchesId\", '' \"Dup\"";

                var queryCuerpo = "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaDocente\" ad" +
                  "\r\ninner join " + CustomSchema.Schema + ".\"Modalidades\" m on m.\"Id\" = ad.\"ModalidadId\"" +
                  "\r\ninner join " + CustomSchema.Schema + ".\"TipoTarea\" tt on tt.\"Id\" = ad.\"TipoTareaId\"" +
                  "\r\ninner join " + CustomSchema.Schema + ".\"TipoPago\" tp on tp.\"Id\"= ad.\"TipoPago\" " +
                  "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on br.\"Id\" = ad.\"BranchesId\"" +
                  "\r\ninner join " + CustomSchema.Schema + ".\"Dependency\" d on d.\"Cod\" = ad.\"DependencyCod\"" +
                  "\r\nwhere ad.\"Estado\" = 'APROBADO' ";

                if (carrera != "null")
                {
                    car = " and ad.\"Carrera\" ='" + carrera + "'";
                }
                if (origenFiltro == "1")
                {
                    orgF = " and ad.\"Origen\" ='DEPEN'";
                }
                else
                {
                    if (origenFiltro == "2")
                    {
                        orgF = " and ad.\"Origen\" ='INDEP'";
                    }
                    if (origenFiltro == "3")
                    {
                        orgF = " and ad. \"Origen\" ='OR'";
                    }
                    if (origenFiltro == "4")
                    {
                        orgF = " and ad. \"Origen\" ='FAC'";
                    }
                    if (origenFiltro == "5")
                    {
                        orgF = " and ad. \"Origen\" ='EXT'";
                    }
                }

                if (docente != "null")
                {
                    doc = " and ad.\"TeacherFullName\" like '%" + docente + "%'";
                }
                if (modalidad != "null")
                {
                    mod = " and ad.\"ModalidadId\" ='" + modalidad + "'";
                }
                if (tarea != "null")
                {
                    tar = " and ad.\"TipoTareaId\" ='" + tarea + "'";
                }
                if (estudiante != "null")
                {
                    est = " and ad.\"StudentFullName\" like '%" + estudiante.ToUpper() + "%'";
                }
                if (mes != "null")
                {
                    mesO = " and ad.\"Mes\" =" + mes + "";
                }
                if (gestion != "null")
                {
                    ges = " and ad.\"Gestion\" =" + gestion + "";
                }
                if (tPag != "null")
                {
                    pag = "and ad.\"TipoPago\" ='" + tPag + "'";
                }
                // Construir las condiciones de rango para TotalBruto y TotalNeto
                // var condicionesRangoBruto = (minBruto >= 0 && maxBruto >= minBruto) ? " AND ad.\"TotalBruto\" BETWEEN " + minBruto + " AND " + maxBruto : "";
                // var condicionesRangoNeto = (minNeto >= 0 && maxNeto >= minNeto) ? " AND ad.\"TotalNeto\" BETWEEN " + minNeto + " AND " + maxNeto : "";

                string order = " order by \"Id\",\"Gestion\", \"Mes\", \"TeacherFullName\" ,\"StudentFullName\"";
                string query = cabecera + queryCuerpo + regionalesUser + car + orgF + doc + mod + tar + est + mesO + ges + pag;
                string querysubTotal = cabeceraSubTotal + queryCuerpo + regionalesUser + car + orgF + doc + mod + tar + est + mesO + ges + pag;
                string QueryOriginal = "(" + query + ") UNION (" + querysubTotal + ")" + order;
                var reportOG = _context.Database.SqlQuery<AsesoriaDocenteReportViewModel>(query).ToList();
                report = _context.Database.SqlQuery<AsesoriaDocenteReportViewModel>(QueryOriginal).ToList();
                if (reportOG.Count < 1)
                {
                    return BadRequest("No se hallaron resultados con los parametros de búsqueda.");
                }
                var formattedList = report.ToList()
                    .Select(x => new
                    {
                        x.Origen,
                        x.Carrera,
                        Docente = x.TeacherFullName,
                        x.Modalidad,
                        Tarea = x.TipoTarea,
                        Alumno = x.StudentFullName,
                        Mes = x.MesLiteral,
                        x.Gestion,
                        x.TotalBruto,
                        x.Deduccion,
                        x.IUE,
                        x.IUEExterior,
                        x.IT,
                        x.TotalNeto,
                        x.Observaciones,
                        Dup = x.Ignore,
                        x.TipoPago
                    });
                return Ok(formattedList);
            }
            catch (Exception exception)
            {
                return BadRequest("Ocurrió un problema. Comuniquese con el administrador. " + exception);
            }
        }

        // info categoria y precio de docentes para el registro
        [HttpGet]
        [Route("api/DocentesList/{id}")]
        public IHttpActionResult DocentesList(string id)
        {
            // Hacer un union con los docentes que no sean indepedientes, es decir que sean de civil nomas, por su jobTitle
            var activeDocentes = _context.Database.SqlQuery<AsesoriaTeachers>("SELECT * FROM (" +
                                                                              "(select lc.\"CUNI\", fn.\"FullName\", lc.\"BranchesId\", true as \"TipoPago\", pe.\"Categoria\", b.\"Abr\" \"Regional\", ca.\"Precio\"" +
            "\r\nfrom " + CustomSchema.Schema + ".\"ContractDetail\" lc " +
            "\r\ninner join " + CustomSchema.Schema + ".\"FullName\" fn on fn.\"CUNI\" = lc.\"CUNI\" " +
            "\r\ninner join " + CustomSchema.Schema + ".\"People\" pe on pe.\"CUNI\" = lc.\"CUNI\"" +
            "\r\ninner join " + CustomSchema.Schema + ".\"Categoria\" ca on ca.\"Cat\" = pe.\"Categoria\"" +
            "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" b on b.\"Id\" = lc.\"BranchesId\"" +
            "\r\nwhere pe.\"Categoria\" is not null " +
            "\r\ngroup by lc.\"CUNI\", fn.\"FullName\", lc.\"BranchesId\",pe.\"Categoria\",b.\"Abr\",ca.\"Precio\")" +
            // aquí juntamos a las personas de ADMNALRHH con los profesores independientes, es decir que estan como socios de negocio
            " UNION ALL " +
            " (select cv.\"SAPId\" as \"CUNI\",ocrd.\"CardName\" \"FullName\", br.\"Id\" as \"BranchesId\",  false as \"TipoPago\", cv.\"Categoria\", br.\"Abr\" \"Regional\", 0 \"Precio\"" +
            "\r\nfrom " + CustomSchema.Schema + ".\"Civil\" cv " +
            "\r\ninner join  " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".OCRD on cv.\"SAPId\" = ocrd.\"CardCode\" " +
            "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on cv.\"BranchesId\" = br.\"Id\"" +
            "\r\nwhere ocrd.\"frozenFor\" = 'N'" +
            "\r\ngroup by cv.\"SAPId\",ocrd.\"CardName\", br.\"Id\", cv.\"Categoria\",br.\"Abr\" ))" +
            " WHERE \"CUNI\" LIKE '%" + id + "%'" +
            "\r\norder by \"FullName\""
            // "where oh.\"jobTitle\" like '%DOCENTE%' "
            ).ToList();


            var user = auth.getUser(Request);

            var filteredList = auth.filerByRegional(activeDocentes.AsQueryable(), user);

            return Ok(filteredList);
        }
    }
}