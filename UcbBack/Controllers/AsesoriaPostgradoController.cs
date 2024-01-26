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
using System.Web.WebPages;
using Microsoft.Ajax.Utilities;
using SAPbobsCOM;

namespace UcbBack.Controllers
{
    public class AsesoriaPostgradoController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidateAuth auth;
        private B1Connection B1;

        public AsesoriaPostgradoController()
        {
            _context = new ApplicationDbContext();
            B1 = B1Connection.Instance();
            auth = new ValidateAuth();
        }
        // convertir a mes literal
        public List<AsesoriaPostgradoViewModel> mesLiteral(string query)
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
            var rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(query).ToList();
            List<AsesoriaPostgradoViewModel> list = new List<AsesoriaPostgradoViewModel>();
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

        // registro por Id
        [HttpGet]
        [Route("api/AsesoriaPostgrado/{id}")]
        public IHttpActionResult getIndividualRecord(int id)
        {
            //datos para la tabla histórica
            var uniqueRecord = _context.AsesoriaPostgrado.FirstOrDefault(x => x.Id == id);

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
        [Route("api/AsesoriaPostgrado")]
        public IHttpActionResult getAsesoriaPostgrado([FromUri] string by)
        {
            // datos para la tabla histórica
            string query = "select a.\"Id\", a.\"TeacherCUNI\", a.\"TeacherBP\", \r\na.\"BranchesId\", br.\"Abr\" as \"Regional\", a.\"Proyecto\"," +
                           "a.\"Modulo\", a.\"DependencyCod\", a.\"Horas\", \r\na.\"MontoHora\", a.\"TotalNeto\", a.\"TotalBruto\", a.\"Mes\"," +
                           " a.\"Gestion\", \r\na.\"Observaciones\", a.\"Deduccion\", t.\"Abr\" as \"TipoTarea\", \r\nnull as \"MesLiteral\", " +
                           "a.\"Origen\", \r\ncase when fn.\"FullName\" is null then c.\"CardName\"\r\nwhen c.\"CardName\" is null then fn.\"FullName\"\r\nend as \"TeacherFullName\", " +
                           "case when a.\"Ignore\" = true then 'D' else '' end as \"Ignored\", a.\"Origen\"" +
                           "\r\nfrom "+CustomSchema.Schema+".\"AsesoriaPostgrado\" a " +
                           "\r\ninner join " + CustomSchema.Schema + ".\"TipoTarea\" t \r\non a.\"TipoTareaId\"=t.\"Id\" " +
                           "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br \r\non a.\"BranchesId\"=br.\"Id\" " +
                           "\r\nleft join " + CustomSchema.Schema + ".\"FullName\" fn \r\non a.\"TeacherCUNI\"=fn.\"CUNI\" " +
                           "\r\nleft join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"OCRD\" c\r\non a.\"TeacherBP\"=c.\"CardCode\"";
            string orderBy = "order by a.\"Gestion\" desc, a.\"Mes\" desc, a.\"Id\" asc, a.\"Proyecto\" asc, a.\"TeacherCUNI\" asc ";
            var rawresult = new List<AsesoriaPostgradoViewModel>();
            var user = auth.getUser(Request);

            if (by.Equals("APROBADO"))
            {
                string customQuery = query + "where a.\"Estado\"='APROBADO' " + orderBy;
                // Mes a literal
                rawresult = mesLiteral(customQuery);
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.Proyecto,
                        x.Modulo,
                        Profesor = x.TeacherFullName,
                        Tarea = x.TipoTarea,
                        Mes = x.MesLiteral,
                        x.Gestion,
                        x.Origen,
                        Dup = x.Ignored
                    });
                return Ok(filteredList);

            }
            else if (by.Equals("PRE-APROBADO"))
            {
                string customQuery = query + "where a.\"Estado\"='PRE-APROBADO' and a.\"Factura\"=false " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Proyecto,
                        x.Modulo,
                        x.TipoTarea,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        x.Origen,
                        x.Ignored
                    }); ;
                return Ok(filteredList);

            }
            else if (by.Equals("REGISTRADO-DEPEN"))
            {
                //para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Origen\"='DEPEN' and a.\"Factura\"=false " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Proyecto,
                        x.Modulo,
                        x.TipoTarea,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        x.Ignored
                    }); ; ;
                return Ok(filteredList);

            }
            else if (by.Equals("REGISTRADO-INDEP"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Origen\"='INDEP' and a.\"Factura\"=false " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Proyecto,
                        x.Modulo,
                        x.TipoTarea,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        x.Ignored
                    }); ; ;
                return Ok(filteredList);
            }
            else if (by.Equals("REGISTRADO-OR"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Origen\"='OR' and a.\"Factura\"=false " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Proyecto,
                        x.Modulo,
                        x.TipoTarea,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        x.Ignored
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("REGISTRADO-FAC"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Factura\"=true " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Proyecto,
                        x.Modulo,
                        x.TipoTarea,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        x.Ignored
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("REGISTRADO-EXT"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Origen\"='EXT' and a.\"Factura\"=false " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Proyecto,
                        x.Modulo,
                        x.TipoTarea,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        x.Ignored
                    }); ; ;
                return Ok(filteredList);
            }
            else if (by.Equals("VERIFICADO-INDEP"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='VERIFICADO' " + "and a.\"Origen\"='INDEP' and a.\"Factura\"=false " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Proyecto,
                        x.Modulo,
                        x.TipoTarea,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        x.Ignored
                    }); ; ;
                return Ok(filteredList);
            }
            else if (by.Equals("VERIFICADO-OR"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='VERIFICADO' " + "and a.\"Origen\"='OR' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Proyecto,
                        x.Modulo,
                        x.TipoTarea,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        x.Ignored
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("VERIFICADO-DEPEN"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='VERIFICADO' " + "and a.\"Origen\"='DEPEN' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Proyecto,
                        x.Modulo,
                        x.TipoTarea,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        x.Ignored
                    }); ; ;
                return Ok(filteredList);

            }
            else if (by.Equals("VERIFICADO-FAC"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='VERIFICADO' " + "and a.\"Factura\"= true " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Proyecto,
                        x.Modulo,
                        x.TipoTarea,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        x.Ignored
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("VERIFICADO-EXT"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='VERIFICADO' " + "and a.\"Origen\"='EXT' and a.\"Factura\"=false " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Id,
                        x.TeacherFullName,
                        x.Proyecto,
                        x.Modulo,
                        x.TipoTarea,
                        TotalNeto = string.Format("{0,00}", x.TotalNeto),
                        TotalBruto = string.Format("{0,00}", x.TotalBruto),
                        x.Ignored
                    }); ; ;
                return Ok(filteredList);
            }
            else
            {
                return BadRequest();
            }

        }

        //conseguir los registros del docente por nombre completo, esto se debe a que no todos los registros tienen cuni o socio de negocio
        [HttpGet]
        [Route("api/TeacherHistory/{id}")]
        public IHttpActionResult TeachingRecords(int id)
        {
            var aux = _context.AsesoriaPostgrado.FirstOrDefault(x => x.Id == id);
            string record = null;
            if (aux.Origen == "INDEP")
            {
                record = " a.\"TeacherBP\" = '" + aux.TeacherBP + "' ";
            }
            else
            {
                
                    record = " a.\"TeacherCUNI\" = '" + aux.TeacherCUNI + "' ";
                
            }
            //muestra los registros aprobados del docente X
            var query = "select a.*   ,\r\ncase when fn.\"FullName\" is null then c.\"CardName\"\r\nwhen c.\"CardName\" is null then fn.\"FullName\"\r\nend as \"TeacherFullName\"," +
                        " t.\"Abr\" as \"TipoTarea\" " +
                        "from " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" a " +
                        "inner join " + CustomSchema.Schema + ".\"TipoTarea\" t " +
                             "on a.\"TipoTareaId\"=t.\"Id\" " +
                        "\r\nleft join " + CustomSchema.Schema + ".\"FullName\" fn \r\non a.\"TeacherCUNI\"=fn.\"CUNI\"  " +
                        "\r\nleft join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"OCRD\" c\r\non a.\"TeacherBP\"=c.\"CardCode\"  " +
                        "where "  + record + 
                        "   and \"Estado\"='APROBADO' " +
                        "order by a.\"Gestion\" desc, a.\"Mes\" desc, a.\"Proyecto\" asc, a.\"TeacherCUNI\" asc ";
            var allTeachingRecords = mesLiteral(query).Select(x => new { x.Id, x.Origen, x.Proyecto, x.Modulo, x.TipoTarea, x.Horas, x.MontoHora, x.TotalBruto, x.Deduccion, x.TotalNeto, Mes = x.MesLiteral, x.Gestion });

            return Ok(allTeachingRecords);
        }

        // lista de docentes para el registro
        [HttpGet]
        [Route("api/DocentesListPostgrado")]
        public IHttpActionResult DocentesList()
        {
            //Hacer un union con los docentes que no sean indepedientes, es decir que sean de civil nomas, por su jobTitle
            var activeDocentes = _context.Database.SqlQuery<AsesoriaTeachers>("(select lc.\"CUNI\", fn.\"FullName\",lc.\"StartDate\", lc.\"EndDate\", lc.\"BranchesId\", " +
                                                                              "true as \"TipoPago\", pe.\"Categoria\", lc.\"Branches\" \"Regional\"" +
                                                                              "\r\nfrom " + CustomSchema.Schema + ".\"LASTCONTRACTS\" lc " +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"FullName\" fn \r\non fn.\"CUNI\"=lc.\"CUNI\" " +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"People\" pe \r\non pe.\"CUNI\"=lc.\"CUNI\" " +
                                                                              "\r\ngroup by lc.\"CUNI\", fn.\"FullName\",lc.\"StartDate\", lc.\"EndDate\", lc.\"BranchesId\", " +
                                                                              "pe.\"Categoria\",lc.\"FullName\",lc.\"Branches\"\r\norder by fn.\"FullName\")" +
                                                                              "\r\nUNION ALL " +
                                                                              "\r\n(select cv.\"SAPId\" as" +
                                                                              " \"CUNI\",ocrd.\"CardName\" \"FullName\",\r\n null as \"StartDate\", null as \"EndDate\", br.\"Id\" as" +
                                                                              " \"BranchesId\", \r\nfalse as \"TipoPago\", cv.\"Categoria\", br.\"Abr\" \"Regional\" " +
                                                                              "\r\nfrom " + CustomSchema.Schema + ".\"Civil\" cv " +
                                                                              "\r\ninner join  " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".OCRD ocrd on cv.\"SAPId\" = ocrd.\"CardCode\" " +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on cv.\"BranchesId\"=br.\"Id\"" +
                                                                              "\r\nwhere ocrd.\"frozenFor\" = 'N') \r\norder by \"FullName\"").ToList();


            var user = auth.getUser(Request);

            var filteredList = auth.filerByRegional(activeDocentes.AsQueryable(), user);

            return Ok(filteredList);
        }

        [HttpGet]
        [Route("api/DocentesListAllPostgrado")]
        public IHttpActionResult DocentesListAll()
        {
            //Hacer un union con los docentes que no sean indepedientes, es decir que sean de civil nomas, por su jobTitle
            var activeDocentes = _context.Database.SqlQuery<AsesoriaTeachers>("(select lc.\"CUNI\", fn.\"FullName\",lc.\"StartDate\", lc.\"EndDate\", lc.\"BranchesId\", " +
                                                                              "true as \"TipoPago\", pe.\"Categoria\", lc.\"Branches\" \"Regional\"" +
                                                                              "\r\nfrom " + CustomSchema.Schema + ".\"LASTCONTRACTS\" lc " +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"FullName\" fn \r\non fn.\"CUNI\"=lc.\"CUNI\" " +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"People\" pe \r\non pe.\"CUNI\"=lc.\"CUNI\" " +
                                                                              "\r\ngroup by lc.\"CUNI\", fn.\"FullName\",lc.\"StartDate\", lc.\"EndDate\", lc.\"BranchesId\", " +
                                                                              "pe.\"Categoria\",lc.\"FullName\",lc.\"Branches\"\r\norder by fn.\"FullName\")" +
                                                                              "\r\nUNION ALL " +
                                                                              "\r\n(select cv.\"SAPId\" as" +
                                                                              " \"CUNI\",ocrd.\"CardName\" \"FullName\",\r\n null as \"StartDate\", null as \"EndDate\", br.\"Id\" as" +
                                                                              " \"BranchesId\", \r\nfalse as \"TipoPago\", cv.\"Categoria\", br.\"Abr\" \"Regional\" " +
                                                                              "\r\nfrom " + CustomSchema.Schema + ".\"Civil\" cv " +
                                                                              "\r\ninner join  " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".OCRD ocrd on cv.\"SAPId\" = ocrd.\"CardCode\" " +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on cv.\"BranchesId\"=br.\"Id\"" +
                                                                              "\r\nwhere ocrd.\"frozenFor\" = 'N') \r\norder by \"FullName\""
            ).ToList();


            return Ok(activeDocentes);
        }

        // para obtener el cuerpo del reporte PDF
        [HttpGet]
        [Route("api/PDFReportBodyPostgrado")]
        public IHttpActionResult PDFReport([FromUri] string part)
        {
            string query = "";
            var report = new List<AsesoriaPostgradoViewModel>();
            string[] data = part.Split(';');
            string section = data[0];
            string state = data[1];
            string origin = data[2];
            // query para generar todos los datos de cada docente, ordenado por carrera y docente
            switch (section)
            {
                case "Body":
                    // obtiene el cuerpo de la tabla para el PDF
                    // join para el nombre de la carrera
                    query = "select \r\na.\"Id\", " +
                            "\r\ncase when fn.\"FullName\" is null then c.\"CardName\"when c.\"CardName\" is null then fn.\"FullName\"end as \"TeacherFullName\", " +
                            "\r\nt.\"Abr\" as \"TipoTarea\"," +
                            "\r\na.\"BranchesId\"," +
                            "\r\na.\"Proyecto\" || '-' || op.\"PrjName\" \"Proyecto\"," +
                            "\r\na.\"Modulo\" || ' ' || pm.\"NameModule\" \"Modulo\", " +
                            "\r\na.\"Horas\", " +
                            "\r\na.\"MontoHora\", " +
                            "\r\na.\"TotalBruto\", " +
                            "\r\na.\"Deduccion\", " +
                            "\r\na.\"Origen\", " +
                            "\r\ncase when \"IUE\" is null then 0 else \"IUE\" end as \"IUE\", " +
                            "\r\ncase when \"IT\" is null then 0 else \"IT\" end as \"IT\", " +
                            "\r\ncase when \"IUEExterior\" is null then 0 else \"IUEExterior\" end as \"IUEExterior\", " +
                            "\r\na.\"StudentFullName\"," +
                            "\r\na.\"TotalNeto\", " +
                            "\r\na.\"Observaciones\", " +
                            "\r\ncase when a.\"StudentFullName\" is null then 'ND' else a.\"StudentFullName\" end as \"StudentFullName\", " +
                            "\r\ncase when a.\"Ignore\" = true then 'D' else '' end as \"Ignored\"" +
                            "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" a " +
                            "\r\ninner join " + CustomSchema.Schema + ".\"ProjectModules\" pm on pm.\"CodModule\"=a.\"Modulo\" and pm.\"CodProject\" = a.\"Proyecto\" " +
                            "\r\ninner join " + CustomSchema.Schema + ".\"TipoTarea\" t on a.\"TipoTareaId\"=t.\"Id\" " +
                            "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on a.\"BranchesId\"=br.\"Id\" " +
                            "\r\nleft join " + CustomSchema.Schema + ".\"FullName\" fn on a.\"TeacherCUNI\"=fn.\"CUNI\" " +
                            "\r\nleft join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"OCRD\" c on a.\"TeacherBP\"=c.\"CardCode\"" +
                            "\r\nleft join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"OPRJ\" op on op.\"PrjCode\"=a.\"Proyecto\"" +
                        "where " +
                            "a.\"Estado\"='" + state + "' " +
                            "and a.\"Origen\" like '%" + origin + "%'" +
                        " order by a.\"Proyecto\" asc, a.\"Modulo\" asc, a.\"Gestion\" desc, a.\"Mes\" desc, \"TeacherFullName\" ";
                    report = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(query).ToList();
                    break;

                case "Results":
                    // obtiene los resultados al pie de cada tabla, por carrera
                    query = "select " +
                            "\r\na.\"BranchesId\"," +
                            "\r\na.\"Proyecto\" || '-' || op.\"PrjName\" \"Proyecto\"," +
                            "\r\nsum(a.\"TotalBruto\") \"TotalBruto\", " +
                            "\r\nsum(a.\"Deduccion\") \"Deduccion\", " +
                            "case when sum(\"IUE\") is null then 0 else sum(\"IUE\") end as \"IUE\",  " +
                            "case when sum(\"IT\") is null then 0 else sum(\"IT\") end as \"IT\", " +
                            "case when sum(\"IUEExterior\") is null then 0 else sum(\"IUEExterior\") end as \"IUEExterior\", " +
                            "\r\nsum(a.\"TotalNeto\") \"TotalNeto\"" +
                            "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" a " +
                            "\r\ninner join " + CustomSchema.Schema + ".\"ProjectModules\" pm on pm.\"CodModule\"=a.\"Modulo\" and pm.\"CodProject\" = a.\"Proyecto\" " +
                            "\r\ninner join " + CustomSchema.Schema + ".\"TipoTarea\" t on a.\"TipoTareaId\"=t.\"Id\" " +
                            "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on a.\"BranchesId\"=br.\"Id\" " +
                            "\r\nleft join " + CustomSchema.Schema + ".\"FullName\" fn on a.\"TeacherCUNI\"=fn.\"CUNI\" " +
                            "\r\nleft join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"OCRD\" c on a.\"TeacherBP\"=c.\"CardCode\"" +
                            "\r\nleft join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"OPRJ\" op on op.\"PrjCode\"=a.\"Proyecto\"" +
                        "where " +
                            "a.\"Estado\"='" + state + "' " +
                            "and a.\"Origen\" like '%" + origin + "%'" +
                        " group by a.\"Proyecto\",op.\"PrjName\",a.\"BranchesId\" " +
                        " order by a.\"Proyecto\" asc ";
                    report = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(query).ToList();
                    break;

                case "FinalResult":
                    // obtiene los resultados totales
                    query = "select " +
                            "sum(\"TotalBruto\") as \"TotalBruto\", " +
                            "sum(\"Deduccion\") as \"Deduccion\", " +
                            "case when sum(\"IUE\") is null then 0 else sum(\"IUE\") end as \"IUE\",  " +
                            "case when sum(\"IT\") is null then 0 else sum(\"IT\") end as \"IT\", " +
                            "case when sum(\"IUEExterior\") is null then 0 else sum(\"IUEExterior\") end as \"IUEExterior\", " +
                            "sum(\"TotalNeto\") as \"TotalNeto\", \"BranchesId\"" +
                        "from " +
                            CustomSchema.Schema + ".\"AsesoriaPostgrado\" a " +

                            "inner join " + CustomSchema.Schema + ".\"Branches\" b " +
                            "on b.\"Id\" = a.\"BranchesId\" " +
                        "where " +
                            "\"Estado\"='" + state + "' " +
                            "and a.\"Origen\" like '%" + origin + "%'" +
                        "group by \"BranchesId\" ";
                    report = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(query).ToList();
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
                    x.TeacherFullName,
                    x.Origen,
                    Alumno = x.StudentFullName,
                    x.TipoTarea,
                    x.Proyecto,
                    x.Modulo,
                    x.Horas,
                    x.MontoHora,
                    Total_Bruto = x.TotalBruto,
                    x.Deduccion,
                    x.IUE,
                    x.IT,
                    IUEExt = x.IUEExterior,
                    Total_Neto = x.TotalNeto,
                    x.Observaciones,
                    Dup = x.Ignored,
                    x.BranchesId
                });

                return Ok(filteredListBody);
            }
            else if (section.Equals("Results"))
            {
                var filteredListResult = auth.filerByRegional(report.AsQueryable(), user).ToList().Select(x => new
                {
                    x.Proyecto,
                    Alumno = x.StudentFullName,
                    Total_Bruto = x.TotalBruto,
                    x.Deduccion,
                    x.IUE,
                    x.IT,
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
                    Alumno = x.StudentFullName,
                    Deduccion = x.Deduccion,
                    IUE = x.IUE,
                    IT = x.IT,
                    IUEExt = x.IUEExterior,
                    Total_Neto = x.TotalNeto,
                });
                return Ok(filteredListResult);
            }
        }

        //para generar el archivo POSGRADO de SALOMON
        [HttpGet]
        [Route("api/ToPostgradoFile")]
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
            string valid = "select  \r\n\"Document\" ,\"FirstSurName\", \"SecondSurName\",  \"Names\", \"MariedSurName\"" +
                               "\r\nfrom( \r\nselect \r\n    p.\"Document\" ,p.\"FirstSurName\", p.\"SecondSurName\", " +
                               "\r\n    p.\"Names\", p.\"MariedSurName\", o.\"PrjName\", o.\"U_Tipo\", o.\"U_PEI_PO\",o.\"PrjCode\", " +
                               "\r\n    a.\"TotalNeto\", t.\"Tarea\" \"TipoTarea\",\r\n    a.\"TeacherCUNI\" as \"CUNI\", a.\"DependencyCod\" as \"Dependency\", a.\"BranchesId\" " +
                               "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" a \r" +
                               "\n    inner join " + CustomSchema.Schema + ".\"People\" p " +
                               "\r\n    on a.\"TeacherCUNI\"=p.\"CUNI\" " +
                               "\r\n    inner join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprj o on o.\"PrjCode\" = a.\"Proyecto\"" +
                               "\r\n    inner join " + CustomSchema.Schema + ".\"LASTCONTRACTS\" lc " +
                               "\r\n    on a.\"TeacherCUNI\"=lc.\"CUNI\" " +
                               "\r\n    inner join " + CustomSchema.Schema + ".\"Branches\" br " +
                               "\r\n    on a.\"BranchesId\"=br.\"Id\" " +
                               "\r\n    inner join " + CustomSchema.Schema + ".\"TipoTarea\" t" +
                               "\r\n    on a.\"TipoTareaId\"=t.\"Id\" \r\nwhere " +
                               "\r\n    a.\"Estado\"='PRE-APROBADO' " +
                               "\r\n    and br.\"Abr\" ='" + segmento + "' " +
                               "\r\n    and a.\"Origen\"='DEPEN' " +
                               "\r\n   and (lc.\"EndDate\" is not null and lc.\"EndDate\" < '" + Auxdate.Year + "-" + Auxdate.Month + "-" + Auxdate.Day + "')" +
                               "\r\norder by a.\"Id\" desc) " +
                               "\r\n group by \"Document\" ,\"FirstSurName\", \"SecondSurName\",  \"Names\", \"MariedSurName\"";

            var valido = _context.Database.SqlQuery<DistPostgradoViewModel>(valid).ToList();
            string validPro = "select  \"PrjName\", \"PrjCode\"" +
                              "\r\nfrom( \r\nselect \r\n    p.\"Document\" ,p.\"FirstSurName\", p.\"SecondSurName\", " +
                              "\r\n    p.\"Names\", p.\"MariedSurName\", o.\"PrjName\", o.\"U_Tipo\", o.\"U_PEI_PO\",o.\"PrjCode\", " +
                              "\r\n    a.\"TotalNeto\", t.\"Tarea\" \"TipoTarea\",\r\n    a.\"TeacherCUNI\" as \"CUNI\", a.\"DependencyCod\" as \"Dependency\", a.\"BranchesId\" " +
                              "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" a \r" +
                              "\n    inner join " + CustomSchema.Schema + ".\"People\" p " +
                              "\r\n    on a.\"TeacherCUNI\"=p.\"CUNI\" " +
                              "\r\n    inner join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprj o on o.\"PrjCode\" = a.\"Proyecto\"" +
                              "\r\n    inner join " + CustomSchema.Schema + ".\"LASTCONTRACTS\" lc " +
                              "\r\n    on a.\"TeacherCUNI\"=lc.\"CUNI\" " +
                              "\r\n    inner join " + CustomSchema.Schema + ".\"Branches\" br " +
                              "\r\n    on a.\"BranchesId\"=br.\"Id\" " +
                              "\r\n    inner join " + CustomSchema.Schema + ".\"TipoTarea\" t" +
                              "\r\n    on a.\"TipoTareaId\"=t.\"Id\" " +
                              "\r\nwhere " +
                              "\r\n    a.\"Estado\"='PRE-APROBADO' " +
                              "\r\n    and br.\"Abr\" ='" + segmento + "' " +
                              "\r\n    and a.\"Origen\"='DEPEN' " +
                              "\r\n   and current_date not between o.\"ValidFrom\" and o.\"ValidTo\"" +
                              "\r\norder by a.\"Id\" desc) " +
                              "\r\n group by \"PrjName\", \"PrjCode\"";

            var validoPro = _context.Database.SqlQuery<DistPostgradoViewModel>(validPro).ToList();
            string aux = "", auxPro = "";
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
            if (validoPro.Count > 0)
            {
                for (int i = 0; i < validoPro.Count; i++)
                {
                    auxPro = auxPro + validoPro[i].PrjCode + "-" + validoPro[i].PrjName + ",";
                }
                HttpResponseMessage response =
                    new HttpResponseMessage(HttpStatusCode.InternalServerError);
                response.Content = new StringContent("No se puede generar el archivo porque los siguientes proyectos no se encuentran con una fecha valida: " + auxPro);
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
                            "El periodo seleccionado no es válido para la generación del archivo POSGRADO en la regional " +
                            segmento);
                    response.RequestMessage = Request;
                    return response;
                }
                else
                {
                    var user = auth.getUser(Request);
                    //El query genera el archivo PREGRADO de SALOMON en base a los datos de las tutorías PRE-APROBADAS
                    string query =
                        "select  \r\n\"Document\" ,\"FirstSurName\", \"SecondSurName\",  \"Names\", \"MariedSurName\", \"PrjName\", '' \"Version\",sum(\"TotalNeto\") as \"TotalNeto\", \"Dependency\", \"CUNI\",  " +
                        "case\r\nwhen \"U_Tipo\" = 'E' then 'EC'\r\nwhen \"U_Tipo\" = 'F' then 'FC'\r\nwhen \"U_Tipo\" = 'P' then 'POST'\r\nwhen \"U_Tipo\" = 'S' then 'SA'\r\nwhen \"U_Tipo\" = 'V' then 'INV'\r\nelse \"U_Tipo\"\r\nend as \"U_Tipo\"" +
                        ", \"TipoTarea\", \"U_PEI_PO\", '' \"PeriodoAcademico\", \"PrjCode\"" +
                        "\r\nfrom( \r\nselect \r\n    p.\"Document\" ,p.\"FirstSurName\", p.\"SecondSurName\", " +
                        "\r\n    p.\"Names\", p.\"MariedSurName\", o.\"PrjName\", o.\"U_Tipo\", o.\"U_PEI_PO\",o.\"PrjCode\", " +
                        "\r\n    a.\"TotalNeto\", t.\"Abr\" \"TipoTarea\",\r\n    a.\"TeacherCUNI\" as \"CUNI\", a.\"DependencyCod\" as \"Dependency\", a.\"BranchesId\" " +
                        "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" a \r" +
                        "\n    inner join " + CustomSchema.Schema + ".\"People\" p " +
                        "\r\n    on a.\"TeacherCUNI\"=p.\"CUNI\" " +
                        "\r\n    inner join " + ConfigurationManager.AppSettings["B1CompanyDB"] +
                        ".oprj o on o.\"PrjCode\" = a.\"Proyecto\"" +
                        "\r\n    inner join " + CustomSchema.Schema + ".\"LASTCONTRACTS\" lc " +
                        "\r\n    on a.\"TeacherCUNI\"=lc.\"CUNI\" " +
                        "\r\n    inner join " + CustomSchema.Schema + ".\"Branches\" br " +
                        "\r\n    on a.\"BranchesId\"=br.\"Id\" " +
                        "\r\n    inner join " + CustomSchema.Schema + ".\"TipoTarea\" t" +
                        "\r\n    on a.\"TipoTareaId\"=t.\"Id\" \r\nwhere " +
                        "\r\n    a.\"Estado\"='PRE-APROBADO' " +
                        "\r\n    and br.\"Abr\" ='" + segmento + "' " +
                        "\r\n    and a.\"Origen\"='DEPEN' " +
                        "\r\norder by a.\"Id\" desc) " +
                        "\r\n group by \"Document\" ,\"FirstSurName\", \"SecondSurName\",  \"Names\", \"MariedSurName\", \"PrjName\",\"CUNI\", \"Dependency\", \"BranchesId\" ,\"U_Tipo\", \"TipoTarea\",\"U_PEI_PO\",\"PrjCode\"\r\n order by \"PrjName\" asc, \"FirstSurName\"";

                    var excelContent = _context.Database.SqlQuery<DistPostgradoViewModel>(query).ToList();

                    var filteredWithoutCol = excelContent.Select(x => new
                    {
                        x.Document, x.FirstSurName, x.SecondSurName, x.Names, x.MariedSurName, x.PrjName, x.Version,
                        x.TotalNeto, x.Dependency, x.CUNI, x.U_Tipo, x.TipoTarea, x.U_PEI_PO, x.PeriodoAcademico,
                        x.PrjCode
                    }).ToList();

                    //--------------------------------------------------------Generación del excel------------------------------------------------------------------------
                    //Para las columnas del excel
                    string[] header = new string[]
                    {
                        "Carnet Identidad", "Primer Apellido", "Segundo Apellido",
                        "Nombres", "Apellido Casada", "Nombre del Proyecto", "Versión", "Total Neto Ganado",
                        "Identificador de dependencia", "CUNI",
                        "Tipo Proyecto", "Tipo de tarea asignada", "PEI", "Periodo académico", "Código Proyecto SAP"
                    };
                    var workbook = new XLWorkbook();

                    //Se agrega la hoja de excel
                    var ws = workbook.Worksheets.Add("POSGRADO");
                    /*var range = workbook.Worksheets.Range("A1:B2");
                    range.Value = "Merged A1:B2";
                    range.Merge();
                    range.Style.Alignment.Vertical = AlignmentVerticalValues.Top;*/
                    // Título
                    ws.Cell("A1").Value = "POSGRADO";
                    ws.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Cell("A2").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    ws.Range("A1:O2").Merge();
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
                    response.Content.Headers.ContentDisposition.FileName = segmento + gestion + mes + "POST.xlsx";
                    response.Content.Headers.ContentType =
                        new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                    response.Content.Headers.ContentLength = ms.Length;
                    //La posicion para el comienzo del stream
                    ms.Seek(0, SeekOrigin.Begin);

                    //-----------------------------------------------------Cambios en PRE-APROBADOS ---------------------------------------------------------------------
                    //Actualizar con la fecha a los registros pre-aprobados
                    var docentesPorAprobar = _context.AsesoriaPostgrado.Where(x =>
                        x.Origen.Equals("DEPEN") && x.Estado.Equals("PRE-APROBADO") &&
                        x.BranchesId == segmentoId).ToList();
                    //Se sobrescriben los registros con la fecha actual y el nuevo estado
                    foreach (var docente in docentesPorAprobar)
                    {
                        docente.Mes = Convert.ToInt16(mes);
                        docente.Gestion = Convert.ToInt16(gestion);
                        docente.Estado = "APROBADO";
                        docente.ToAuthAt = DateTime.Now;
                        docente.UserAuth = user.Id;
                    }

                    _context.SaveChanges();

                    return response;

                }
            }
        }
        // para generar el archivo PROYECTOS de SARAI
        [HttpGet]
        [Route("api/ToProyectosFile")]
        public HttpResponseMessage ToCarreraFile([FromUri] string data)
        {
            string[] info = data.Split(';');
            int segmentoId = Convert.ToInt16(info[0]);
            string segmento = _context.Branch.FirstOrDefault(x => x.Id == segmentoId).Abr;
            // el mes y la gestion son necesarios para guardar el registro histórico ISAAC
            string mes = (info[1]);
            string gestion = info[2];
            string proy = info[3];
            var user = auth.getUser(Request);
            //El query genera el archivo PREGRADO de SALOMON en base a los datos de las tutorías PRE-APROBADAS
            var auxDates = _context.Database.SqlQuery<Serv_ProyectosViewModel>("select * from " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprj where \"PrjCode\" = '" + proy + "' and current_date between \"ValidFrom\" and \"ValidTo\"").ToList();
            if (auxDates.Count < 1)
            {
                HttpResponseMessage response =
                    new HttpResponseMessage(HttpStatusCode.InternalServerError);
                response.Content = new StringContent("No se puede generar el archivo porque el proyecto no tiene una fecha valida.");
                response.RequestMessage = Request;
                return response;
            }
            else
            {
                string query =
                "select a.\"TeacherBP\" as \"Codigo_Socio\", case when fn.\"FullName\" is null then cr.\"CardName\" when cr.\"CardName\" is null then fn.\"FullName\" end as \"Nombre_Socio\", " +
                "\r\na.\"DependencyCod\" as \"Cod_Dependencia\", o.\"U_PEI_PO\" as \"PEI_PO\", \r\nprj.\"NameModule\" \"Nombre_del_Servicio\", \r\no.\"PrjCode\" \"Código_Proyecto_SAP\"," +
                "\r\n\r\ncase \r\nwhen a.\"Modulo\" = '0' then substring(concat ('', concat (concat(substring(a.\"StudentFullName\",1,20), ' '),  substring(prj.\"NameModule\",1,20))),1,40)\r\nelse substring(concat ('', concat (concat(a.\"Modulo\", ' '), concat ('', prj.\"NameModule\"))),1,40)\r\nend as \"Nombre_del_Proyecto\",\r\n'' \"Versión\", '' \"PeriodoAcadémico\", " +
                "t.\"Abr\" as \"Tipo_Tarea_Asignada\", \r\ncase when o.\"U_Tipo\" = 'E' then 'CC_EC'\r\nwhen o.\"U_Tipo\" = 'F' then 'CC_FC'\r\nwhen o.\"U_Tipo\" = 'P' then 'CC_POST'" +
                "\r\nwhen o.\"U_Tipo\" = 'S' then 'CC_SA'\r\nwhen o.\"U_Tipo\" = 'V' then 'CC_INV'\r\nelse '' end as  \"Cuenta_Asignada\",\r\na.\"TotalBruto\" as \"Monto_Contrato\", " +
                "a.\"IUE\" as \"Monto_IUE\", a.\"IT\" as \"Monto_IT\", a.\"TotalNeto\" as \"Monto_a_Pagar\",  \r\na.\"Observaciones\" " +
                "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" a " +
                " \r\ninner join " + CustomSchema.Schema + ".\"ProjectModules\" prj on prj.\"CodProject\" = a.\"Proyecto\" and prj.\"CodModule\" = '0'" +
                "\r\ninner join " + CustomSchema.Schema + ".\"Civil\" c " +
                "\r\non a.\"TeacherBP\"=c.\"SAPId\" " +
                "\r\ninner join " + CustomSchema.Schema + ".\"TipoTarea\" t " +
                "\r\non a.\"TipoTareaId\"=t.\"Id\" " +
                "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br " +
                "\r\non a.\"BranchesId\"=br.\"Id\" " +
                "\r\ninner join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprj o " +
                "\r\non a.\"Proyecto\"=o.\"PrjCode\" " +
                "\r\nleft join " + CustomSchema.Schema + ".\"FullName\" fn \r\non a.\"TeacherCUNI\"=fn.\"CUNI\"  " +
                "\r\nleft join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"OCRD\" cr\r\non a.\"TeacherBP\"=cr.\"CardCode\"  " +
                "\r\nwhere \r\n   a.\"Estado\"='PRE-APROBADO' " +
                "\r\nand br.\"Abr\" ='" + segmento + "' " +
                "\r\n   and a.\"Origen\"='INDEP' " +
                "\r\n   and a.\"Proyecto\"='" + proy + "' " +
                "\r\norder by a.\"Id\" asc;";

                var excelContent = _context.Database.SqlQuery<Serv_ProyectosViewModel>(query).ToList();

                //Para las columnas del excel
                string[] header = new string[]{"Codigo_Socio", "Nombre_Socio", "Cod_Dependencia",
                                            "PEI_PO", "Nombre_del_Servicio", "Codigo_Proyecto_SAP", "Nombre_del_Proyecto", "Versión", "Periodo_Académico",
                                            "Tipo_Tarea_Asignada", "Cuenta_Asignada",
                                            "Monto_Contrato","Monto_IUE","Monto_IT", "IUEExterior", "Monto_a_Pagar", "Observaciones"};
                var workbook = new XLWorkbook();

                //Se agrega la hoja de excel
                var ws = workbook.Worksheets.Add("Plantilla_PROYECTOS");

                // Rango hoja excel
                //1,1: es la posicion inicial; 2,header.Length: es el alto y el ancho
                var rngTable = ws.Range(1, 1, 2, header.Length);

                //Bordes para las columnas
                var columns = ws.Range(2, 1, excelContent.Count + 1, header.Length);
                columns.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                columns.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                //auxiliar: desde qué línea ponemos los nombres de columna
                var headerPos = 1;

                //Ciclo para asignar los nombres a las columnas y darles formato
                for (int i = 0; i < header.Length; i++)
                {
                    ws.Cell(headerPos, i + 1).Value = header[i];
                    ws.Cell(headerPos, i + 1).Style.Font.Bold = true;
                    ws.Cell(headerPos, i + 1).Style.Font.FontColor = XLColor.White;
                    ws.Cell(headerPos, i + 1).Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent1);
                }

                //Aquí hago el attachment del query a mi hoja de de excel
                ws.Cell(2, 1).Value = excelContent.AsEnumerable();

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
                response.Content.Headers.ContentDisposition.FileName = segmento + "-CC_PROYECTOS.xlsx";
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                response.Content.Headers.ContentLength = ms.Length;
                //La posicion para el comienzo del stream
                ms.Seek(0, SeekOrigin.Begin);

                //-----------------------------------------------------Cambios en PRE-APROBADOS INDEP ---------------------------------------------------------------------
                //Actualizar con la fecha a los registros pre-aprobados
                var branchesId = _context.Branch.FirstOrDefault(x => x.Abr == segmento);
                var docentesPorAprobar = _context.AsesoriaPostgrado.Where(x => x.Origen.Equals("INDEP") && x.Estado.Equals("PRE-APROBADO") && x.BranchesId == segmentoId && x.Proyecto == proy).ToList();
                //Se sobrescriben los registros con la fecha actual y el nuevo estado
                foreach (var docente in docentesPorAprobar)
                {
                    docente.Mes = Convert.ToInt16(mes);
                    docente.Gestion = Convert.ToInt16(gestion);
                    docente.Estado = "APROBADO";
                    docente.ToAuthAt = DateTime.Now;
                    docente.UserAuth = user.Id;
                }

                _context.SaveChanges();

                return response;
            }
        }

        // para generar el archivo PROYECTOS de SARAI
        [HttpGet]
        [Route("api/ProyectosExt")]
        public HttpResponseMessage ProyectosExt([FromUri] string data)
        {
            string[] info = data.Split(';');
            int segmentoId = Convert.ToInt16(info[0]);
            string segmento = _context.Branch.FirstOrDefault(x => x.Id == segmentoId).Abr;
            // el mes y la gestion son necesarios para guardar el registro histórico ISAAC
            string mes = (info[1]);
            string gestion = info[2];
            string proy = info[3];
            var user = auth.getUser(Request);
            //El query genera el archivo PREGRADO de SALOMON en base a los datos de las tutorías PRE-APROBADAS
            var auxDates = _context.Database.SqlQuery<Serv_ProyectosViewModel>("select * from " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprj where \"PrjCode\" = '" + proy + "' and current_date between \"ValidFrom\" and \"ValidTo\"").ToList();
            if (auxDates.Count < 1)
            {
                HttpResponseMessage response =
                    new HttpResponseMessage(HttpStatusCode.InternalServerError);
                response.Content = new StringContent("No se puede generar el archivo porque el proyecto no tiene una fecha valida.");
                response.RequestMessage = Request;
                return response;
            }
            else
            {
                string query =
                "select a.\"TeacherBP\" as \"Codigo_Socio\", case when fn.\"FullName\" is null then cr.\"CardName\" when cr.\"CardName\" is null then fn.\"FullName\" end as \"Nombre_Socio\", " +
                "\r\na.\"DependencyCod\" as \"Cod_Dependencia\", o.\"U_PEI_PO\" as \"PEI_PO\", \r\nprj.\"NameModule\" \"Nombre_del_Servicio\", \r\no.\"PrjCode\" \"Código_Proyecto_SAP\"," +
                "\r\n\r\ncase \r\nwhen a.\"Modulo\" = '0' then substring(concat ('', concat (concat(substring(a.\"StudentFullName\",1,20), ' '),  substring(prj.\"NameModule\",1,20))),1,40)\r\nelse substring(concat ('', concat (concat(a.\"Modulo\", ' '), concat ('', prj.\"NameModule\"))),1,40)\r\nend as \"Nombre_del_Proyecto\",\r\n'' \"Versión\", '' \"PeriodoAcadémico\", " +
                "t.\"Abr\" as \"Tipo_Tarea_Asignada\", \r\ncase when o.\"U_Tipo\" = 'E' then 'CC_EC'\r\nwhen o.\"U_Tipo\" = 'F' then 'CC_FC'\r\nwhen o.\"U_Tipo\" = 'P' then 'CC_POST'" +
                "\r\nwhen o.\"U_Tipo\" = 'S' then 'CC_SA'\r\nwhen o.\"U_Tipo\" = 'V' then 'CC_INV'\r\nelse '' end as  \"Cuenta_Asignada\",\r\na.\"TotalBruto\" as \"Monto_Contrato\", " +
                "a.\"IUE\" as \"Monto_IUE\", a.\"IT\" as \"Monto_IT\", a.\"IUEExterior\" as \"IUEExterior\", a.\"TotalNeto\" as \"Monto_a_Pagar\",  \r\na.\"Observaciones\" " +
                "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" a " +
                " \r\ninner join " + CustomSchema.Schema + ".\"ProjectModules\" prj on prj.\"CodProject\" = a.\"Proyecto\" and prj.\"CodModule\" = '0'" +
                "\r\ninner join " + CustomSchema.Schema + ".\"Civil\" c " +
                "\r\non a.\"TeacherBP\"=c.\"SAPId\" " +
                "\r\ninner join " + CustomSchema.Schema + ".\"TipoTarea\" t " +
                "\r\non a.\"TipoTareaId\"=t.\"Id\" " +
                "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br " +
                "\r\non a.\"BranchesId\"=br.\"Id\" " +
                "\r\ninner join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprj o " +
                "\r\non a.\"Proyecto\"=o.\"PrjCode\" " +
                "\r\nleft join " + CustomSchema.Schema + ".\"FullName\" fn \r\non a.\"TeacherCUNI\"=fn.\"CUNI\"  " +
                "\r\nleft join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"OCRD\" cr\r\non a.\"TeacherBP\"=cr.\"CardCode\"  " +
                "\r\nwhere \r\n   a.\"Estado\"='PRE-APROBADO' " +
                "\r\nand br.\"Abr\" ='" + segmento + "' " +
                "\r\n   and a.\"Origen\"='EXT' " +
                "\r\n   and a.\"Proyecto\"='" + proy + "' " +
                "\r\norder by a.\"Id\" asc;";

                var excelContent = _context.Database.SqlQuery<Serv_ProyectosViewModel>(query).ToList();

                //Para las columnas del excel
                string[] header = new string[]{"Codigo_Socio", "Nombre_Socio", "Cod_Dependencia",
                                            "PEI_PO", "Nombre_del_Servicio", "Codigo_Proyecto_SAP", "Nombre_del_Proyecto", "Versión", "Periodo_Académico",
                                            "Tipo_Tarea_Asignada", "Cuenta_Asignada",
                                            "Monto_Contrato","Monto_IUE","Monto_IT", "IUEExterior", "Monto_a_Pagar", "Observaciones"};
                var workbook = new XLWorkbook();

                //Se agrega la hoja de excel
                var ws = workbook.Worksheets.Add("Plantilla_PROYECTOS");

                // Rango hoja excel
                //1,1: es la posicion inicial; 2,header.Length: es el alto y el ancho
                // var rngTable = ws.Range(1, 1, 2, header.Length);

                //Bordes para las columnas
                var columns = ws.Range(2, 1, excelContent.Count + 1, header.Length);
                columns.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                columns.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                //auxiliar: desde qué línea ponemos los nombres de columna
                var headerPos = 1;

                //Ciclo para asignar los nombres a las columnas y darles formato
                //for (int i = 0; i < header.Length; i++)
                //{
                //    ws.Cell(headerPos, i + 1).Value = header[i];
                //    ws.Cell(headerPos, i + 1).Style.Font.Bold = true;
                //    ws.Cell(headerPos, i + 1).Style.Font.FontColor = XLColor.White;
                //    ws.Cell(headerPos, i + 1).Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent1);
                //}

                ////Aquí hago el attachment del query a mi hoja de de excel
                //ws.Cell(2, 1).Value = excelContent.AsEnumerable();

                ////Ajustar contenidos
                //ws.Columns().AdjustToContents();

                ws.Cell(headerPos, 1).InsertTable(excelContent.AsEnumerable(), "Table");

                // Ajustar contenidos después de insertar la tabla
                ws.Tables.Table(0).ShowAutoFilter = false; // Puedes ajustar esto según tus necesidades
                ws.Tables.Table(0).Theme = XLTableTheme.TableStyleLight1;
                ws.Columns().AdjustToContents();

                //Carga el objeto de la respuesta
                HttpResponseMessage response = new HttpResponseMessage();

                //Array de bytes
                var ms = new MemoryStream();
                workbook.SaveAs(ms);
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StreamContent(ms);
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                response.Content.Headers.ContentDisposition.FileName = segmento + "-CC_PROYECTOS.xlsx";
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                response.Content.Headers.ContentLength = ms.Length;
                //La posicion para el comienzo del stream
                ms.Seek(0, SeekOrigin.Begin);

                ////-----------------------------------------------------Cambios en PRE-APROBADOS EXT ---------------------------------------------------------------------
                ////Actualizar con la fecha a los registros pre-aprobados
                //var branchesId = _context.Branch.FirstOrDefault(x => x.Abr == segmento);
                //var docentesPorAprobar = _context.AsesoriaPostgrado.Where(x => x.Origen.Equals("EXT") && x.Estado.Equals("PRE-APROBADO") && x.BranchesId == segmentoId && x.Proyecto == proy).ToList();
                ////Se sobrescriben los registros con la fecha actual y el nuevo estado
                //foreach (var docente in docentesPorAprobar)
                //{
                //    docente.Mes = Convert.ToInt16(mes);
                //    docente.Gestion = Convert.ToInt16(gestion);
                //    docente.Estado = "APROBADO";
                //    docente.ToAuthAt = DateTime.Now;
                //    docente.UserAuth = user.Id;
                //}

                //_context.SaveChanges();

                return response;
            }
        }
        //Opciones de proyecto para generar el archivo Proyectos SARAI
        [HttpGet]
        [Route("api/GetProjectsOptions/")]
        public IHttpActionResult GetProyectosOpciones()
        {
            //datos para la tabla histórica
            string query =
                "select " +
                "\r\no.\"PrjCode\", o.\"PrjName\", br.\"Id\" \"BranchesId\" " +
                "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" a " +
                "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br " +
                "\r\non a.\"BranchesId\"=br.\"Id\" " +
                "\r\ninner join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprj o " +
                "\r\non a.\"Proyecto\"=o.\"PrjCode\" " +
                "\r\nwhere \r\n   a.\"Estado\"='PRE-APROBADO' " +
                "\r\n group by o.\"PrjCode\", o.\"PrjName\", br.\"Id\"" +
                "\r\norder by o.\"PrjCode\" asc;";


            var rawResult = _context.Database.SqlQuery<OPRJ>(query).Select(x => new
            {
                x.PrjCode,
                x.PrjName,
                x.BranchesId

            }).AsQueryable();

            var user = auth.getUser(Request);

            var result = auth.filerByRegional(rawResult, user).ToList().Select(x => new
            {
                x.PrjCode,
                x.PrjName,
            }).ToList();

            return Ok(result);
        }
        //registro de la tutoria
        [HttpPost]
        [Route("api/AsesoriaPostgrado")]
        public IHttpActionResult Post([FromBody] AsesoriaPostgrado asesoria)
        {
            
            var B1conn = B1Connection.Instance();
            var user = auth.getUser(Request);
            //validacion regional prof y proy
            var ProyReg = _context.Database.SqlQuery<string>("select \"U_Sucursal\" from " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprj where \"PrjCode\" = '" + asesoria.Proyecto + "'").FirstOrDefault();
            var PeopleRegCUNI = _context.Database.SqlQuery<string>("select \"Branches\" from " + CustomSchema.Schema + ".lastcontracts where cuni = '" + asesoria.TeacherCUNI + "'").FirstOrDefault();
            var PeopleRegBP = _context.Database.SqlQuery<string>("select b.\"Abr\" from " + CustomSchema.Schema + ".\"Civil\" c inner join " + CustomSchema.Schema + ".\"Branches\" b on b.\"Id\" = c.\"BranchesId\" where \"SAPId\" = '" + asesoria.TeacherBP + "'").FirstOrDefault();
            if (string.IsNullOrEmpty(asesoria.Proyecto) || string.IsNullOrEmpty(asesoria.Modulo))
            {
                return BadRequest("No se pueden ingresar datos con valores vacios o iguales a 0.");
            }
            if (asesoria.Ignore == false && (_context.AsesoriaPostgrado.FirstOrDefault(x => x.Modulo == asesoria.Modulo && x.Proyecto == asesoria.Proyecto) != null))
            {
                return BadRequest("La combinación de proyecto y módulo ya existe en la BD");
            }
            if (asesoria.Origen.Equals("DEPEN"))
            {
                if (!Equals(ProyReg, PeopleRegCUNI))
                {
                    return BadRequest("El docente seleccionado no pertenece a la Sede del proyecto. No es posible realizar el registro.");
                }
                if (string.IsNullOrEmpty(asesoria.TeacherCUNI))
                {
                    return BadRequest("No se pueden ingresar datos con valores vacios o iguales a 0.");
                }
            }
            if (asesoria.TipoTareaId == 0 || asesoria.TipoTareaId == null)
            {
                return BadRequest("Debe ingresar un tipo de tarea.");
            }
            if (asesoria.Origen.Equals("INDEP"))
            {
                if (!Equals(ProyReg, PeopleRegBP))
                {
                    return BadRequest("El docente seleccionado no pertenece a la Sede del proyecto. No es posible realizar el registro.");
                }
                if (string.IsNullOrEmpty(asesoria.TeacherBP))
                {
                    return BadRequest("No se pueden ingresar datos con valores vacios o iguales a 0.");
                }
            }
            if (asesoria.TotalBruto <= 0 || asesoria.TotalNeto <= 0)
            {
                return BadRequest("No se pueden ingresar datos con valores negativos o iguales a 0");
            }
            if (asesoria.Origen.Equals("INDEP") && asesoria.Factura == false && (asesoria.IUE == 0 || asesoria.IT == 0))
            {
                return BadRequest("El monto para IUE o IT debe ser mayor a 0.");
            }
            if (asesoria.Origen.Equals("INDEP"))
            {
                if (string.IsNullOrEmpty(asesoria.IUE.ToString()) || string.IsNullOrEmpty(asesoria.IT.ToString()))
                {
                    return BadRequest("El monto para IUE o IT no puede registrarse en blanco.");
                }
            }
            if (asesoria.Origen.Equals("EXT") && asesoria.Factura == false && asesoria.IUEExterior == 0)
            {
                return BadRequest("El monto para IUEExterior debe ser mayor a 0.");
            }
            if (asesoria.Origen.Equals("EXT"))
            {
                if (string.IsNullOrEmpty(asesoria.IUEExterior.ToString()))
                {
                    return BadRequest("El monto para IUEExterior no puede registrarse en blanco.");
                }
            }

            //El branchesId es del último puesto de quién registra
            var userCUNI = user.People.CUNI;
                var regionalId = _context.Database.SqlQuery<int>("select b.\"Id\" " +
                                                                 "from " +
                                                                 "   " +
                                                                 ConfigurationManager.AppSettings["B1CompanyDB"] +
                                                                 ".oprj op " +
                                                                 " inner join  " + CustomSchema.Schema +
                                                                 ".\"Branches\" b  on b.\"Abr\" = op.\"U_Sucursal\"" +
                                                                 "where " +
                                                                 "op.\"PrjCode\"='" + asesoria.Proyecto + "'")
                    .FirstOrDefault();

                asesoria.BranchesId = regionalId;

                //el Id del siguiente registro
                asesoria.Id = AsesoriaPostgrado.GetNextId(_context);
                //asegura que no se junte el nuevo registro con los históricos
                asesoria.Estado = "REGISTRADO";
                asesoria.UserCreate = user.Id;
                //identifica la dependencia del registro en base al nombre de la carrera y la regional
                var dep = _context.Database.SqlQuery<int>("select dep.\"Cod\"" +
                                                          "\r\n    from " +
                                                          ConfigurationManager.AppSettings["B1CompanyDB"] +
                                                          ".oprj op" +
                                                          "\r\n    inner join " + CustomSchema.Schema + ".\"OrganizationalUnit\" ou " +
                                                          "\r\n    on ou.\"Cod\" = op.\"U_UORGANIZA\"" +
                                                          "\r\n    inner join " + CustomSchema.Schema + ".\"Dependency\" dep" +
                                                          "\r\n   on dep.\"OrganizationalUnitId\" = ou.\"Id\"" +
                                                          "\r\n   where op.\"PrjCode\" = '" + asesoria.Proyecto + "'" +
                                                          "\r\n     and dep.\"BranchesId\"= " + asesoria.BranchesId).FirstOrDefault().ToString();
                asesoria.DependencyCod = dep;
                asesoria.StudentFullName = asesoria.StudentFullName.ToUpper();
                if (asesoria.Origen == "INDEP")
                {
                    asesoria.Deduccion = asesoria.IUE + asesoria.IT;
                }
                //agregar el nuevo registro en el contexto
                _context.AsesoriaPostgrado.Add(asesoria);
                _context.SaveChanges();
                return Ok("Información registrada");
        }
        //modificacion de la tutoria
        [HttpPut]
        [Route("api/AsesoriaPostgrado/{id}")]
        public IHttpActionResult Put(int id, [FromBody] AsesoriaPostgrado asesoria)
        {
            var user = auth.getUser(Request);
            var ProyReg = _context.Database.SqlQuery<string>("select \"U_Sucursal\" from " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprj where \"PrjCode\" = '" + asesoria.Proyecto + "'").FirstOrDefault();
            var PeopleRegCUNI = _context.Database.SqlQuery<string>("select \"Branches\" from " + CustomSchema.Schema + ".lastcontracts where cuni = '" + asesoria.TeacherCUNI + "'").FirstOrDefault();
            var PeopleRegBP = _context.Database.SqlQuery<string>("select b.\"Abr\" from " + CustomSchema.Schema + ".\"Civil\" c inner join " + CustomSchema.Schema + ".\"Branches\" b on b.\"Id\" = c.\"BranchesId\" where \"SAPId\" = '" + asesoria.TeacherBP + "'").FirstOrDefault();

            if (!_context.AsesoriaPostgrado.ToList().Any(x => x.Id == id))
            {
                return BadRequest("No existe el registro correspondiente");
            }
            else
            {
                if (string.IsNullOrEmpty(asesoria.TeacherBP))
                {
                    if (asesoria.Ignore == false && (_context.AsesoriaPostgrado.FirstOrDefault(x => x.TeacherCUNI == asesoria.TeacherCUNI && x.Modulo == asesoria.Modulo && x.Proyecto == asesoria.Proyecto && x.Id != id) != null))
                    {
                        return BadRequest("La combinación de docente, proyecto y módulo ya existe en la BD");
                    }
                    if (!Equals(ProyReg, PeopleRegCUNI))
                    {
                        return BadRequest("El docente seleccionado no pertenece a la Sede del proyecto. No es posible realizar el registro.");
                    }
                }
                if (asesoria.TipoTareaId == 0 || asesoria.TipoTareaId == null)
                {
                    return BadRequest("Debe ingresar un tipo de tarea.");
                }
                if (string.IsNullOrEmpty(asesoria.TeacherCUNI))
                {
                    if (asesoria.Ignore == false && (_context.AsesoriaPostgrado.FirstOrDefault(x => x.TeacherBP == asesoria.TeacherBP && x.Modulo == asesoria.Modulo && x.Proyecto == asesoria.Proyecto && x.Id != id) != null))
                    {
                        return BadRequest("La combinación de docente, proyecto y módulo ya existe en la BD");
                    }
                    if (!Equals(ProyReg, PeopleRegBP))
                    {
                        return BadRequest("El docente seleccionado no pertenece a la Sede del proyecto. No es posible realizar el registro.");
                    }
                }
                if (asesoria.TotalBruto <= 0 || asesoria.TotalNeto <= 0)
                {
                    return BadRequest("No se pueden ingresar datos con valores negativos o iguales a 0");
                }
                if (asesoria.Origen.Equals("INDEP") && asesoria.Factura == false && (asesoria.IUE < 0 || asesoria.IT < 0))
                {
                    return BadRequest("El monto para IUE o IT debe ser mayor a 0.");
                }
                if (asesoria.Origen.Equals("INDEP"))
                {
                    if (string.IsNullOrEmpty(asesoria.IUE.ToString()) || string.IsNullOrEmpty(asesoria.IT.ToString()))
                    {
                        return BadRequest("El monto para IUE o IT no puede registrarse en blanco.");
                    }
                }
                if (asesoria.Origen.Equals("EXT") && asesoria.Factura == false && asesoria.IUEExterior < 0)
                {
                    return BadRequest("El monto para IUEExterior no debe ser menor a 0.");
                }
                if (asesoria.Origen.Equals("EXT"))
                {
                    if (string.IsNullOrEmpty(asesoria.IUEExterior.ToString()))
                    {
                        return BadRequest("El monto para IUEExterior no puede registrarse en blanco.");
                    }
                }
                    var thisAsesoria = _context.AsesoriaPostgrado.FirstOrDefault(x => x.Id == id);
                //Temporalidad
                thisAsesoria.Mes = asesoria.Mes;
                thisAsesoria.Gestion = asesoria.Gestion;
                //Carrera y Dep
                thisAsesoria.DependencyCod = asesoria.DependencyCod;
                thisAsesoria.Proyecto = asesoria.Proyecto;
                thisAsesoria.Modulo = asesoria.Modulo;

                //Docente
                thisAsesoria.TeacherCUNI = asesoria.TeacherCUNI;
                thisAsesoria.TeacherBP = asesoria.TeacherBP;
                thisAsesoria.Origen = asesoria.Origen;
                thisAsesoria.NumeroContrato = asesoria.NumeroContrato;
                //Estudiante
                //Sobre la tutoria
                thisAsesoria.TipoTareaId = asesoria.TipoTareaId;
                thisAsesoria.TipoPago = asesoria.TipoPago;
                thisAsesoria.Ignore = asesoria.Ignore;
                //Sobre costos
                thisAsesoria.Horas = asesoria.Horas;
                thisAsesoria.MontoHora = asesoria.MontoHora;
                thisAsesoria.TotalBruto = asesoria.TotalBruto;
                thisAsesoria.TotalNeto = asesoria.TotalNeto;
                thisAsesoria.Deduccion = asesoria.Deduccion;
                thisAsesoria.Observaciones = asesoria.Observaciones;
                thisAsesoria.IUE = asesoria.IUE;
                thisAsesoria.IT = asesoria.IT;
                thisAsesoria.IUEExterior = asesoria.IUEExterior;

                thisAsesoria.BranchesId = asesoria.BranchesId;
                //Modifica su estado
                thisAsesoria.Estado = asesoria.Estado;
                thisAsesoria.Factura = asesoria.Factura;
                if (!string.IsNullOrEmpty(asesoria.StudentFullName))
                {
                    thisAsesoria.StudentFullName = asesoria.StudentFullName.ToUpper();
                }
                thisAsesoria.UpdatedAt = DateTime.Now;
                thisAsesoria.UserUpdate = user.Id;
                _context.SaveChanges();
                return Ok("Se actualizaron los datos correctamente");
            }
        }

        //para la instancia de el modulo de aprobacion Isaac, pasar a pre-aprobacion
        [HttpPut]
        [Route("api/ToPreAprobacionPostgrado")]
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
                    var thisAsesoria = _context.AsesoriaPostgrado.FirstOrDefault(x => x.Id == currentElement);
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
                    return Ok("No se pudieron actualizar los siguientes registros:" + failedUpdates);//aquí meterle el concat por comas
                }
            }
        }

        //para la instancia de el modulo de aprobacion Isaac, pasar a historico
        [HttpPut]
        [Route("api/SendHistoricPostgrado")]
        public IHttpActionResult SendHistoric([FromUri] string myArray, [FromUri] string data)
        {
            string[] info = data.Split(';');
            int segmentoId = Convert.ToInt16(info[0]);
            int mes = Convert.ToInt32((info[1]));
            int gestion = Convert.ToInt32(info[2]);
            var user = auth.getUser(Request);
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
                    var thisAsesoria = _context.AsesoriaPostgrado.FirstOrDefault(x => x.Id == currentElement);
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

        //para la instancia de el modulo de aprobacion Isaac
        [HttpDelete]
        [Route("api/DeleteRecordPostgrado/{id}")]
        public IHttpActionResult DeleteRecord(int id)
        {
            //solo borrarlo en la primera instancia, no se eliminan los aprobados
            var recordForDeletion = _context.AsesoriaPostgrado.FirstOrDefault(x => x.Id == id && x.Estado == "REGISTRADO");
            if (recordForDeletion == null)
            {
                return BadRequest("El registro no existe en BD");
            }
            else
            {
                _context.AsesoriaPostgrado.Remove(recordForDeletion);
                _context.SaveChanges();
                return Ok("Se eliminó el registro exitosamente");
            }
        }

        [HttpPut]
        [Route("api/SendHistoricPostgrado")]
        public IHttpActionResult SendHistoric([FromUri] string myArray)
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

                        thisAsesoria.Estado = "APROBADO";
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

        [HttpPut]
        [Route("api/ToVerificacionPost")]
        public IHttpActionResult ToVerificacionPost([FromUri] string myArray)
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
                    var thisAsesoria = _context.AsesoriaPostgrado.FirstOrDefault(x => x.Id == currentElement);
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

        [HttpGet]
        [Route("api/BusquedaAvanzadaIsaacPost/{Proyecto}/{Modulo}/{Docente}/{Origen}/{tarea}/{mes}/{gestion}/{minB}/{maxB}/{minN}/{maxN}/{tpago}")]
        public IHttpActionResult BusquedaAvanzadaPost(string Proyecto, string Modulo, string Docente,
            string Origen, string Tarea, string Mes, string gestion, int minB, int maxB, int minN, int maxN, string tpago)
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

                var report = new List<AsesoriaPostgradoReportViewModel>();
                string pro = "";
                string mod = "";
                string doc = "";
                string tar = "";
                string est = "";
                string mes0 = "";
                string ges = "";
                string org = "";
                string pag = "";
                var cabecera =
                    "select\r\n1 \"Id\",\r\nad.\"Proyecto\",\r\nad.\"Modulo\",\r\nad.\"Origen\", \r\ntt.\"Tarea\" \"TipoTarea\", \r\ntp.\"Nombre\" \"TipoPago\", \r\nad.\"Observaciones\",\r\nad.\"TotalBruto\", \r\ncase when ad.\"IUE\" is null then 0 else ad.\"IUE\" end as \"IUE\",\r\ncase when ad.\"IT\" is null then 0 else ad.\"IT\" end as \"IT\", \r\ncase when ad.\"IUEExterior\" is null then 0 else ad.\"IUEExterior\" end as \"IUEExterior\",  \r\nad.\"Deduccion\", \r\nad.\"TotalNeto\", \r\ncase when ad.\"Mes\" = 1 then 'ENE'when ad.\"Mes\" = 2 then 'FEB'when ad.\"Mes\" = 3 then 'MAR'when ad.\"Mes\" = 4 then 'ABR'when ad.\"Mes\" = 5 then 'MAY'when ad.\"Mes\" = 6 then 'JUN'when ad.\"Mes\" = 7 then 'JUL'when ad.\"Mes\" = 8 then 'AGO'when ad.\"Mes\" = 9 then 'SEP'when ad.\"Mes\" = 10 then 'OCT'when ad.\"Mes\" = 11 then 'NOV'when ad.\"Mes\" = 12 then 'DIC'else ''end as \"MesLiteral\",\r\nad.\"Mes\", \r\nad.\"Gestion\", \r\nbr.\"Abr\" \"Regional\", \r\nad.\"BranchesId\", \r\ncase when ad.\"Ignore\" = true then 'D' when ad.\"Ignore\" = false then '' end as \"Ignore\",\r\nx.\"TeacherFullName\", \r\nad.\"StudentFullName\"";
                var cabeceraSubTotal =
                    "select ad.\"TotalBruto\",  ad.\"IUE\", ad.\"IT\", ad.\"IUEExterior\",   ad.\"Deduccion\",  ad.\"TotalNeto\",  ad.\"BranchesId\"";
                var subtotal = "select 8 \"Id\", null \"Proyecto\", null \"Modulo\", null \"Origen\",  null \"TipoTarea\", null \"TipoPago\",  null \"Observaciones\", sum(\"TotalBruto\") \"TotalBruto\",  sum(\"IUE\") \"IUE\", sum(\"IT\") \"IT\", sum(\"IUEExterior\") \"IUEExterior\",   sum(\"Deduccion\") \"Deduccion\",  sum(\"TotalNeto\") \"TotalNeto\",  null \"MesLiteral\", null \"Mes\", null \"Gestion\",  null \"Regional\",  max(\"BranchesId\"),  null \"Ignore\", null \"TeacherFullName\", null \"StudentFullName\" from ";
                var queryCuerpo = "from " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" ad" +
                                  "\r\ninner join " + CustomSchema.Schema + ".\"TipoTarea\" tt on tt.\"Id\" = ad.\"TipoTareaId\"" +
                                  "\r\ninner join " + CustomSchema.Schema + ".\"TipoPago\" tp on tp.\"Id\"= ad.\"TipoPago\" " +
                                  "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on br.\"Id\" = ad.\"BranchesId\"" +
                                  "\r\nleft join (select\r\nad.\"TeacherCUNI\", " +
                                  "\r\nad.\"TeacherBP\", " +
                                  "\r\ncase when ad.\"Origen\" = 'INDEP' then ad.\"TeacherBP\" else ad.\"TeacherCUNI\" end as \"Cod\"," +
                                  "\r\ncase when ad.\"Origen\" = 'INDEP' then ocrd.\"CardName\" else fn.\"FullName\" end as \"TeacherFullName\"" +
                                  "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" ad" +
                                  "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on br.\"Id\" = ad.\"BranchesId\"" +
                                  "\r\nleft join " + CustomSchema.Schema + ".\"FullName\" fn on fn.cuni = ad.\"TeacherCUNI\"" +
                                  "\r\nleft join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".ocrd on ocrd.\"CardCode\" = ad.\"TeacherBP\"" +
                                  "\r\nwhere ad.\"Estado\"= 'APROBADO' ) x on x.\"Cod\" = ad.\"TeacherCUNI\" or x.\"Cod\" = ad.\"TeacherBP\"" +
                                  "\r\nwhere ad.\"Estado\"= 'APROBADO' ";

                if (Proyecto != "null")
                {
                    pro = " and ad.\"Proyecto\" ='" + Proyecto + "'";
                }

                if (Modulo != "null")
                {
                    mod = " and ad.\"Modulo\" ='" + Modulo + "'";
                }

                if (Origen == "1")
                {
                    org = " and ad.\"Origen\" ='DEPEN'";
                }
                else
                {
                    if (Origen == "2")
                    {
                        org = " and ad.\"Origen\" ='INDEP'";
                    }
                    else if (Origen == "3")
                    {
                        org = " and ad. \"Origen\" ='OR'";
                    }
                    else if (Origen == "4")
                    {
                        org = " and ad. \"Origen\" ='FAC'";
                    }
                    else if (Origen == "5")
                    {
                        org = " and ad. \"Origen\" ='EXT'";
                    }
                }
                // todo falta el conseguir el nombre del docente para busqueda
                if (Docente != "null")
                {
                    doc = " and \"TeacherFullName\" like '%" + Docente + "%'";
                }

                if (Tarea != "null")
                {
                    tar = " and ad.\"TipoTareaId\" ='" + Tarea + "'";
                }

                if (Mes != "null")
                {
                    mes0 = " and ad.\"Mes\" =" + Mes + "";
                }

                if (gestion != "null")
                {
                    ges = " and ad.\"Gestion\" =" + gestion + "";
                }
                if (tpago != "null")
                {
                    pag = " and ad.\"TipoPago\" = " + tpago + "";
                }

                // Construir las condiciones de rango para TotalBruto y TotalNeto
                var condicionesRangoBruto = (minB >= 0 && maxB >= minB) ? " AND ad.\"TotalBruto\" BETWEEN " + minB + " AND " + maxB : "";
                var condicionesRangoNeto = (minN >= 0 && maxN >= minN) ? " AND ad.\"TotalNeto\" BETWEEN " + minN + " AND " + maxN : "";
                string order = " order by \"Id\",\"Gestion\", \"Mes\", \"TeacherFullName\"";
                string group = " group by ad.\"TeacherCUNI\", \r\nad.\"TeacherBP\", \r\nad.\"DependencyCod\", \r\nad.\"Proyecto\",\r\nad.\"Modulo\",\r\nad.\"Origen\", \r\ntt.\"Tarea\", \r\ntp.\"Nombre\", \r\nad.\"Observaciones\",\r\nad.\"TotalBruto\", \r\nad.\"IUE\",\r\nad.\"IT\", \r\nad.\"IUEExterior\",  \r\nad.\"Deduccion\", \r\nad.\"TotalNeto\",\r\nad.\"Mes\",\r\nad.\"Gestion\", \r\nbr.\"Abr\", \r\nad.\"BranchesId\", \r\nad.\"Ignore\",\r\nx.\"TeacherFullName\", \r\nad.\"StudentFullName\"";
                string query = cabecera + queryCuerpo + regionalesUser + pro + mod + org + doc + tar + est +
                               mes0 + ges + pag + condicionesRangoBruto + condicionesRangoNeto + group;
                string querysubTotal = cabeceraSubTotal + queryCuerpo + regionalesUser + pro + mod + org + doc + tar + est +
                                       mes0 + ges + pag + condicionesRangoBruto + condicionesRangoNeto + group;
                string QueryOriginal = "(" + query + ") UNION (" + subtotal + " (" + querysubTotal + ")) " + order;
                var reportOG = _context.Database.SqlQuery<AsesoriaPostgradoReportViewModel>(query).ToList();
                report = _context.Database.SqlQuery<AsesoriaPostgradoReportViewModel>(QueryOriginal).ToList();
                if (reportOG.Count < 1)
                {
                    return BadRequest("No se hallaron resultados con los parametros de búsqueda.");
                }

                var formattedList = report.ToList()
                    .Select(x => new
                    {
                        x.Origen,
                        x.Proyecto,
                        x.Modulo,
                        Docente = x.TeacherFullName,
                        Alumno = x.StudentFullName,
                        Tarea = x.TipoTarea,
                        Mes = x.MesLiteral,
                        x.Gestion,
                        x.TotalBruto,
                        x.Deduccion,
                        RCIVA = x.IUE,
                        x.IT,
                        x.IUEExterior,
                        x.TotalNeto,
                        x.Observaciones,
                        Dup = x.Ignore,
                        Pago = x.TipoPago
                    });
                return Ok(formattedList);
            }
            catch (Exception exception)
            {
                return BadRequest("Ocurrió un problema. Comuniquese con el administrador. " + exception);
            }
        }


        [HttpGet]
        [Route("api/ProyectosUsed")]
        public IHttpActionResult ProyectosUsed()
        {
            //Solo saca los proyectos que fueron registrados en postgrado
            var activeDocentes = _context.Database.SqlQuery<AuxiliarBranches>("select ap.\"Proyecto\" \"Cod\", oprj.\"PrjName\" \"Name\", br.\"Id\" \"BranchesId\"" +
                                                                              "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" ap" +
                                                                              "\r\ninner join  " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprj on oprj.\"PrjCode\" = ap.\"Proyecto\"" +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on br.\"Abr\" = oprj.\"U_Sucursal\"" +
                                                                              "\r\nwhere ap.\"Estado\"= 'APROBADO' \r\ngroup by ap.\"Proyecto\", oprj.\"PrjName\", br.\"Id\"" +
                                                                              "\r\norder by ap.\"Proyecto\""
            ).ToList();


            var user = auth.getUser(Request);

            var filteredList = auth.filerByRegional(activeDocentes.AsQueryable(), user);

            return Ok(filteredList);
        }
        [HttpGet]
        [Route("api/ModulesUsed")]
        public IHttpActionResult ModulesUsed()
        {
            //Solo saca los proyectos que fueron registrados en postgrado
            var activeDocentes = _context.Database.SqlQuery<AuxiliarBranches>("select ap.\"Proyecto\" \"CodAux\", ap.\"Modulo\" \"Cod\", pm.\"NameModule\" \"Name\", pm.\"BranchesId\", pm.\"Id\"" +
                                                                              "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" ap" +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"ProjectModules\" pm on pm.\"CodModule\" = ap.\"Modulo\"  and pm.\"CodProject\" = ap.\"Proyecto\"" +
                                                                              "\r\nwhere ap.\"Estado\"= 'APROBADO'" +
                                                                              " group by ap.\"Proyecto\", ap.\"Modulo\", pm.\"NameModule\", pm.\"BranchesId\", pm.\"Id\"" +
                                                                              "\r\norder by ap.\"Modulo\", pm.\"NameModule\""
            ).ToList();


            var user = auth.getUser(Request);

            var filteredList = auth.filerByRegional(activeDocentes.AsQueryable(), user);

            return Ok(filteredList);
        }
        [HttpGet]
        [Route("api/ModulesUsedByProject/{id}")]
        public IHttpActionResult ModulesUsedByProject(string id)
        {
            //Solo saca los proyectos que fueron registrados en postgrado
            var activeDocentes = _context.Database.SqlQuery<AuxiliarBranches>("select ap.\"Proyecto\" \"CodAux\", ap.\"Modulo\" \"Cod\", pm.\"NameModule\" \"Name\", pm.\"BranchesId\", pm.\"Id\"" +
                                                                              "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" ap" +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"ProjectModules\" pm on pm.\"CodModule\" = ap.\"Modulo\"  and pm.\"CodProject\" = ap.\"Proyecto\"" +
                                                                              "\r\nwhere ap.\"Estado\"= 'APROBADO' " +
                                                                              "\r\nand ap.\"Proyecto\"= '" + id + "' " +
                                                                              " group by ap.\"Proyecto\", ap.\"Modulo\", pm.\"NameModule\", pm.\"BranchesId\", pm.\"Id\"" +
                                                                              "\r\norder by ap.\"Modulo\", pm.\"NameModule\""
            ).ToList();


            var user = auth.getUser(Request);

            var filteredList = auth.filerByRegional(activeDocentes.AsQueryable(), user);

            return Ok(filteredList);
        }
        [HttpGet]
        [Route("api/DocentesListBusquedaPost")]
        public IHttpActionResult DocentesListBusqueda()
        {
            //Hacer un union con los docentes que no sean indepedientes, es decir que sean de civil nomas, por su jobTitle
            var activeDocentes = _context.Database.SqlQuery<AsesoriaTeachers>("select \"FullName\", \"BranchesId\",\"Regional\" from " +
                                                                              "\r\n((select fn.\"FullName\", lc.\"BranchesId\",b.\"Abr\" \"Regional\"" +
                                                                              "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" ad" +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"FullName\" fn on fn.cuni = ad.\"TeacherCUNI\"" +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".LASTCONTRACTS lc on lc.cuni = ad.\"TeacherCUNI\"" +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" b on b.\"Id\" = lc.\"BranchesId\"" +
                                                                              "\r\nwhere ad.\"Estado\"= 'APROBADO' " +
                                                                              "\r\ngroup by fn.cuni,ad.\"TeacherCUNI\", fn.\"FullName\", lc.\"BranchesId\",b.\"Abr\")" +
                                                                              "\r\nunion all(select ocrd.\"CardName\" \"FullName\", br.\"Id\" as \"BranchesId\",  " +
                                                                              "\r\n br.\"Abr\" \"Regional\"" +
                                                                              "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" cv " +
                                                                              "\r\ninner join   " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".OCRD on cv.\"TeacherBP\" = ocrd.\"CardCode\" " +
                                                                              "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on cv.\"BranchesId\" = br.\"Id\"" +
                                                                              "\r\nwhere cv.\"Estado\"= 'APROBADO' " +
                                                                              "\r\ngroup by cv.\"TeacherBP\",ocrd.\"CardName\", br.\"Id\", br.\"Abr\"))" +
                                                                              "\r\ngroup by \"FullName\", \"BranchesId\",\"Regional\"\r\norder by \"FullName\""
            ).ToList();


            var user = auth.getUser(Request);

            var filteredList = auth.filerByRegional(activeDocentes.AsQueryable(), user);

            return Ok(filteredList);
        }
        // REPORTE POR CARRERA
        // obtener carreras segun su estado en la lista
        [HttpGet]
        [Route("api/AseProyectos")]
        public IHttpActionResult AseProyectos([FromUri] string by)
        {
            string query = "select oprj.\"PrjCode\" \"Cod\", oprj.\"PrjName\" \"Proyecto\", a.\"BranchesId\"" +
                           "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" a " +
                           "\r\ninner join "+CustomSchema.Schema+".\"Branches\" br on a.\"BranchesId\"=br.\"Id\" " +
                           "\r\ninner join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"OPRJ\" oprj on oprj.\"PrjCode\" = a.\"Proyecto\"";
            string orderBy = " group by oprj.\"PrjCode\", oprj.\"PrjName\", a.\"BranchesId\" order by oprj.\"PrjCode\" asc";
            var rawresult = new List<AsesoriaPostgradoViewModel>();
            var user = auth.getUser(Request);

            if (by.Equals("APROBADO"))
            {
                string customQuery = query + "where a.\"Estado\"='APROBADO' " + orderBy;
                //Mes a literal
                rawresult = mesLiteral(customQuery);
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new { x.Cod, x.Proyecto });
                return Ok(filteredList);

            }
            else if (by.Equals("PRE-APROBADO"))
            {
                string customQuery = query + "where a.\"Estado\"='PRE-APROBADO' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Proyecto
                    }); ;
                return Ok(filteredList);

            }
            else if (by.Equals("REGISTRADO-DEPEN"))
            {
                //para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Origen\"='DEPEN' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Proyecto
                    });
                return Ok(filteredList);

            }
            else if (by.Equals("REGISTRADO-INDEP"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Origen\"='INDEP' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Proyecto
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("REGISTRADO-OR"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Origen\"='OR' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Proyecto
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("REGISTRADO-FAC"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Factura\"=true " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Proyecto,
                        x.StudentFullName
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("REGISTRADO-EXT"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='REGISTRADO' " + "and a.\"Origen\"='EXT' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
                var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList()
                    .Select(x => new
                    {
                        x.Cod,
                        x.Proyecto
                    });
                return Ok(filteredList);
            }
            else if (by.Equals("VERIFICADO-DEPEN"))
            {
                // para la pantalla de aprobación nos interesan los registrados nada más
                string customQuery = query + "where a.\"Estado\"='VERIFICADO' " + "and a.\"Origen\"='DEPEN' " + orderBy;
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
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
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
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
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
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
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
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
                rawresult = _context.Database.SqlQuery<AsesoriaPostgradoViewModel>(customQuery).ToList();
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
        [Route("api/PDFReportBodyXProyecto")]
        public IHttpActionResult PDFReportXProyecto([FromUri] string part)
        {
            string query = "";
            var report = new List<AsesoriaPostgradoReportViewModel>();
            string[] data = part.Split(';');
            string section = data[0];
            string state = data[1];
            string origin = data[2];
            string proyecto = data[3];
            string qOrigen = "";
            if (origin == "FAC")
            {
                // qOrigen = " and a.\"Origen\" = 'INDEP' and a.\"Factura\" = true ";
                qOrigen = " and (a.\"Origen\" = 'INDEP' or a.\"Origen\" = 'EXT') and a.\"Factura\" = true ";

            }
            else
            {
                qOrigen = " and a.\"Origen\" like '%" + origin + "%' and a.\"Factura\" = false ";
            }
            // query para generar todos los datos de cada carrera, ordenado por carrera y docente
            switch (section)
            {
                case "Body":
                    // obtiene el cuerpo de la tabla para el PDF
                    // join para el nombre de la carrera
                    query =
                        "select \r\ncase when a.\"Origen\" = 'INDEP' then ocrd.\"CardName\" else fn.\"FullName\" end as \"TeacherFullName\"," +
                        "\r\nt.\"Abr\" as \"TipoTarea\", " +
                        "\r\na.\"Proyecto\" || '-' || op.\"PrjName\" as \"Proyecto\" , " +
                        "\r\na.\"Modulo\" || ' ' || pm.\"NameModule\" as \"Modulo\"," +
                        "\r\na.\"Horas\", a.\"MontoHora\", " +
                        "\r\na.\"TotalBruto\" , " +
                        "\r\na.\"Deduccion\" , " +
                        "\r\na.\"StudentFullName\" , " +
                        "\r\ncase when TRIM(a.\"StudentFullName\") = '' or a.\"StudentFullName\" is null then 'ND' else a.\"StudentFullName\" end as \"StudentFullName\", " +
                        "\r\ncase when a.\"IUE\" is null then 0 else a.\"IUE\" end as \"IUE\", " +
                        "\r\ncase when a.\"IT\" is null then 0 else a.\"IT\" end as \"IT\", " +
                        "\r\ncase when a.\"IUEExterior\" is null then 0 else a.\"IUEExterior\" end as \"IUEExterior\", " +
                        "\r\na.\"TotalNeto\" , " +
                        "\r\na.\"Observaciones\", a.\"BranchesId\", " +
                        "\r\n case when a.\"Ignore\" = true then 'D' when a.\"Ignore\" = false then '' end as \"Ignore\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" a " +
                        "\r\ninner join " + CustomSchema.Schema + ".\"TipoTarea\" t on a.\"TipoTareaId\"=t.\"Id\" " +
                        "\r\ninner join " + CustomSchema.Schema + ".\"ProjectModules\" pm on a.\"Proyecto\"=pm.\"CodProject\"  and a.\"Modulo\"=pm.\"CodModule\" " +
                        "\r\ninner join " + ConfigurationManager.AppSettings["B1CompanyDB"] +
                        ".\"OPRJ\" op on a.\"Proyecto\"= op.\"PrjCode\" " +
                        "\r\nleft join " + CustomSchema.Schema + ".\"FullName\" fn on fn.cuni = a.\"TeacherCUNI\"" +
                        "\r\nleft join " + ConfigurationManager.AppSettings["B1CompanyDB"] +
                        ".ocrd on ocrd.\"CardCode\" = a.\"TeacherBP\"" +
                        "\r\nwhere a.\"Estado\"='" + state + "' " +
                        qOrigen +
                        "and a.\"Proyecto\" like '%" + proyecto + "%'" +
                        "\r\norder by \"Proyecto\",  a.\"Id\", \"TeacherFullName\" asc";
                    report = _context.Database.SqlQuery<AsesoriaPostgradoReportViewModel>(query).ToList();
                    break;

                case "Results":
                    // obtiene los resultados al pie de cada tabla, por carrera
                    query = "select " +
                            "\r\na.\"Proyecto\" || '-' || op.\"PrjName\" as \"Proyecto\" ," +
                            "sum(\"TotalBruto\") as \"TotalBruto\", " +
                            "sum(\"Deduccion\") as \"Deduccion\", " +
                            "case when sum(\"IUE\") is null then 0 else sum(\"IUE\") end as \"IUE\",  " +
                            "case when sum(\"IT\") is null then 0 else sum(\"IT\") end as \"IT\", " +
                            "case when sum(\"IUEExterior\") is null then 0 else sum(\"IUEExterior\") end as \"IUEExterior\", " +
                            "sum(\"TotalNeto\") as \"TotalNeto\", \"BranchesId\" " +
                            "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" a " +
                            "\r\ninner join " + CustomSchema.Schema + ".\"TipoTarea\" t on a.\"TipoTareaId\"=t.\"Id\" " +
                            "\r\ninner join " + ConfigurationManager.AppSettings["B1CompanyDB"] +
                            ".\"OPRJ\" op on a.\"Proyecto\"= op.\"PrjCode\" " +
                            "\r\nleft join " + CustomSchema.Schema + ".\"FullName\" fn on fn.cuni = a.\"TeacherCUNI\"" +
                            "\r\nleft join " + ConfigurationManager.AppSettings["B1CompanyDB"] +
                            ".ocrd on ocrd.\"CardCode\" = a.\"TeacherBP\"" +
                            "\r\nwhere a.\"Estado\"='" + state + "' " +
                            qOrigen +
                            "and a.\"Proyecto\" like '%" + proyecto + "%'" +
                        "group by \"Proyecto\", \"PrjName\", \"BranchesId\" " +
                        "order by \"Proyecto\" ";
                    report = _context.Database.SqlQuery<AsesoriaPostgradoReportViewModel>(query).ToList();
                    break;

                case "FinalResult":
                    // obtiene los resultados al pie de cada tabla, por carrera
                    query = "select " +
                            "sum(\"TotalBruto\") as \"TotalBruto\", " +
                            "sum(\"Deduccion\") as \"Deduccion\", " +
                            "case when sum(\"IUE\") is null then 0 else sum(\"IUE\") end as \"IUE\",  " +
                            "case when sum(\"IT\") is null then 0 else sum(\"IT\") end as \"IT\", " +
                            "case when sum(\"IUEExterior\") is null then 0 else sum(\"IUEExterior\") end as \"IUEExterior\", " +
                            "sum(\"TotalNeto\") as \"TotalNeto\", \"BranchesId\"" +
                        "from " +
                            CustomSchema.Schema + ".\"AsesoriaPostgrado\" a " +

                            "inner join " + CustomSchema.Schema + ".\"Branches\" b " +
                            "on b.\"Id\" = a.\"BranchesId\" " +
                        "where " +
                            "\"Estado\"='" + state + "' " +
                            qOrigen +
                            "and a.\"Proyecto\" like '%" + proyecto + "%'" +
                        "group by \"BranchesId\" ";
                    report = _context.Database.SqlQuery<AsesoriaPostgradoReportViewModel>(query).ToList();
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
                    Proyecto = x.Proyecto,
                    Alumno = x.StudentFullName,
                    Docente = x.TeacherFullName,
                    Tarea = x.TipoTarea,
                    x.Modulo,
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
                    Proyecto = x.Proyecto,
                    Total_Bruto = x.TotalBruto,
                    Alumno = x.StudentFullName,
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
                    Alumno = x.StudentFullName,
                    Deduccion = x.Deduccion,
                    IUE = x.IUE,
                    IT = x.IT,
                    IUEExt = x.IUEExterior,
                    Total_Neto = x.TotalNeto,
                });
                return Ok(filteredListResult);
            }
        }
        [HttpGet]
        [Route("api/BusquedaPagosPost/{Proyecto}/{Modulo}/{Docente}/{Origen}/{tarea}/{mes}/{gestion}/{date1}/{date2}")]
        public IHttpActionResult BusquedaPagosPost(string Proyecto, string Modulo, string Docente,
            string Origen, string Tarea, string Mes, string gestion, DateTime date1, DateTime date2)
        {
            DateTime aux = new DateTime(1969, 12, 31);
            if ((DateTime.Compare(date1, aux) == 0) || (DateTime.Compare(date2, aux) == 0))
            {
                return BadRequest("Debe ingresar fechas obligatoriamente.");
            }
            if (date1 > date2)
            {
                return BadRequest("Debe ingresar fechas validas.");
            }
            try
            {
                //Siguientes lineas para realizar el filtro por regional directamente dentro del query segun las regionales a las que tenga acceso el usuario
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

                var report = new List<AsesoriaPostgradoReportViewModel>();
                string pro = "";
                string mod = "";
                string doc = "";
                string tar = "";
                string est = "";
                string mes0 = "";
                string ges = "";
                string org = "";
                string dates = "";
                var cabecera =
                    "select\r\n1 \"Id\",\r\nad.\"Proyecto\",\r\nad.\"Modulo\",\r\nad.\"Origen\", \r\ntt.\"Tarea\" \"TipoTarea\", \r\nad.\"Observaciones\",\r\nad.\"TotalBruto\", \r\ncase when ad.\"IUE\" is null then 0 else ad.\"IUE\" end as \"IUE\",\r\ncase when ad.\"IT\" is null then 0 else ad.\"IT\" end as \"IT\",  \r\nad.\"Deduccion\", \r\nad.\"TotalNeto\", \r\ncase when ad.\"Mes\" = 1 then 'ENE'when ad.\"Mes\" = 2 then 'FEB'when ad.\"Mes\" = 3 then 'MAR'when ad.\"Mes\" = 4 then 'ABR'when ad.\"Mes\" = 5 then 'MAY'when ad.\"Mes\" = 6 then 'JUN'when ad.\"Mes\" = 7 then 'JUL'when ad.\"Mes\" = 8 then 'AGO'when ad.\"Mes\" = 9 then 'SEP'when ad.\"Mes\" = 10 then 'OCT'when ad.\"Mes\" = 11 then 'NOV'when ad.\"Mes\" = 12 then 'DIC'else ''end as \"MesLiteral\",\r\nad.\"Mes\", \r\nad.\"Gestion\", \r\nbr.\"Abr\" \"Regional\", \r\nad.\"BranchesId\", \r\ncase when ad.\"Ignore\" = true then 'D' when ad.\"Ignore\" = false then '' end as \"Ignore\",\r\ncase when ad.\"Factura\" = true then 'F' when ad.\"Factura\" = false then '' end as \"Factura\",\r\nx.\"TeacherFullName\", ad.\"CreatedAt\", ad.\"ToAuthAt\"";
                var cabeceraSubTotal =
                    "select ad.\"TotalBruto\",  ad.\"IUE\", ad.\"IT\",   ad.\"Deduccion\",  ad.\"TotalNeto\",  ad.\"BranchesId\"";
                var subtotal = "select 8 \"Id\", null \"Proyecto\", null \"Modulo\", null \"Origen\",  null \"TipoTarea\",  null \"Observaciones\", sum(\"TotalBruto\") \"TotalBruto\",  sum(\"IUE\") \"IUE\", sum(\"IT\") \"IT\",   sum(\"Deduccion\") \"Deduccion\",  sum(\"TotalNeto\") \"TotalNeto\",  null \"MesLiteral\", null \"Mes\", null \"Gestion\",  null \"Regional\",  max(\"BranchesId\"),  null \"Ignore\",null \"Factura\", null \"TeacherFullName\", null \"CreatedAt\", null \"ToAuthAt\" from ";
                var queryCuerpo = "from " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" ad" +
                                  "\r\ninner join " + CustomSchema.Schema + ".\"TipoTarea\" tt on tt.\"Id\" = ad.\"TipoTareaId\"" +
                                  "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on br.\"Id\" = ad.\"BranchesId\"" +
                                  "\r\nleft join (select\r\nad.\"TeacherCUNI\", " +
                                  "\r\nad.\"TeacherBP\", " +
                                  "\r\ncase when ad.\"Origen\" = 'INDEP' then ad.\"TeacherBP\" else ad.\"TeacherCUNI\" end as \"Cod\"," +
                                  "\r\ncase when ad.\"Origen\" = 'INDEP' then ocrd.\"CardName\" else fn.\"FullName\" end as \"TeacherFullName\"" +
                                  "\r\nfrom " + CustomSchema.Schema + ".\"AsesoriaPostgrado\" ad" +
                                  "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br on br.\"Id\" = ad.\"BranchesId\"" +
                                  "\r\nleft join " + CustomSchema.Schema + ".\"FullName\" fn on fn.cuni = ad.\"TeacherCUNI\"" +
                                  "\r\nleft join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".ocrd on ocrd.\"CardCode\" = ad.\"TeacherBP\"" +
                                  "\r\nwhere ad.\"Estado\"= 'APROBADO' ) x on x.\"Cod\" = ad.\"TeacherCUNI\" or x.\"Cod\" = ad.\"TeacherBP\"" +
                                  "\r\nwhere ad.\"Estado\"= 'APROBADO' ";

                if (Proyecto != "null")
                {
                    pro = " and ad.\"Proyecto\" ='" + Proyecto + "'";
                }

                if (Modulo != "null")
                {
                    mod = " and ad.\"Modulo\" ='" + Modulo + "'";
                }

                if (Origen == "1")
                {
                    org = " and ad.\"Origen\" ='DEPEN'";
                }
                else
                {
                    if (Origen == "2")
                    {
                        org = " and ad.\"Origen\" ='INDEP'";
                    }
                }
                //todo falta el conseguir el nombre del docente para busqueda
                if (Docente != "null")
                {
                    doc = " and \"TeacherFullName\" like '%" + Docente + "%'";
                }

                if (Tarea != "null")
                {
                    tar = " and ad.\"TipoTareaId\" ='" + Tarea + "'";
                }

                if (Mes != "null")
                {
                    mes0 = " and ad.\"Mes\" =" + Mes + "";
                }

                if (gestion != "null")
                {
                    ges = " and ad.\"Gestion\" =" + gestion + "";
                }

                dates = " and ad.\"ToAuthAt\" between '" + date1.Year + '-' + date1.Month + '-' + date1.Day + " 00:00:00' and '" + date2.Year + '-' + date2.Month + '-' + date2.Day + " 23:59:59'";

                    string order = " order by \"Id\",\"Gestion\", \"Mes\", \"TeacherFullName\"";
                    string group = " group by ad.\"TeacherCUNI\", \r\nad.\"TeacherBP\", \r\nad.\"DependencyCod\", \r\nad.\"Proyecto\",\r\nad.\"Modulo\",\r\nad.\"Origen\", \r\ntt.\"Tarea\", \r\nad.\"Observaciones\",\r\nad.\"TotalBruto\", \r\nad.\"IUE\",\r\nad.\"IT\",  \r\nad.\"Deduccion\", \r\nad.\"TotalNeto\",\r\nad.\"Mes\",\r\nad.\"Gestion\", \r\nbr.\"Abr\", \r\nad.\"BranchesId\", \r\nad.\"Ignore\",\r\nad.\"Factura\",\r\nx.\"TeacherFullName\", ad.\"CreatedAt\", ad.\"ToAuthAt\"";
                string query = cabecera + queryCuerpo + regionalesUser + dates + pro + mod + org + doc + tar + est +
                               mes0 + ges + group;
                string querysubTotal = cabeceraSubTotal + queryCuerpo + regionalesUser + dates + pro + mod + org + doc + tar + est +
                                       mes0 + ges + group;
                string QueryOriginal = "(" + query + ") UNION (" + subtotal + "(" + querysubTotal + "))" + order;
                var reportOG = _context.Database.SqlQuery<AsesoriaPostgradoReportViewModel>(query).ToList();
                report = _context.Database.SqlQuery<AsesoriaPostgradoReportViewModel>(QueryOriginal).ToList();
                if (reportOG.Count < 1)
                {
                    return BadRequest("No se hallaron resultados con los parametros de búsqueda.");
                }

                var formattedList = report.ToList()
                    .Select(x => new
                    {
                        x.Origen,
                        x.Proyecto,
                        x.Modulo,
                        Docente = x.TeacherFullName,
                        Tarea = x.TipoTarea,
                        Mes = x.MesLiteral,
                        x.Gestion,
                        x.TotalBruto,
                        x.Deduccion,
                        x.IUE,
                        x.IT,
                        x.TotalNeto,
                        x.Observaciones,
                        Dup = x.Ignore,
                        Fac = x.Factura,
                        fecha_creacion = x.CreatedAt,
                        fecha_auth = x.ToAuthAt
                    });
                return Ok(formattedList);
            }
            catch (Exception exception)
            {
                return BadRequest("Ocurrió un problema. Comuniquese con el administrador. " + exception);
            }
        }

    }
}