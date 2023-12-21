using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.Design.Serialization;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.UI.WebControls;
using Newtonsoft.Json.Linq;
using UcbBack.Logic;
using UcbBack.Models;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using DocumentFormat.OpenXml.Office2013.Drawing.Chart;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Ajax.Utilities;
using Newtonsoft.Json;
using UcbBack.Logic.B1;
using UcbBack.Logic.Mail;
using UcbBack.Models.Auth;
using UcbBack.Models.Not_Mapped;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using UcbBack.Models.Not_Mapped.ViewMoldes;
namespace UcbBack.Controllers
{
    public class BusquedaGrupalController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidatePerson validator;
        private ValidateAuth auth;
        private ADClass activeDirectory;
        public class MyObject
        {
            public string name { get; set; }
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public List<MyObject> children { get; set; }
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? Size { get; set; }
        }
        public BusquedaGrupalController()
        {
            _context = new ApplicationDbContext();
            validator = new ValidatePerson(_context);
            auth = new ValidateAuth();
            activeDirectory = new ADClass();
        }
        //Funcion donde adquirimos la tabla de busqueda grupal
        public IHttpActionResult Get()
        {
            string query = "select p.\"Id\",p.CUNI,p.\"Document\",lc.\"FullName\",lc.\"Positions\",lc.\"Dependency\"," +
                           " case when (lc.\"Active\"=false and lc.\"EndDate\"<current_date) " +
                           "then 'Inactivo' else 'Activo' end as \"Status\", lc.\"BranchesId\"" +
                           ",lc.\"Branches\" " +
                           "from " + CustomSchema.Schema + ".\"People\" p " +
                           "inner join " + CustomSchema.Schema + ".\"LASTCONTRACTS_PRIORITY\" lc " +
                           "on p.cuni = lc.cuni " +
                           " order by \"FullName\";";

            var rawResult = _context.Database.SqlQuery<ContractDetailViewModel>(query).Select(x => new
            {
                x.Id,
                x.CUNI,
                x.Document,
                x.FullName,
                x.Dependency,
                x.DependencyCod,
                x.Branches,
                x.Positions,
                x.Dedication,
                x.Linkage,
                x.Status,
                x.BranchesId
            }).AsQueryable();

            var user = auth.getUser(Request);

            var result = auth.filerByRegional(rawResult, user).ToList().Select(x => new
            {
                x.Id,
                x.CUNI,
                x.Document,
                x.FullName,
                x.Positions,
                x.Dependency,
                x.Branches,
                Estado = x.Status
            }).ToList();

            return Ok(result);
        }
        //Funcion de regreso de person data por contractid
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/BusquedaGrupal/PersonData/{id}/{by}")]
        public IHttpActionResult PersonData(int id, string by)
        {
            var query = "";
            if (by == "ContractId")
            {
                query = "select \r\npe.\"Id\",\r\npe.\"CUNI\"," +
                        "\r\npe.\"Document\" as \"Documento\"," +
                        "\r\ncase when pe.\"TypeDocument\" = 'CI' then 'CARNET DE IDENTIDAD'" +
                        "\r\nwhen pe.\"TypeDocument\" = 'CE' then 'CARNET EXTRANJERO'" +
                        "\r\nwhen pe.\"TypeDocument\" = 'PAS' then 'PASAPORTE'" +
                        " end as \"TipoDocumento\"," +
                        "\r\npe.\"Ext\",\r\npe.\"Names\" as \"Nombres\"," +
                        "\r\npe.\"FirstSurName\" as \"PrimerApellido\"," +
                        "\r\npe.\"SecondSurName\" as \"SegundoApellido\"," +
                        "\r\npe.\"MariedSurName\" as \"ApellidoCasada\"," +
                        "\r\npe.\"BirthDate\" as \"FechaNacimiento\"," +
                        "\r\n case when pe.\"Gender\" = 'F' then 'FEMENINO' " +
                        "\r\nwhen pe.\"Gender\" = 'M' then 'MASCULINO' " +
                        " end as \"Genero\"," +
                        "\r\nYEARS_BETWEEN(TO_DATE(pe.\"BirthDate\"),current_date) as \"Edad\"," +
                        "\r\npe.\"Nationality\" as \"Nacionalidad\"," +
                        "\r\npe.\"PersonalEmail\" as \"EmailPersonal\"," +
                        "\r\npe.\"UcbEmail\" as \"EmailUCB\",\r\npe.\"AFP\", pe.\"Insurance\" \"Seguro\"," +
                        "\r\npe.\"NUA\"\r\nfrom " +
                        CustomSchema.Schema +
                        "_prueba.\"People\" pe" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"LASTCONTRACTS_PRIORITY\" lc" +
                        "\r\non lc.\"PeopleId\" = pe.\"Id\"" +
                        "\r\nwhere lc.\"Id\" = " + id;
                var rawResult = _context.Database.SqlQuery<PeopleData>(query).Select(x => new
                {
                    x.Id,
                    x.CUNI,
                    x.TipoDocumento,
                    x.Documento,
                    x.Ext,
                    x.Nombres,
                    x.PrimerApellido,
                    x.SegundoApellido,
                    x.ApellidoCasada,
                    FechaNacimiento = x.FechaNacimiento.HasValue ? x.FechaNacimiento.Value.ToString("dd/MM/yyyy") : null,
                    x.Genero,
                    x.Nacionalidad,
                    x.EmailPersonal,
                    x.EmailUCB,
                    x.AFP,
                    x.NUA,
                    x.Edad,
                    x.Seguro
                }).ToList();
                return Ok(rawResult);
            }
            else
            {
                query = "select \r\npe.\"Id\",\r\npe.\"CUNI\"," +
                        "\r\npe.\"Document\" as \"Documento\"," +
                        "\r\ncase when pe.\"TypeDocument\" = 'CI' then 'CARNET DE IDENTIDAD'" +
                        "\r\nwhen pe.\"TypeDocument\" = 'CE' then 'CARNET EXTRANJERO'" +
                        "\r\nwhen pe.\"TypeDocument\" = 'PAS' then 'PASAPORTE'" +
                        " end as \"TipoDocumento\"," +
                        "\r\npe.\"Ext\",\r\npe.\"Names\" as \"Nombres\"," +
                        "\r\npe.\"FirstSurName\" as \"PrimerApellido\"," +
                        "\r\npe.\"SecondSurName\" as \"SegundoApellido\"," +
                        "\r\npe.\"MariedSurName\" as \"ApellidoCasada\"," +
                        "\r\npe.\"BirthDate\" as \"FechaNacimiento\"," +
                        "\r\n case when pe.\"Gender\" = 'F' then 'FEMENINO' " +
                        "\r\nwhen pe.\"Gender\" = 'M' then 'MASCULINO' " +
                        " end as \"Genero\"," +
                        "\r\nYEARS_BETWEEN(TO_DATE(pe.\"BirthDate\"),current_date) as \"Edad\"," +
                        "\r\npe.\"Nationality\" as \"Nacionalidad\"," +
                        "\r\npe.\"PersonalEmail\" as \"EmailPersonal\"," +
                        "\r\npe.\"UcbEmail\" as \"EmailUCB\",\r\npe.\"AFP\", pe.\"Insurance\"  \"Seguro\"," +
                        "\r\npe.\"NUA\"\r\nfrom "+CustomSchema.Schema+".\"People\" pe" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"LASTCONTRACTS_PRIORITY\" lc" +
                        "\r\non lc.\"PeopleId\" = pe.\"Id\"" +
                        "\r\nwhere pe.\"Id\" = " + id;
                //var rawResult = _context.Database.SqlQuery<PeopleData>(query).ToList();
                var rawResult = _context.Database.SqlQuery<PeopleData>(query).Select(x => new
                {
                    x.Id,
                    x.CUNI,
                    x.TipoDocumento,
                    x.Documento,
                    x.Ext,
                    x.Nombres,
                    x.PrimerApellido,
                    x.SegundoApellido,
                    x.ApellidoCasada,
                    FechaNacimiento = x.FechaNacimiento.HasValue ? x.FechaNacimiento.Value.ToString("dd/MM/yyyy") : null,
                    x.Genero,
                    x.Nacionalidad,
                    x.EmailPersonal,
                    x.EmailUCB,
                    x.AFP,
                    x.NUA,
                    x.Edad,
                    x.Seguro
                }).ToList();
                return Ok(rawResult);
            }
            
        }
        //Funcion de regreso de contractdeailt por contractid
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/BusquedaGrupal/ContractData/{id}/{by}")]
        public IHttpActionResult ContractData(int id, string by)
        {
            var query = "";
            if (by == "ContractId")
            {
                query = "select \r\ncd.\"Id\",\r\nbr.\"Abr\" as \"Regional\"," +
                        "\r\ndep.\"Name\" as \"Dependencia\",\r\npos.\"Name\" as \"Posicion\"," +
                        "\r\ntt.\"Value\" as \"Vinculacion\"," +
                        "\r\ncase\r\nwhen cd.\"Dedication\" ='TC' then 'TIEMPO COMPLETO'" +
                        "\r\nwhen cd.\"Dedication\" = 'MT' then 'MEDIO TIEMPO'" +
                        "\r\nwhen cd.\"Dedication\" ='TH' then 'TIEMPO HORARIO'" +
                        "\r\nend as \"Dedicacion\",\r\ncd.\"StartDate\" as \"FechaInicio\"," +
                        "\r\ncd.\"EndDate\" as \"FechaFin\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"LASTCONTRACTS_PRIORITY\" lc" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"People\" pe" +
                        "\r\non lc.\"PeopleId\" = pe.\"Id\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"ContractDetail\" cd" +
                        "\r\non cd.\"PeopleId\" = pe.\"Id\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Dependency\" dep" +
                        "\r\non dep.\"Id\" = cd.\"DependencyId\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Position\" pos" +
                        "\r\non pos.\"Id\" = cd.\"PositionsId\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br" +
                        "\r\non br.\"Id\" = cd.\"BranchesId\"" +
                        "\r\ninner join  " + CustomSchema.Schema + ".\"TableOfTables\" tt" +
                        "\r\non tt.\"Id\" = cd.\"Linkage\"" +
                        "\r\nwhere lc.\"Id\" = " + id + 
                        "\r\norder by cd.\"EndDate\" asc";
                var rawResult = _context.Database.SqlQuery<ContractData>(query).Select(x => new
                {
                    x.Id,
                    x.Regional,
                    x.Dependencia,
                    x.Posicion,
                    x.Vinculacion,
                    x.Dedicacion,
                    FechaInicio = x.FechaInicio.HasValue ? x.FechaInicio.Value.ToString("dd/MM/yyyy") : null,
                    FechaFin = x.FechaFin.HasValue ? x.FechaFin.Value.ToString("dd/MM/yyyy") : null,
                   
                }).ToList();
                return Ok(rawResult);
            }
            else
            {
                query = "select \r\ncd.\"Id\",\r\nbr.\"Abr\" as \"Regional\"," +
                        "\r\ndep.\"Name\" as \"Dependencia\",\r\npos.\"Name\" as \"Posicion\"," +
                        "\r\ntt.\"Value\" as \"Vinculacion\"," +
                        "\r\ncase\r\nwhen cd.\"Dedication\" ='TC' then 'TIEMPO COMPLETO'" +
                        "\r\nwhen cd.\"Dedication\" = 'MT' then 'MEDIO TIEMPO'" +
                        "\r\nwhen cd.\"Dedication\" ='TH' then 'TIEMPO HORARIO'" +
                        "\r\nend as \"Dedicacion\",\r\ncd.\"StartDate\" as \"FechaInicio\"," +
                        "\r\ncd.\"EndDate\" as \"FechaFin\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"LASTCONTRACTS_PRIORITY\" lc" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"People\" pe" +
                        "\r\non lc.\"PeopleId\" = pe.\"Id\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"ContractDetail\" cd" +
                        "\r\non cd.\"PeopleId\" = pe.\"Id\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Dependency\" dep" +
                        "\r\non dep.\"Id\" = cd.\"DependencyId\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Position\" pos" +
                        "\r\non pos.\"Id\" = cd.\"PositionsId\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br" +
                        "\r\non br.\"Id\" = cd.\"BranchesId\"" +
                        "\r\ninner join  " + CustomSchema.Schema + ".\"TableOfTables\" tt" +
                        "\r\non tt.\"Id\" = cd.\"Linkage\"" +
                        "\r\nwhere lc.\"PeopleId\" = " + id +
                        "\r\norder by cd.\"EndDate\" asc";
                var rawResult = _context.Database.SqlQuery<ContractData>(query).Select(x => new
                {
                    x.Id,
                    x.Regional,
                    x.Dependencia,
                    x.Posicion,
                    x.Vinculacion,
                    x.Dedicacion,
                    FechaInicio = x.FechaInicio.HasValue ? x.FechaInicio.Value.ToString("dd/MM/yyyy") : null,
                    FechaFin = x.FechaFin.HasValue ? x.FechaFin.Value.ToString("dd/MM/yyyy") : null,

                }).ToList(); 
                return Ok(rawResult);
            }

        }
        //Filtro de unidad organizacional por regionales a las que el usuario tiene acceso
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/BusquedaGrupal/FiltrarUO/")]
        public IHttpActionResult FiltrarUO()
        {

            var user = auth.getUser(Request);
            var brs = activeDirectory.getUserBranches(user);
            var ubranches = brs.Select(x => x.Id).ToList();

            var query = "select uo.\"Id\", uo.\"Name\", uo.\"Cod\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"OrganizationalUnit\" uo" +
                        "\r\n where uo.\"Active\" = true " +
                        "order by uo.\"Name\"";

            var list = _context.Database.SqlQuery<FiltroBG>(query).ToList();
            var filtered =
                from Lc in list.ToList()
                join branches in ubranches on Lc.BranchesId equals branches
                select Lc;
            return Ok(list);
        }

        //Filtro de unidad organizacional por regionales a las que el usuario tiene acceso
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/BusquedaGrupal/Regional/")]
        public IHttpActionResult FiltrarRegional()
        {

            var user = auth.getUser(Request);
            var brs = activeDirectory.getUserBranches(user);
            var ubranches = brs.Select(x => x.Id).ToList();

            var query = "select uo.\"Id\", uo.\"Name\", uo.\"Abr\", uo.\"Id\" \"BranchesId\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"Branches\" uo" +
                        "order by uo.\"Name\"";

            var list = _context.Database.SqlQuery<RegionalBG>(query).ToList();
            var filtered =
                from Lc in list.ToList()
                join branches in ubranches on Lc.BranchesId equals branches
                select Lc;
            return Ok(list);
        }
        //Filtro de unidad organizacional por regionales a las que el usuario tiene acceso
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/BusquedaGrupal/FiltrarParent/")]
        public IHttpActionResult FiltrarParent()
        {

            var user = auth.getUser(Request);
            var brs = activeDirectory.getUserBranches(user);
            var ubranches = brs.Select(x => x.Id).ToList();

            var query = "select dep.\"Parent\" as \"Id\", uo.\"Name\", uo.\"Cod\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"OrganizationalUnit\" uo" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Dependency\" dep" +
                        "\r\non dep.\"Parent\" = uo.\"Id\"" +
                        "\r\n where dep.\"Active\" = true " +
                        "\r\ngroup by dep.\"Parent\", uo.\"Name\", uo.\"Cod\"" +
                        "\r\norder by uo.\"Name\"";

            var list = _context.Database.SqlQuery<FiltroBG>(query).ToList();
            var filtered =
                from Lc in list.ToList()
                join branches in ubranches on Lc.BranchesId equals branches
                select Lc;
            return Ok(list);
        }
        //Filtro de dependencia por regionales a las que el usuario tiene acceso
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/BusquedaGrupal/FiltrarDep")]
        public IHttpActionResult FiltrarDep()
        {

            var user = auth.getUser(Request);
            var brs = activeDirectory.getUserBranches(user);
            var ubranches = brs.Select(x => x.Id).ToList();

            var query = "select dep.\"Id\", dep.\"Name\", dep.\"Cod\", br.\"Id\" as \"BranchesId\", br.\"Abr\" as \"Branch\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"OrganizationalUnit\" uo" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Dependency\" dep" +
                        "\r\non dep.\"OrganizationalUnitId\" = uo.\"Id\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br" +
                        "\r\non br.\"Id\" = dep.\"BranchesId\"" +
                        "\r\n where dep.\"Active\" = true " +
                        "\r\n order by dep.\"Name\"";

            var list = _context.Database.SqlQuery<FiltroBG>(query).ToList();
            var filtered =
                from Lc in list.ToList()
                join branches in ubranches on Lc.BranchesId equals branches
                select Lc;
            return Ok(filtered.ToList());
        }
        //Filtro dummys proof para dependencia
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/BusquedaGrupal/SelectUO/{group}")]
        public IHttpActionResult SelectUO(string group)
        {

            var user = auth.getUser(Request);
            var brs = activeDirectory.getUserBranches(user);
            var ubranches = brs.Select(x => x.Id).ToList();
            var query = "";
            if (group == "0")
            {
                query = "select dep.\"Id\", dep.\"Name\", dep.\"Cod\", br.\"Id\" as \"BranchesId\", " +
                        "br.\"Abr\" as \"Branch\", uo.\"Id\" as \"OrganizationUnitId\" , uo.\"Name\" as \"OrganizationUnit\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"OrganizationalUnit\" uo" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Dependency\" dep" +
                        "\r\non dep.\"OrganizationalUnitId\" = uo.\"Id\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br" +
                        "\r\non br.\"Id\" = dep.\"BranchesId\"" +
                        "\r\ngroup by dep.\"Id\", dep.\"Name\", dep.\"Cod\", br.\"Id\", \r\nbr.\"Abr\", uo.\"Id\", uo.\"Name\"" +
                        "\r\norder by dep.\"Name\" asc ";

            }
            else
            {
                string[] selected = group.Split(',');

                string whereas = "\r\n where dep.\"OrganizationalUnitId\" in (";

                for (int i = 0; i < selected.Length - 1; i++)
                {
                    whereas = whereas + selected[i] + ",";
                }

                whereas = whereas + selected[selected.Length - 1] + ")";
                query =
                    "select dep.\"Id\", dep.\"Name\", dep.\"Cod\", br.\"Id\" as \"BranchesId\", " +
                    "br.\"Abr\" as \"Branch\", uo.\"Id\" as \"OrganizationUnitId\" , uo.\"Name\" as \"OrganizationUnit\"" +
                    "\r\nfrom " + CustomSchema.Schema + ".\"OrganizationalUnit\" uo" +
                    "\r\ninner join " + CustomSchema.Schema + ".\"Dependency\" dep" +
                    "\r\non dep.\"OrganizationalUnitId\" = uo.\"Id\"" +
                    "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br" +
                    "\r\non br.\"Id\" = dep.\"BranchesId\"" + whereas +
                    "\r\ngroup by dep.\"Id\", dep.\"Name\", dep.\"Cod\", br.\"Id\", \r\nbr.\"Abr\", uo.\"Id\", uo.\"Name\"" +
                    "\r\norder by dep.\"Cod\" asc ";
            }

            var list = _context.Database.SqlQuery<FiltroBG>(query).ToList();
           var filtered =
               from Lc in list.ToList()
               join branches in ubranches on Lc.BranchesId equals branches
               select Lc;
           return Ok(filtered.ToList());
        }
        //Filtro dummys proof para dependencia
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/BusquedaGrupal/{regional}/{unito}/{padre}/{dependencia}/{posicion}/{dedicacion}/{vinculacion}/{personal}/{fechaInicio}/{fechaFin}")]
        public IHttpActionResult BusquedaGrupal(string regional, string unito, string padre, string dependencia, string posicion, string dedicacion, string vinculacion, string personal, string fechaInicio, string fechaFin)
        {

           // var user = auth.getUser(Request);
            //var brs = activeDirectory.getUserBranches(user);
            //var ubranches = brs.Select(x => x.Id).ToList();
            string reg = "";
            string uo = "";
            string pad = "";
            string dep = "";
            string pos = "";
            string ded = "";
            string vin = "";
            string fechas = "";
            string per = "";
            var query = "select lc.\"Id\", lc.\"CUNI\", p.\"Document\", f.\"FullName\", d.\"Name\" \"Dependency\", br.\"Abr\" \"Branches\",\r\nps.\"Name\" \"Positions\", " +
                        "case when lc.\"Active\" = true then 'Activo'\r\nwhen lc.\"Active\" = false then 'Inactivo'\r\nend as \"Status\", lc.\"PositionDescription\"," +
                        "ROW_NUMBER() OVER ( PARTITION BY lc.cuni \r\n\t\t\torder by \t\r\n\t\t\tlc.\"Active\" desc,\r\n\t (case when lc.\"EndDate\" is null\r\n\t\t\t\tthen 1 \r\n\t\t\t\telse 0 \r\n\t\t\t\tend) desc,\r\n\tlc.\"EndDate\" desc ) AS row_num  " + 
                        " from " + CustomSchema.Schema + ".\"ContractDetail\" lc" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Dependency\" d" +
                        "\r\non d.\"Id\" = lc.\"DependencyId\"" + 
                        "\r\ninner join " + CustomSchema.Schema + ".\"People\" p" +
                        "\r\non p.\"CUNI\" = lc.\"CUNI\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"FullName\" f" +
                        "\r\non f.\"CUNI\" = lc.\"CUNI\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br" +
                        "\r\non br.\"Id\" = lc.\"BranchesId\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Position\" ps" +
                        "\r\non ps.\"Id\" = lc.\"PositionsId\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"TableOfTables\" tt1" +
                        "\r\non tt1.\"Value\" = lc.\"Dedication\"" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"TableOfTables\" tt2" +
                        "\r\non tt2.\"Id\" = lc.\"Linkage\"";
            string[] selectedReg = regional.Split(',');

            reg = "\r\n where lc.\"BranchesId\" in (";

            for (int i = 0; i < selectedReg.Length - 1; i++)
            {
                reg = reg + selectedReg[i] + ",";
            }
            reg = reg + selectedReg[selectedReg.Length - 1] + ")";

            if (unito != "0")
            {
                uo = "\r\n and lc.\"OUId\" in (";
                string[] selectedUO = unito.Split(',');
                for (int i = 0; i < selectedUO.Length - 1; i++)
                {
                    uo = uo + selectedUO[i] + ",";
                }
                uo = uo + selectedUO[selectedUO.Length - 1] + ")";
            }
            if (padre != "0")
            {
                pad = "\r\n and d.\"Parent\" in (";
                string[] selectedPadre = padre.Split(',');
                for (int i = 0; i < selectedPadre.Length - 1; i++)
                {
                    pad = pad + selectedPadre[i] + ",";
                }
                pad = pad + selectedPadre[selectedPadre.Length - 1] + ")";
            }
            if (dependencia != "0")
            {
                dep = "\r\n and d.\"Id\" in (";
                string[] selectedDep = dependencia.Split(',');
                for (int i = 0; i < selectedDep.Length - 1; i++)
                {
                    dep = dep + selectedDep[i] + ",";
                }
                dep = dep + selectedDep[selectedDep.Length - 1] + ")";
            }
            if (posicion != "0")
            {
                pos = "\r\n and lc.\"PositionsId\" in (";
                string[] selectedPos = posicion.Split(',');
                for (int i = 0; i < selectedPos.Length - 1; i++)
                {
                    pos = pos + selectedPos[i] + ",";
                }
                pos = pos + selectedPos[selectedPos.Length - 1] + ")";
            }
            if (dedicacion != "0")
            {
                ded = "\r\n and tt1.\"Id\" in (";
                string[] selectedDed = dedicacion.Split(',');
                for (int i = 0; i < selectedDed.Length - 1; i++)
                {
                    ded = ded + selectedDed[i] + ",";
                }
                ded = ded + selectedDed[selectedDed.Length - 1] + ")";
            }
            if (vinculacion != "0")
            {
                vin = "\r\n and tt2.\"Id\" in (";
                string[] selectedVin = vinculacion.Split(',');
                for (int i = 0; i < selectedVin.Length - 1; i++)
                {
                    vin = vin + selectedVin[i] + ",";
                }
                vin = vin + selectedVin[selectedVin.Length - 1] + ")";
            }
            if (fechaFin != "0" && fechaInicio != "0")
            {
                fechas = "\r\nand lc.\"StartDate\" >= '" + fechaInicio + "' \r\n and lc.\"EndDate\" <= '" + fechaFin + "'";
            }

            if (personal == "Activo")
            {
                per = "\r\n and lc.\"Active\" = true";
            }
            if (personal == "Inactivo")
            {
                per = "\r\n and lc.\"Active\" = false";
            }
            query = "Select * from (" + query + reg + uo + pad + dep + pos + ded + vin + fechas + per + ") where row_num = 1 order by \"Status\", \"FullName\"";

            var list = _context.Database.SqlQuery<ContractDetailViewModel>(query);
            var result = list.ToList().Select(x => new
            {
                x.Id,
                x.CUNI,
                x.Document,
                x.FullName,
                x.PositionDescription,
                x.Dependency,
                x.Branches,
                Estado = x.Status
            }).ToList();

           /*
            var filtered =
                from Lc in list.ToList()
                join branches in ubranches on Lc.BranchesId equals branches
                select Lc;
            */
            return Ok(result);
        }
        //Organigrama
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Organigrama/{IdDep}")]
        public IHttpActionResult Organigrama(int IdDep)
        {

            var user = auth.getUser(Request);
            var brs = activeDirectory.getUserBranches(user);
            var ubranches = brs.Select(x => x.Id).ToList();

            var query = "select    \r\ncase\r\nwhen p7.\"Name\" = 'Sin Padre' then null" +
                        "\r\nelse p7.\"Name\"\r\nend as parent7_id,\r\ncase\r\nwhen p7.\"Cod\" = 0 then null\r\nelse p7.\"Cod\"\r\nend as \"Cod7\",  \r\n     \r\ncase   when p6.\"Name\" = 'Sin Padre' then null\r\nelse p6.\"Name\"\r\nend as parent6_id,\r\ncase\r\nwhen p6.\"Cod\" = 0 then null\r\nelse p6.\"Cod\"\r\nend as \"Cod6\",   \r\n           \r\n case  when p5.\"Name\" = 'Sin Padre' then null\r\nelse p5.\"Name\"\r\nend as parent5_id,\r\ncase\r\nwhen p5.\"Cod\" = 0 then null\r\nelse p5.\"Cod\"\r\nend as \"Cod5\",    \r\n    \r\ncase   when p4.\"Name\" = 'Sin Padre' then null\r\nelse p4.\"Name\"\r\nend as parent4_id,\r\ncase\r\nwhen p4.\"Cod\" = 0 then null\r\nelse p4.\"Cod\"\r\nend as \"Cod4\",     \r\n           \r\ncase   when p3.\"Name\" = 'Sin Padre' then null\r\nelse p3.\"Name\"\r\nend as parent3_id,\r\ncase\r\nwhen p3.\"Cod\" = 0 then null\r\nelse p3.\"Cod\"\r\nend as \"Cod3\",   \r\n             \r\ncase  when p2.\"Name\" = 'Sin Padre' then null\r\nelse p2.\"Name\"\r\nend as parent2_id,\r\ncase\r\nwhen p2.\"Cod\" = 0 then null\r\nelse p2.\"Cod\"\r\nend as \"Cod2\",    \r\n                \r\n p1.\"Name\" as \"Dep\", \r\n p1.\"Cod\" \"Cod\",              \r\n p1.\"Id\" as product_id,              \r\n p1.\"BranchesId\",              \r\n b.\"Name\" \"Regional\"  " +
                        "\r\nfrom        " +
                        CustomSchema.Schema +".\"Dependency\" p1\r\nleft join  " +
                        CustomSchema.Schema + ".\"Dependency\" p2 on p2.\"Id\" = p1.\"Parent\" \r\nleft join   " +
                        CustomSchema.Schema + ".\"Dependency\" p3 on p3.\"Id\" = p2.\"Parent\" \r\nleft join   " +
                        CustomSchema.Schema + ".\"Dependency\" p4 on p4.\"Id\" = p3.\"Parent\"  \r\nleft join   " +
                        CustomSchema.Schema + ".\"Dependency\" p5 on p5.\"Id\" = p4.\"Parent\"  \r\nleft join   " +
                        CustomSchema.Schema + ".\"Dependency\" p6 on p6.\"Id\" = p5.\"Parent\"\r\nleft join  " +
                        CustomSchema.Schema + ".\"Dependency\" p7 on p7.\"Id\" = p6.\"Parent\"\r\ninner join " +CustomSchema.Schema +".\"Branches\" b on b.\"Id\" = p1.\"BranchesId\"\r\nwhere       " +
                        "p1.\"Id\" =" + IdDep + "\r\n order       by 1, 2, 3, 4, 5, 6, 7;";

            var list = _context.Database.SqlQuery<Chart>(query).ToList();
            var filtered =
                from Lc in list.ToList()
                join branches in ubranches on Lc.BranchesId equals branches
                select Lc;
            //return Ok(filtered.ToList());

            string parent7 = "",
                parent6 = "",
                parent5 = "",
                parent4 = "",
                parent3 = "",
                parent2 = "",
                parent = "",
                regional = "";
            foreach (var aPart in filtered)
            {

                parent7 =aPart.Cod7 + "-" + aPart.PARENT7_ID;
                parent6 = aPart.Cod6 + "-" + aPart.PARENT6_ID;
                parent5 = aPart.Cod5 + "-" + aPart.PARENT5_ID;
                parent4 = aPart.Cod4 + "-" + aPart.PARENT4_ID;
                parent3 = aPart.Cod3 + "-" + aPart.PARENT3_ID;
                parent2 = aPart.Cod2 + "-" + aPart.PARENT2_ID;
                parent = aPart.Cod + "-" + aPart.Dep;
                regional = aPart.Regional;
            }

            var root = new MyObject();
            if (parent6 == "-" && parent5 == "-" && parent4 == "-" && parent3 == "-" && parent2 != "-")
            {
                root = new MyObject()
                {
                    name = regional,
                    children = new List<MyObject>() 
                {
                    new MyObject()
                    {
                        name = parent2,
                        children = new List<MyObject>() 
                        {
                            new MyObject()
                            {
                                name = parent,
                            }
                        },
                    }
                }
                };
            }

            if (parent6 == "-" && parent5 == "-" && parent4 == "-" && parent3 != "-")
            {
                root = new MyObject()
                {
                    name = regional,
                    children = new List<MyObject>() 
                    {
                        new MyObject()
                                {
                                    name = parent3,
                                    children = new List<MyObject>() 
                                    {
                                        new MyObject()
                                        {
                                            name = parent2,
                                            children = new List<MyObject>() 
                                            {
                                                new MyObject()
                                                {
                                                    name = parent,
                                                }
                                            },
                                        }
                                    }
                                }
                    }
                };
            }
            if (parent6 == "-" && parent5 == "-" && parent4 != "-")
            {
                root = new MyObject()
                {
                    name = regional,
                    children = new List<MyObject>() 
                    {
                                new MyObject()
                                {
                                    name = parent4,
                                    children = new List<MyObject>() 
                                    {
                                        new MyObject()
                                        {
                                            name = parent3,
                                            children = new List<MyObject>() 
                                            {
                                                new MyObject()
                                                {
                                                    name = parent2,
                                                    children = new List<MyObject>() 
                                                    {
                                                        new MyObject()
                                                        {
                                                            name = parent,
                                                        }
                                                    },
                                                }
                                            }
                                        }
                                    }

                        } 
                    }
                };
            }
            if (parent6 == "-" && parent5 != "-")
            {
                root = new MyObject()
                {
                    name = regional,
                    children = new List<MyObject>() 
                    {
                        new MyObject()
                        {
                            name = parent5,
                            children = new List<MyObject>() 
                            {
                                new MyObject()
                                {
                                    name = parent4,
                                    children = new List<MyObject>() 
                                    {
                                        new MyObject()
                                        {
                                            name = parent3,
                                            children = new List<MyObject>() 
                                            {
                                                new MyObject()
                                                {
                                                    name = parent2,
                                                    children = new List<MyObject>() 
                                                    {
                                                        new MyObject()
                                                        {
                                                            name = parent,
                                                        }
                                                    },
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                        } 
                    }
                };
            }
            if (parent6 == "-" && parent7 != "-")
            {
                root = new MyObject()
                {
                    name = regional,
                    children = new List<MyObject>() 
                {
                            new MyObject()
                            {
                                name = parent6,
                                children = new List<MyObject>() 
                                {
                                    new MyObject()
                                    {
                                        name = parent5,
                                        children = new List<MyObject>() 
                                        {
                                            new MyObject()
                                            {
                                                name = parent4,
                                                children = new List<MyObject>() 
                                                {
                                                    new MyObject()
                                                    {
                                                        name = parent3,
                                                        children = new List<MyObject>() 
                                                        {
                                                            new MyObject()
                                                            {
                                                                name = parent2,
                                                                children = new List<MyObject>() 
                                                                {
                                                                    new MyObject()
                                                                    {
                                                                        name = parent,
                                                                    }
                                                                },
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                        }
                    }
                }
                };
            }
            return Ok(root);
            
        }
        //Busqueda individual Activo
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/BusquedaIndividualActivo/")]
        public IHttpActionResult BusquedaInidividualActivo()
        {

             var user = auth.getUser(Request);
            var brs = activeDirectory.getUserBranches(user);
            var ubranches = brs.Select(x => x.Id).ToList();

            var query = "select\r\nlc.\"PeopleId\",\r\nlc.\"Id\",\r\nlc.\"CUNI\"," +
                        "\r\nlc.\"Document\" \"Documento\",\r\nlc.\"FullName\" \"Nombre\",\r\nlc.\"Positions\" \"Posicion\"," +
                        "\r\nlc.\"Linkage\" \"Vinculacion\",\r\nlc.\"Dependency\" \"Dependencia\",\r\nlc.\"Branches\" \"Regional\", lc.\"BranchesId\"," +
                        "case when lc.\"Active\" = true then 'Activo'\r\nwhen lc.\"Active\" = false then 'Inactivo'\r\nend as \"Status\", lc.\"StartDate\" \"FechaInicio\", lc.\"EndDate\" \"FechaFin\", lc.\"PositionDescription\" \"Cargo\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"LASTCONTRACTS_PRIORITY\" lc" +
                        "\r\nwhere lc.\"Active\" = true order by lc.\"FullName\"";

            var list = _context.Database.SqlQuery<BusquedaIndividual>(query).ToList();
            var filtered =
                from Lc in list.ToList()
                join branches in ubranches on Lc.BranchesId equals branches
                select Lc;
            return Ok(filtered.ToList());
        }
        //Busqueda individual Historico
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/BusquedaIndividualHistorico/")]
        public IHttpActionResult BusquedaInidividualHistorico()
        {

            var user = auth.getUser(Request);
            var brs = activeDirectory.getUserBranches(user);
            var ubranches = brs.Select(x => x.Id).ToList();

            var query = "select\r\nlc.\"PeopleId\",\r\nlc.\"Id\",\r\nlc.\"CUNI\"," +
                        "\r\nlc.\"Document\" \"Documento\",\r\nlc.\"FullName\" \"Nombre\",\r\nlc.\"Positions\" \"Posicion\"," +
                        "\r\nlc.\"Linkage\" \"Vinculacion\",\r\nlc.\"Dependency\" \"Dependencia\",\r\nlc.\"Branches\" \"Regional\", lc.\"BranchesId\"," +
                        "case when lc.\"Active\" = true then 'Activo'\r\nwhen lc.\"Active\" = false then 'Inactivo'\r\nend as \"Status\", lc.\"StartDate\" \"FechaInicio\", lc.\"EndDate\" \"FechaFin\", lc.\"PositionDescription\" \"Cargo\"" +
                        "\r\nfrom " + CustomSchema.Schema + ".\"LASTCONTRACTS_PRIORITY\" lc order by lc.\"FullName\"";

            var list = _context.Database.SqlQuery<BusquedaIndividual>(query).ToList();
            var filtered =
                from Lc in list.ToList()
                join branches in ubranches on Lc.BranchesId equals branches
                select Lc;
            return Ok(filtered.ToList());
        }
        //Busqueda individual Historico
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Novedades/Altas")]
        public IHttpActionResult Altas()
        {

            var user = auth.getUser(Request);
            var brs = activeDirectory.getUserBranches(user);
            var ubranches = brs.Select(x => x.Id).ToList();

            var query = "select *\r\nfrom "+CustomSchema.Schema+".\"FIRSTCONTRACTS\" fc" +
                        "\r\nwhere month(fc.\"StartDate\") = month(current_date)" +
                        "\r\nand year(fc.\"StartDate\") = year(current_date)";

            var list = _context.Database.SqlQuery<BusquedaIndividual>(query).ToList();
            var filtered =
                from Lc in list.ToList()
                join branches in ubranches on Lc.BranchesId equals branches
                select Lc;
            return Ok(filtered.ToList());
        }
        //Busqueda individual Historico
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/Novedades/Bajas")]
        public IHttpActionResult Bajas()
        {

            var user = auth.getUser(Request);
            var brs = activeDirectory.getUserBranches(user);
            var ubranches = brs.Select(x => x.Id).ToList();

            var query = "select *" +
                        "\r\nfrom "+CustomSchema.Schema+".\"LASTCONTRACTS\" lc" +
                        "\r\nwhere lc.\"EndDate\" <= current_date" +
                        "\r\nand lc.\"Active\" = true";

            var list = _context.Database.SqlQuery<BusquedaIndividual>(query).ToList();
            var filtered =
                from Lc in list.ToList()
                join branches in ubranches on Lc.BranchesId equals branches
                select Lc;
            return Ok(filtered.ToList());
        }

        //Cargos
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/BusquedaGrupal/Posicion")]
        public IHttpActionResult PosicionResult()
        {

            var user = auth.getUser(Request);
            var brs = activeDirectory.getUserBranches(user);
            var ubranches = brs.Select(x => x.Id).ToList();

            var query = "select pos.\"Id\", pos.\"Name\", l.\"Cod\"" +
                        "\r\nfrom "+CustomSchema.Schema+".\"Position\" pos" +
                        "\r\ninner join " + CustomSchema.Schema + ".\"Level\" l" +
                        "\r\non pos.\"LevelId\" = l.\"Id\"";

            var list = _context.Database.SqlQuery<PosicionBG>(query).ToList();
            return Ok(list);
        }
    
    }
}
