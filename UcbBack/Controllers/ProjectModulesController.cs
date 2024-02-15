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
using System.Data.Entity.Infrastructure;
using UcbBack.Logic.ExcelFiles;
using UcbBack.Models.Dist;
using System.Diagnostics;
using UcbBack.Logic.ExcelFiles.Serv;
using System.Globalization;
using System.Web.WebPages;

namespace UcbBack.Controllers
{
    public class ProjectModulesController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidateAuth auth;
        private B1Connection B1;

        public ProjectModulesController()
        {
            _context = new ApplicationDbContext();
            B1 = B1Connection.Instance();
            auth = new ValidateAuth();
        }
        //[HttpGet]
        //[Route("api/ProjectModules/")]
        //public IHttpActionResult Get()
        //{
        //    //datos para la tabla histórica
        //    string query = "select pm.\"NameModule\" \"PrjAbr\", pj.* , b.\"Abr\"" +
        //                   "\r\nfrom " + CustomSchema.Schema + ".\"ProjectModules\" pj" +
        //                   "\r\ninner join  " + CustomSchema.Schema + ".\"Branches\" b on b.\"Id\" = pj.\"BranchesId\"" +
        //                   "\r\nleft join " + CustomSchema.Schema + ".\"ProjectModules\" pm on pm.\"CodProject\" = pj.\"CodProject\"" +
        //                   "\r\nwhere pm.\"CodModule\" = '0'" +
        //                   "\r\norder by pj.\"CodProject\", pj.\"CodModule\"";

        //    var rawResult = _context.Database.SqlQuery<ProjectModulesViewModel>(query).Select(x => new
        //    {
        //        x.Id,
        //        x.BranchesId,
        //        x.CodModule,
        //        x.CodProject,
        //        x.PrjAbr,
        //        x.NameModule,
        //        x.TeacherFullName,
        //        x.TeacherCI,
        //        x.Horas,
        //        x.MontoHora,
        //        x.FechaInicio,
        //        x.FechaFin,
        //        x.Observaciones

        //    }).AsQueryable();

        //    var user = auth.getUser(Request);

        //    var result = auth.filerByRegional(rawResult, user).ToList().Select(x => new
        //    {
        //        x.Id,
        //        Cod_Proyecto = x.CodProject,
        //        Nombre_Proyecto = x.PrjAbr,
        //        Cod_Modulo = x.CodModule,
        //        Nombre_Modulo = x.NameModule,
        //        Docente = x.TeacherFullName,
        //        x.Horas,
        //        x.MontoHora,
        //        Fecha_Inicio = x.FechaInicio != null ? x.FechaInicio.ToString("dd-MM-yyyy") : null,
        //        Fecha_Fin = x.FechaFin != null ? x.FechaFin.ToString("dd-MM-yyyy") : null,
        //        x.Observaciones
        //    }).ToList();

        //    return Ok(result);
        //}
        // Corregir con Rodrigo Tejada y analizar el query. De momento funciona bien, pero se debe optimizar y evitar duplicados, REVISAR SAP, error de unidad organizacional!!!.
        [HttpGet]
        [Route("api/ProjectModules/")]
        public IHttpActionResult Get()
        {

            string query = "SELECT DISTINCT pm.\"NameModule\" \"PrjAbr\", pj.*, b.\"Abr\", oprj.\"U_UORGANIZA\" " +
                "\r\nFROM " + CustomSchema.Schema + ".\"ProjectModules\" pj" +
                "\r\nINNER JOIN  " + CustomSchema.Schema + ".\"Branches\" b ON b.\"Id\" = pj.\"BranchesId\"" +
                "\r\nLEFT JOIN " + CustomSchema.Schema + ".\"ProjectModules\" pm ON pm.\"CodProject\" = pj.\"CodProject\"" +
                "\r\ninner join UCATOLICA .\"OPRJ\" on oprj.\"PrjCode\" = pm.\"CodProject\" "+
                "\r\nWHERE pm.\"CodModule\" = '0'" +
                "\r\nORDER BY pj.\"CodProject\", pj.\"CodModule\"";


            var rawResult = _context.Database.SqlQuery<ProjectModulesViewModel>(query).Select(x => new
            {
                x.Id,
                x.BranchesId,
                x.CodModule,
                x.CodProject,
                x.PrjAbr,
                x.NameModule,
                x.TeacherFullName,
                x.TeacherCI,
                x.Horas,
                x.MontoHora,
                x.FechaInicio,
                x.FechaFin,
                x.Observaciones,
                x.U_UORGANIZA

            }).AsQueryable();

            var user = auth.getUser(Request);

            var result = auth.filerByRegional(rawResult, user).ToList().Select(x => new
            {
                x.Id,
                Cod_Proyecto = x.CodProject,
                Nombre_Proyecto = x.PrjAbr,
                Cod_Modulo = x.CodModule,
                Nombre_Modulo = x.NameModule,
                Docente = x.TeacherFullName,
                x.Horas,
                x.MontoHora,
                Fecha_Inicio = x.FechaInicio != null ? x.FechaInicio.ToString("dd-MM-yyyy") : null,
                Fecha_Fin = x.FechaFin != null ? x.FechaFin.ToString("dd-MM-yyyy") : null,
                x.Observaciones,
                x.U_UORGANIZA
            }).ToList();

            return Ok(result);
        }

        [HttpGet]
        [Route("api/GetUnitName/{cod}")]
        public IHttpActionResult GetUnitName(string cod)
        {
            try
            {
                string query = @"
            SELECT DISTINCT ou.""Name""
            FROM ""ADMNALRRHH"".""OrganizationalUnit"" ou
            JOIN ""ADMNALRRHH"".""Dependency"" d ON ou.""Id"" = d.""OrganizationalUnitId""
            JOIN ""ADMNALRRHH"".""AsesoriaPostgrado"" ap ON d.""Cod"" = ap.""DependencyCod""
            WHERE ap.""Proyecto"" = '" + cod + "'";

                var result = _context.Database.SqlQuery<string>(query).FirstOrDefault();
                return Ok(result);
            }
            catch (Exception exception)
            {
                return BadRequest("Ocurrió un problema. Comuníquese con el administrador. " + exception);
            }
        }

        [HttpGet]
        [Route("api/GetUniteCode/{cod}")]
        public IHttpActionResult GetUniteCode(string cod)
        {
            try
            {
                string query = @"
            SELECT DISTINCT ou.""Cod""
            FROM ""ADMNALRRHH"".""OrganizationalUnit"" ou
            JOIN ""ADMNALRRHH"".""Dependency"" d ON ou.""Id"" = d.""OrganizationalUnitId""
            JOIN ""ADMNALRRHH"".""AsesoriaPostgrado"" ap ON d.""Cod"" = ap.""DependencyCod""
            WHERE ap.""Proyecto"" = '" + cod + "'";

                var result = _context.Database.SqlQuery<string>(query).FirstOrDefault();
                return Ok(result);
            }
            catch (Exception exception)
            {
                return BadRequest("Ocurrió un problema. Comuníquese con el administrador. " + exception);
            }
        }
        //registro por Id
        [HttpGet]
        [Route("api/ProjectModules/{id}")]
        public IHttpActionResult IndividualRecordProjectModules(int id)
        {
            //datos para la tabla histórica
            var uniqueRecord = _context.ProjectModuleses.FirstOrDefault(x => x.Id == id);
            if (uniqueRecord == null)
            {
                return BadRequest("Ese registro no existe");
            }
            else
            {
                return Ok(uniqueRecord);
            }
        }
      //registro de la tutoria
        [HttpPost]
        [Route("api/ProjectModules")]
        public IHttpActionResult Post([FromBody] ProjectModules module)
        {
            var ProyReg = _context.Database.SqlQuery<string>("select \"U_Sucursal\" from " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprj where \"PrjCode\" = '" + module.CodProject + "'").FirstOrDefault(); ;
            var PeopleRegCUNI = _context.Database.SqlQuery<string>("select \"Branches\" from " + CustomSchema.Schema + ".lastcontracts where cuni = '" + module.TeacherCI + "'").FirstOrDefault(); ;
            var PeopleRegBP = _context.Database.SqlQuery<string>("select b.\"Abr\" from " + CustomSchema.Schema + ".\"Civil\" c inner join " + CustomSchema.Schema + ".\"Branches\" b on b.\"Id\" = c.\"BranchesId\" where \"SAPId\" = '" + module.SocioNegocio + "'").FirstOrDefault(); ;
            if (string.IsNullOrEmpty(module.NameModule))
            {
                return BadRequest("El Nombre del módulo no puede ser vacio.");
            }

            if (string.IsNullOrEmpty(module.CodModule))
            {
                return BadRequest("El Código del módulo no puede ser vacio.");
            }
            if (module.CodModule.Length > 7)
            {
                return BadRequest("El Código del módulo no puede tener más de 7 carácteres.");
            }
            if (!string.IsNullOrEmpty(module.SocioNegocio))
            {
                if (!Equals(ProyReg, PeopleRegBP))
                {
                    return BadRequest("El docente seleccionado no pertenece a la Sede del proyecto. No es posible realizar el registro.");
                }

            }
            if (!string.IsNullOrEmpty(module.TeacherCI))
            {
                if (!Equals(ProyReg, PeopleRegCUNI))
                {
                    return BadRequest("El docente seleccionado no pertenece a la Sede del proyecto. No es posible realizar el registro.");
                }
            }
            if (module.CodModule != "0")
            {
                if ((_context.ProjectModuleses.FirstOrDefault(x =>
                    x.CodProject == module.CodProject && x.CodModule == "0")) == null)
                {
                    return BadRequest("Este proyecto necesita estar habilitado para su registro. Por favor, a continuación ingrese el nombre del proyecto abreviado.");
                }
                if (module.CodProject == "")
                {
                    return BadRequest("Debe seleccionar un código de proyecto valido.");
                }
                if (module.Horas == 0 || module.MontoHora == 0)
                {
                    return BadRequest("Los campos Cantidad de Horas y Costo por Hora deben ser mayores a 0.");
                }
            }
            if ((_context.ProjectModuleses.FirstOrDefault(x => x.CodProject == module.CodProject && x.CodModule.ToUpper() == module.CodModule.ToUpper()) != null))
            {
                var uniqueRecord = _context.ProjectModuleses.FirstOrDefault(x => x.CodProject == module.CodProject && x.CodModule.ToUpper() == module.CodModule.ToUpper());
                string answer = "Código proyecto: " + uniqueRecord.CodProject + "\r\n" +
                                "Código Módulo: " + uniqueRecord.CodModule + "\r\n" +
                                "Nombre Módulo: " + uniqueRecord.NameModule + "\r\n" +
                                "Profesor: " + uniqueRecord.TeacherFullName + "\r\n" +
                                "Horas: " + uniqueRecord.Horas + "\r\n" +
                                "Monto Hora: " + uniqueRecord.MontoHora + "\r\n" +
                                "Fecha inicio: " + uniqueRecord.FechaInicio + "\r\n" +
                                "Fecha fin: " + uniqueRecord.FechaFin;
                return BadRequest("Este módulo ya se encuentra registrado con este proyecto.\r\n" + answer);
            }
            else
            {
                var regionalId = _context.Database.SqlQuery<int>("select b.\"Id\" " +
                                                                 "from " +
                                                                 "   " +
                                                                 ConfigurationManager.AppSettings["B1CompanyDB"] +
                                                                 ".oprj op " +
                                                                 " inner join  " + CustomSchema.Schema +
                                                                 ".\"Branches\" b  on b.\"Abr\" = op.\"U_Sucursal\"" +
                                                                 "where " +
                                                                 "op.\"PrjCode\"='" + module.CodProject + "'")
                    .FirstOrDefault();

                module.BranchesId = regionalId;
                //el Id del siguiente registro
                module.Id = ProjectModules.GetNextId(_context);
                //identifica la dependencia del registro en base al nombre de la carrera y la regional

                module.TeacherFullName = module.TeacherFullName.ToUpper();
                module.NameModule = module.NameModule.ToUpper();
                module.CodModule = module.CodModule.ToUpper();
                //agregar el nuevo registro en el contexto
                _context.ProjectModuleses.Add(module);
                _context.SaveChanges();
                return Ok("Información registrada");
            }
        }
        //modificacion de la tutoria
        [HttpPut]
        [Route("api/ProjectModule/{id}/{flag}")]
        public IHttpActionResult Put(int id, [FromBody] ProjectModules prj, int flag = 0)
        {
            //Validacion lista #2
            //todo hacer la validacion de docente en caso de que quiera cambiar si es que tiene registros del modulo con el docente en asesoria postgrado
            AsesoriaPostgrado thisValidEntriesDep = null;
            ProjectModules info = _context.ProjectModuleses.FirstOrDefault(x => x.Id == id);
            var ProyReg = _context.Database.SqlQuery<string>("select \"U_Sucursal\" from " +
                                                          ConfigurationManager.AppSettings["B1CompanyDB"] +
                                                          ".oprj where oprj.\"PrjCode\" = '" +
                                                          prj.CodProject + "'").FirstOrDefault();
            var PeopleRegCUNI = _context.Database.SqlQuery<string>("select \"Branches\" from " + CustomSchema.Schema + ".lastcontracts where cuni = '" + prj.TeacherCI + "'").FirstOrDefault();
            var PeopleRegBP = _context.Database.SqlQuery<string>("select b.\"Abr\" from " + CustomSchema.Schema + ".\"Civil\" c inner join " + CustomSchema.Schema + ".\"Branches\" b on b.\"Id\"= c.\"BranchesId\" where \"SAPId\" = '" + prj.SocioNegocio + "'").FirstOrDefault();
            //VALIDACION ENTRE REGIONAL PROYECTO Y REGIONAL PERSONA
            if (!string.IsNullOrEmpty(prj.SocioNegocio))
            {
                if (!Equals(ProyReg, PeopleRegBP))
                {
                    return BadRequest("El docente seleccionado no pertenece a la Sede del proyecto. No es posible realizar el registro.");
                }
            }
            if (!string.IsNullOrEmpty(prj.TeacherCI))
            {
                if (!Equals(ProyReg, PeopleRegCUNI))
                {
                    return BadRequest("El docente seleccionado no pertenece a la Sede del proyecto. No es posible realizar el registro.");
                }
            }
            //VALIDACION SI TIENE REGISTROS EN ISAAC CON EL MODULO
            if (flag == 0 && info.CodModule != "0")
            {
                if (info.SocioNegocio != null || info.SocioNegocio != "")
                {
                    thisValidEntriesDep = _context.AsesoriaPostgrado.FirstOrDefault(x =>
                        x.Proyecto == prj.CodProject && x.Modulo == prj.CodModule);
                    if (thisValidEntriesDep != null)
                    {
                        return BadRequest("Ya se encuentran registros en ISAAC Postgrado con este módulo. ¿Quiere continuar con la modificación?");
                    }
                }
                if (info.TeacherCI != null || info.TeacherCI != "")
                {
                    thisValidEntriesDep = _context.AsesoriaPostgrado.FirstOrDefault(x =>
                        x.Proyecto == prj.CodProject && x.Modulo == prj.CodModule);
                    if (thisValidEntriesDep != null)
                    {
                        return BadRequest("Ya se encuentran registros en ISAAC Postgrado con este módulo. ¿Quiere continuar con la modificación?");
                    }

                }
            }
            if (!_context.ProjectModuleses.ToList().Any(x => x.Id == id))
            {
                return BadRequest("No existe el registro correspondiente");
            }
            else
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest("Datos inválidos para el registro");
                }
                if (_context.ProjectModuleses.FirstOrDefault(x => x.Id == id).CodModule == "0" && prj.NameModule.Length > 46)
                {
                    return BadRequest("El nombre del módulo no debe sobrepasar los 45 caracteres.");
                }
                else
                {
                    var thisProjectModule = _context.ProjectModuleses.FirstOrDefault(x => x.Id == id);
                    thisProjectModule.CodProject = prj.CodProject;
                    thisProjectModule.CodModule = prj.CodModule.ToUpper();
                    thisProjectModule.NameModule = prj.NameModule.ToUpper();
                    thisProjectModule.SocioNegocio = prj.SocioNegocio;
                    thisProjectModule.TeacherCI = prj.TeacherCI;
                    thisProjectModule.TeacherFullName = prj.TeacherFullName.ToUpper();
                    thisProjectModule.Horas = prj.Horas;
                    thisProjectModule.MontoHora = prj.MontoHora;
                    thisProjectModule.Observaciones = prj.Observaciones;
                    thisProjectModule.FechaInicio = prj.FechaInicio;
                    thisProjectModule.FechaFin = prj.FechaFin;

                    var regionalId = _context.Database.SqlQuery<int>("select b.\"Id\" " +
                                                                     "from " +
                                                                     "   " +
                                                                     ConfigurationManager.AppSettings["B1CompanyDB"] +
                                                                     ".oprj op " +
                                                                     " inner join  " + CustomSchema.Schema +
                                                                     ".\"Branches\" b  on b.\"Abr\" = op.\"U_Sucursal\"" +
                                                                     "where " +
                                                                     "op.\"PrjCode\"='" + prj.CodProject + "'")
                        .FirstOrDefault();

                    thisProjectModule.BranchesId = regionalId;

                    thisProjectModule.BranchesId = prj.BranchesId;
                    thisProjectModule.UpdatedAt = DateTime.Now;
                    _context.SaveChanges();
                    return Ok("Se actualizaron los datos correctamente");
                }
            }
        }
        //para la instancia de el modulo de aprobacion Isaac
        [HttpDelete]
        [Route("api/DeleteModule/{id}")]
        public IHttpActionResult DeleteModule(int id)
        {
            //solo borrarlo en la primera instancia, no se eliminan los aprobados
            var recordForDeletion = _context.ProjectModuleses.FirstOrDefault(x => x.Id == id);
            
            if (recordForDeletion == null)
            {
                return BadRequest("El registro no existe en BD");
            }
            if (recordForDeletion.CodModule == "0")
            {
                return BadRequest("No es posible eliminar módulos con código 0.");
            }
            if (_context.AsesoriaPostgrado.FirstOrDefault(x => x.Proyecto == recordForDeletion.CodProject && x.Modulo == recordForDeletion.CodModule) != null)
            {
                return BadRequest("No es posible eliminar, existen registros Postgrado con el módulo.");
            }
            else
            {
                _context.ProjectModuleses.Remove(recordForDeletion);
                _context.SaveChanges();
                return Ok("Se eliminó el registro exitosamente");
            }
        }
        [HttpGet]
        [Route("api/GetModule/{project}")]
        public IHttpActionResult GetModule(string project)
        {
            //datos para la tabla histórica
            string query = "select pj.*" +
                           "\r\nfrom " + CustomSchema.Schema + ".\"ProjectModules\" pj" +
                           "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" b" +
                           "\r\non b.\"Id\" = pj.\"BranchesId\"" +
                           "\r\nwhere pj.\"CodProject\" = '" + project + "'" +
                           "\r\norder by pj.\"CodProject\", pj.\"CodModule\"";

            var rawResult = _context.Database.SqlQuery<ProjectModules>(query).Select(x => new
            {
                x.Id,
                x.BranchesId,
                x.CodModule,
                x.CodProject,
                x.NameModule,
                x.TeacherFullName,
                x.TeacherCI,
                x.Horas,
                x.MontoHora,
                x.FechaInicio,
                x.FechaFin,
                x.Observaciones,
                x.SocioNegocio

            }).AsQueryable();

            var user = auth.getUser(Request);

            var result = auth.filerByRegional(rawResult, user).ToList().Select(x => new
            {
                x.Id,
                x.CodModule,
                x.CodProject,
                x.NameModule,
                x.TeacherFullName,
                x.TeacherCI,
                x.Horas,
                x.MontoHora,
                x.FechaInicio,
                x.FechaFin,
                x.Observaciones,
                x.SocioNegocio
            }).ToList();

            return Ok(result);
        }
        [HttpGet]
        [Route("api/GetModuleInfoBruto/{Id}/{proy}")]
        public IHttpActionResult GetModuleInfoBruto(string id, string proy)
        {


            //datos para la tabla histórica
            string query = "select pj.*" +
                           "\r\nfrom " + CustomSchema.Schema + ".\"ProjectModules\" pj" +
                           "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" b" +
                           "\r\non b.\"Id\" = pj.\"BranchesId\"" +
                           "\r\nwhere pj.\"CodModule\" = '" + id + "'" +
                           "\r\nand pj.\"CodProject\" = '" + proy + "'" +
                           "\r\norder by pj.\"CodProject\", pj.\"CodModule\"";

            var rawResult = _context.Database.SqlQuery<ProjectModules>(query).Select(x => new
            {
                x.Id,
                x.BranchesId,
                x.CodModule,
                x.CodProject,
                x.NameModule,
                x.TeacherFullName,
                x.TeacherCI,
                x.Horas,
                x.MontoHora,
                x.FechaInicio,
                x.FechaFin,
                x.Observaciones,
                x.SocioNegocio

            }).AsQueryable();

            var user = auth.getUser(Request);

            var result = auth.filerByRegional(rawResult, user).ToList().Select(x => new
            {
                x.Id,
                x.BranchesId,
                x.CodModule,
                x.CodProject,
                x.NameModule,
                x.TeacherFullName,
                x.TeacherCI,
                x.Horas,
                x.MontoHora,
                FechaInicio = x.FechaInicio != null ? x.FechaInicio.ToString("dd-MM-yyyy") : null,
                FechaFin = x.FechaFin != null ? x.FechaFin.ToString("dd-MM-yyyy") : null,
                x.Observaciones,
                x.SocioNegocio
            }).ToList();

            return Ok(result);
        }
        [HttpGet]
        [Route("api/GetModuleInfo/{Id}/{proy}")]
        public IHttpActionResult GetModuleInfo(string id, string proy)
        {
            
            var uniqueRecord = _context.ProjectModuleses.FirstOrDefault(x => x.CodModule == id && x.CodProject == proy);
            if (uniqueRecord == null)
            {
                return BadRequest("Ese registro no existe");
            }
            else
            {
                return Ok(uniqueRecord);
            }
             
            /*
            //datos para la tabla histórica
            string query = "select pj.*" +
                           "\r\nfrom " + CustomSchema.Schema + ".\"ProjectModules\" pj" +
                           "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" b" +
                           "\r\non b.\"Id\" = pj.\"BranchesId\"" +
                           "\r\nwhere pj.\"CodModule\" = '" + id + "'" +
                           "\r\nand pj.\"CodProject\" = '" + proy+ "'" +
                           "\r\norder by pj.\"CodProject\", pj.\"CodModule\"";

            var rawResult = _context.Database.SqlQuery<ProjectModules>(query).Select(x => new
            {
                x.Id,
                x.BranchesId,
                x.CodModule,
                x.CodProject,
                x.NameModule,
                x.TeacherFullName,
                x.TeacherCI,
                x.Horas,
                x.MontoHora,
                x.FechaInicio,
                x.FechaFin,
                x.Observaciones,
                x.SocioNegocio

            }).AsQueryable();

            var user = auth.getUser(Request);

            var result = auth.filerByRegional(rawResult, user).ToList().Select(x => new
            {
                x.Id,
                x.BranchesId,
                x.CodModule,
                x.CodProject,
                x.NameModule,
                x.TeacherFullName,
                x.TeacherCI,
                x.Horas,
                x.MontoHora,
                x.FechaInicio,
                x.FechaFin,
                x.Observaciones,
                x.SocioNegocio
            }).ToList();

            return Ok(result);*/
        }
        [HttpGet]
        [Route("api/GetProjectUsed/")]
        public IHttpActionResult GetProyectosUsados()
        {
            //datos para la tabla histórica
            string query = "select o.\"PrjCode\", o.\"PrjName\", \"BranchesId\"" +
                           "\r\nfrom "+ CustomSchema.Schema +".\"ProjectModules\" pm" +
                           "\r\ninner join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprj o on o.\"PrjCode\" = pm.\"CodProject\"" +
                           "\r\ngroup by o.\"PrjCode\",o.\"PrjName\", \"BranchesId\"" +
                           "\r\norder by o.\"PrjCode\"";

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
        [HttpGet]
        [Route("api/GetProjects/")]
        public IHttpActionResult GetProyectos()
        {
            //datos para la tabla histórica
            string query = "select \"PrjCode\", \"PrjName\", b.\"Id\" \"BranchesId\"" +
                           "\r\nfrom " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprj o" +
                           "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" b on b.\"Abr\" = o.\"U_Sucursal\"" +
                           "\r\nwhere \"Active\" = 'Y'" +
                           "\r\norder by \"PrjCode\"";

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
        [HttpGet]
        [Route("api/GetProjectInfo/{Cod}")]
        public IHttpActionResult GetProyectosInfo(string Cod)
        {
            //datos para la tabla histórica
            string query = "select  \"PrjCode\", \"PrjName\", b.\"Id\" \"BranchesId\", \"ValidTo\", \"ValidFrom\", \"U_PEI_PO\", \"U_UORGANIZA\", " +
                           "case" +
                           "\r\nwhen \"U_Tipo\" = 'E' then 'EDUCACION CONTINUA'" +
                           "\r\nwhen \"U_Tipo\" = 'F' then 'FORMACION CONTINUA'" +
                           "\r\nwhen \"U_Tipo\" = 'I' then 'INFRAESTRUCTURA'" +
                           "\r\nwhen \"U_Tipo\" = 'P' then 'POSTGRADO'" +
                           "\r\nwhen \"U_Tipo\" = 'S' then 'SERVICIOS'" +
                           "\r\nwhen \"U_Tipo\" = 'V' then 'INVESTIGACION'" +
                           "\r\nelse \"U_Tipo\"" +
                           "\r\nend as \"U_Tipo\"" +
                           "\r\nfrom " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprj o" +
                           "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" b on b.\"Abr\" = o.\"U_Sucursal\"" +
                           "\r\nwhere \"PrjCode\" = '" + Cod + "'" +
                           "\r\norder by \"PrjCode\"";

            var rawResult = _context.Database.SqlQuery<OPRJ>(query).Select(x => new
            {
                x.PrjCode,
                x.PrjName,
                x.BranchesId,
                x.U_PEI_PO,
                x.ValidFrom,
                x.ValidTo,
                x.U_Tipo,
                x.U_UOrganiza

            }).AsQueryable();

            var user = auth.getUser(Request);

            var result = auth.filerByRegional(rawResult, user).ToList().Select(x => new
            {
                x.PrjCode,
                x.PrjName,
                x.U_PEI_PO,
                ValidFrom = x.ValidFrom.ToString("dd/MM/yyyy"),
                ValidTo = x.ValidTo.ToString("dd/MM/yyyy"),
                x.U_Tipo,
                x.U_UOrganiza
            }).ToList();

            return Ok(result);
        }
    }
}