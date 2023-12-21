using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using UcbBack.Logic;
using UcbBack.Models;
using UcbBack.Models.Auth;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using UcbBack.Models.Not_Mapped.ViewMoldes;

namespace UcbBack.Controllers
{
    public class CustomUserController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidateToken validator;
        private ADClass activeDirectory;

        public CustomUserController()
        {
            _context = new ApplicationDbContext();
            validator = new ValidateToken();
            activeDirectory = new ADClass();
        }

        // GET api/user
        [Route("api/user/")]
        public IHttpActionResult Get()
        {
            /*var userlist = _context.CustomUsers.Include(x=>x.People)
                .ToList().
                Select(x => new
                {
                    x.Id,
                    x.UserPrincipalName,
                    person = x.People.GetFullName(),
                    x.PeopleId,
                    x.AutoGenPass,
                    x.TipoLicenciaSAP,
                    x.CajaChica,
                    x.SolicitanteCompras,
                    x.AutorizadorCompras,
                    x.Rendiciones
                });*/
            var query = "select u.\"Id\", p.\"SAPCodeRRHH\", p.cuni, p.\"Document\", c.\"FullName\", " +
            " case when u.\"AutorizadorCompras\" = true then 'APROBADOR' when u.\"SolicitanteCompras\" = true then 'SOLICITANTE' when u.\"CajaChica\" = true then 'CAJA CHICA' when u.\"Rendiciones\" = true then 'RENDICIONES' ELSE 'SIN ROL' END AS \"Rol\"," +
            " case when auth.\"FullName\" is null then null else 'Las solicitudes que usted realice deberan ser aprobadas por: ' end as \"MensajeAprobacion\"," +
            " p.\"UcbEmail\",p.\"PersonalEmail\",coalesce(u.\"UserPrincipalName\",'Sin Usuario') as \"UserPrincipalName\", c.\"DependencyCod\", c.\"Dependency\", ou.\"Cod\" as \"OUCod\", " +
            " ou.\"Name\" as \"OUName\", c.\"Positions\", " +
            " auth.\"FullName\" as \"AuthFullName\", br.\"Name\" as \"Branches\", u.\"AutoGenPass\", " +
            " case when (c.\"Active\" = false and c.\"EndDate\" < current_date) then 'INACTIVO' else 'ACTIVO' end as \"State\" " +
            " from " + CustomSchema.Schema + ".lastcontracts_priority c " +
            " inner join " + CustomSchema.Schema + ".\"Branches\" br " +
            "    on c.\"BranchesId\" = br.\"Id\" " +
            " left join " + CustomSchema.Schema + ".\"User\" u " +
            "    on c.\"PeopleId\" = u.\"PeopleId\" " +
            " inner join " + CustomSchema.Schema + ".\"People\" p " +
            "    on c.\"PeopleId\" = p.\"Id\" " +
            " inner join " + CustomSchema.Schema + ".\"OrganizationalUnit\" ou " +
            "    on c.\"OUId\" = ou.\"Id\" " +
            " left join " + CustomSchema.Schema + ".lastcontracts auth " +
            "   on u.\"AuthPeopleId\" = auth.\"PeopleId\" " +
            " left join " + CustomSchema.Schema + ".\"People\" pauth " +
            "    on auth.\"PeopleId\" = pauth.\"Id\"" +
            //" where c.\"EndDate\" is null or c.\"EndDate\" > current_date" +
            " order by (case when u.\"UserPrincipalName\" is null then 1 else 0 end) asc," +
            "    c.\"FullName\"";
            var rawresult = _context.Database.SqlQuery<UserViewModel>(query).Select(x=>new
            {
                x.Id,
                x.CUNI,
                x.Document,
                x.FullName,
                x.Rol,
                x.UcbEmail,
                x.PersonalEmail,
                x.UserPrincipalName,
                x.DependencyCod,
                x.Dependency,
                x.OUName,
                x.OUCod,
                x.Positions,
                x.AuthFullName,
                x.Branches,
                x.AutoGenPass,
                x.MensajeAprobacion,
                x.State

            }).ToList();
            return Ok(rawresult);
        }

        [HttpPost]
        [Route("api/user/Branches/{id}")]
        public IHttpActionResult AddSegment(int id, [FromBody]JObject credentials)
        {
            CustomUser userInDB = null;

            userInDB = _context.CustomUsers.Include(x => x.People).FirstOrDefault(d => d.Id == id);

            if (userInDB == null)
                return NotFound();

            int branchesId = 0;
            if (credentials["BranchesId"] == null)
                return BadRequest();

            if (!Int32.TryParse(credentials["BranchesId"].ToString(), out branchesId))
                return BadRequest();

            var branchInDB = _context.Branch.FirstOrDefault(b => b.Id == branchesId);

            if (branchInDB == null)
                return BadRequest();

            activeDirectory.AddUserToGroup(userInDB.UserPrincipalName, branchInDB.ADGroupName);

            return Ok();
        }

        [HttpGet]
        [Route("api/user/Branches/{id}")]
        public IHttpActionResult GetSegments(int id)
        {
            CustomUser userInDB = null;

            userInDB = _context.CustomUsers.Include(x => x.People).FirstOrDefault(d => d.Id == id);

            if (userInDB == null)
                return NotFound();

            var br = activeDirectory.getUserBranches(userInDB).Select(x=>new {x.Id,x.Abr,x.Name});
            
            return Ok(br);
        }

        [HttpDelete]
        [Route("api/user/Branches/{id}")]
        public IHttpActionResult RemoveSegment(int id, [FromUri]int branchesId)
        {
            CustomUser userInDB = null;

            userInDB = _context.CustomUsers.Include(x => x.People).FirstOrDefault(d => d.Id == id);

            if (userInDB == null)
                return NotFound();

            if (branchesId == 0)
                return BadRequest();

            var branchInDB = _context.Branch.FirstOrDefault(b => b.Id == branchesId);

            if (branchInDB == null)
                return BadRequest();

            activeDirectory.RemoveUserFromGroup(userInDB.UserPrincipalName, branchInDB.ADGroupName);

            return Ok();
        }

        [HttpGet]
        [Route("api/user/Rol/{id}")]
        public IHttpActionResult GetRols(int id)
        {
            CustomUser userInDB = null;
            userInDB = _context.CustomUsers.Include(x => x.People).FirstOrDefault(d => d.Id == id);

            if (userInDB == null)
                return NotFound();

            var rols = activeDirectory.getUserRols(userInDB).Select(x => new { x.Id, x.Name });
            return Ok(rols);
        }

        [HttpPost]
        [Route("api/user/Rol/{id}")]
        public IHttpActionResult AddRol(int id, [FromBody]JObject credentials)
        {
            CustomUser userInDB = null;

            userInDB = _context.CustomUsers.Include(x => x.People).FirstOrDefault(d => d.Id == id);

            if (userInDB == null)
                return NotFound();

            int rolId = 0;
            if (credentials["RolId"] == null)
                return BadRequest();

            if (!Int32.TryParse(credentials["RolId"].ToString(), out rolId))
                return BadRequest();

            var rolInDB = _context.Rols.FirstOrDefault(b => b.Id == rolId);

            if (rolInDB == null)
                return BadRequest();

            activeDirectory.AddUserToGroup(userInDB.UserPrincipalName, rolInDB.ADGroupName);

            return Ok();
        }

        [HttpDelete]
        [Route("api/user/Rol/{id}")]
        public IHttpActionResult RemoveRol(int id, [FromUri]int rolId)
        {
            CustomUser userInDB = null;

            userInDB = _context.CustomUsers.Include(x => x.People).FirstOrDefault(d => d.Id == id);

            if (userInDB == null)
                return NotFound();

            if (rolId == 0)
                return BadRequest();

            var rolInDB = _context.Rols.FirstOrDefault(b => b.Id == rolId);

            if (rolInDB == null)
                return BadRequest();

            activeDirectory.RemoveUserFromGroup(userInDB.UserPrincipalName, rolInDB.ADGroupName);

            return Ok();
        }

        [Route("api/user/DefAuth/{id}")]
        public IHttpActionResult GetAuth(int id)
        {
            var userInDB = _context.Person.FirstOrDefault(d => d.Id == id);

            if (userInDB == null)
                return NotFound();
            return Ok(userInDB.GetLastManagerAuthorizator(_context).Id);
        }

        // GET api/user/5
        [Route("api/user/{id}")]
        public IHttpActionResult Get(int id)
        {
            CustomUser userInDB = null;

            userInDB = _context.CustomUsers.Include(x=>x.People).FirstOrDefault(d => d.Id == id);

            if (userInDB == null)
                return NotFound();
            dynamic respose = new JObject();
            respose.Id = userInDB.Id;
            respose.UserPrincipalName = userInDB.UserPrincipalName;
            respose.PeopleId = userInDB.People.Id;
            respose.Name = userInDB.People.GetFullName();
            respose.Gender = userInDB.People.Gender;
            respose.TipoLicenciaSAP = userInDB.TipoLicenciaSAP;
            respose.CajaChica = userInDB.CajaChica;
            respose.SolicitanteCompras = userInDB.SolicitanteCompras;
            respose.AutorizadorCompras = userInDB.AutorizadorCompras;
            respose.Rendiciones = userInDB.Rendiciones;
            respose.RendicionesDolares = userInDB.RendicionesDolares;
            respose.AuthPeopleId = userInDB.AuthPeopleId;
            respose.UcbEmail = userInDB.People.UcbEmail;

            return Ok(respose);
        }

        // POST: /api/user/
        [HttpPost]
        [Route("api/user/")]
        public IHttpActionResult Register([FromBody]CustomUser user)
        {
            //if (!ModelState.IsValid)
            //    return BadRequest(ModelState);
            var person = _context.Person.FirstOrDefault(x => x.Id == user.PeopleId);
            
            List<string> palabras = new List<string>(new string[]
            {
                "aula",
                "libro",
                "lapiz",
                "papel",
                "folder",
                "lentes"
            });

            Random rnd = new Random();

            string pass = palabras[rnd.Next(6)];
            while (pass.Length < 8)
            {
                pass += rnd.Next(10);
            }

            CustomUser account;
            var ex = _context.CustomUsers.FirstOrDefault(x => x.PeopleId == person.Id);
            if (ex == null)
            {
                activeDirectory.adddOrUpdate(person, pass);
                _context.SaveChanges();
                account = _context.CustomUsers.Include(x=>x.People).FirstOrDefault(x => x.PeopleId == person.Id);
                account.AutoGenPass = pass;
                account.TipoLicenciaSAP = user.TipoLicenciaSAP;
                account.CajaChica = user.CajaChica == null ? false : user.CajaChica.Value;
                account.SolicitanteCompras = user.SolicitanteCompras == null ? false : user.SolicitanteCompras.Value;
                account.AutorizadorCompras = user.AutorizadorCompras == null ? false : user.AutorizadorCompras.Value;
                account.Rendiciones = user.Rendiciones == null ? false:user.Rendiciones.Value;
                account.RendicionesDolares = user.RendicionesDolares == null ? false : user.RendicionesDolares.Value;
                account.AuthPeopleId = user.AuthPeopleId;
                _context.SaveChanges();

                if (account.Rendiciones.Value || account.CajaChica.Value || account.RendicionesDolares.Value)
                {
                    account.CreateInRendiciones(_context);
                    account.updatePerfilesRend(_context);
                }
                _context.SaveChanges();
            }
            else
            {
                return Ok();
            }

            user = account;

            dynamic respose = new JObject();
            respose.Id = user.Id;
            respose.UserPrincipalName = user.UserPrincipalName;

            return Created(new Uri(Request.RequestUri + "/" + respose.Id), respose);
        }

        // GET api/user
        [HttpPut]
        [Route("api/user/{id}")]
        public IHttpActionResult Put(int id, UserViewModel user)
        {
            var userInDb = _context.CustomUsers.Include(x=>x.People).FirstOrDefault(x => x.Id == id);
            if (userInDb == null)
                return NotFound();
            userInDb.TipoLicenciaSAP = user.TipoLicenciaSAP;
            userInDb.CajaChica = user.CajaChica;
            userInDb.SolicitanteCompras = user.SolicitanteCompras;
            userInDb.AutorizadorCompras = user.AutorizadorCompras;
            userInDb.Rendiciones = user.Rendiciones;
            userInDb.RendicionesDolares = user.RendicionesDolares;
            userInDb.AuthPeopleId = user.AuthPeopleId;
            userInDb.People.UcbEmail = user.UcbEmail;
            userInDb.People.PersonalEmail = user.PersonalEmail;
            if (userInDb.Rendiciones.Value || userInDb.CajaChica.Value || userInDb.RendicionesDolares.Value)
                userInDb.CreateInRendiciones(_context);
            userInDb.updatePerfilesRend(_context);
            _context.SaveChanges();
            return Ok(userInDb);

        }

        // DELETE api/user/5
        [HttpPost]
        [Route("api/user/ChangeStatus")]
        public IHttpActionResult ChangeStatus(int id)
        {
            var userInDB = _context.CustomUsers.FirstOrDefault(d => d.Id == id);
            if (userInDB == null)
                return NotFound();

            //_context.CustomUsers.Remove(userInDB);
            _context.SaveChanges();
            return Ok();
        }
    }
}
