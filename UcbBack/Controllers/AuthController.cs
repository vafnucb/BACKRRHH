using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using UcbBack.Logic;
using UcbBack.Models;
using UcbBack.Models.Auth;
using System.Data.Entity;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Controllers
{
    public class AuthController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidateToken validator;
        private ValidateAuth validateauth;
        private ADClass activeDirectory;

        public AuthController()
        {
            _context = new ApplicationDbContext();
            validator = new ValidateToken();
            validateauth = new ValidateAuth();
            activeDirectory = new ADClass();
        }

        [HttpGet]
        [Route("api/auth/GetMenu")]
        public IHttpActionResult GetMenu()
        {
            var start = DateTime.Now;
            int userid;
            IEnumerable<string> headerId;
            if (!Request.Headers.TryGetValues("id", out headerId))
                return BadRequest();
            if (!Int32.TryParse(headerId.FirstOrDefault(), out userid))
                return BadRequest();

            var user = _context.CustomUsers.Include(x => x.People).FirstOrDefault(cu => cu.Id == userid);
            if (user == null)
                return Unauthorized();
            var uexist = DateTime.Now;

            var rls = activeDirectory.getUserRols(user).Select(x => x.Id);
            var ugetrols = DateTime.Now;

            var br = activeDirectory.getUserBranches(user);
            var ugetbr = DateTime.Now;

            List<Access> access;
            // activeDirectory.AddUserToGroup("G.ARANA.M@UCB.BO", "Personas.Segmentos.Cochabamba");
            //if admin return all
            if (activeDirectory.memberOf(user, "Personas.Admin"))
            {
                access = _context.Accesses
                    .Include(a => a.Resource.Module)
                    .Include(a => a.Resource).ToList();
            }
            // else search all the user access
            else
            {
                access = _context.RolshaAccesses.Include(a => a.Access)
                    .Include(a => a.Rol)
                    .Include(a => a.Access.Resource.Module)
                    .Include(a => a.Access.Resource).ToList()
                    .Where(r => rls.Contains(r.Rolid)).Select(a => a.Access).ToList();
            }

            List<dynamic> res = new List<dynamic>();
            var listModules = access.Select(a => a.Resource.Module).Distinct().OrderBy(x => x.Id);
            var listResources = access.Select(a => a.Resource).Distinct().OrderBy(x => x.Id);
            foreach (var module in listModules)
            {
                List<dynamic> children = new List<dynamic>();
                foreach (var child in listResources.Where(c => c.ModuleId == module.Id))
                {
                    var listmethods = access.Where(a => a.ResourceId == child.Id).Select(a => a.Method).Distinct();
                    dynamic c = new JObject();
                    c.name = child.Name;
                    c.path = child.Path;
                    c.methods = JArray.FromObject(listmethods.ToArray());
                    children.Add(c);
                }

                dynamic r = new JObject();
                r.name = module.Name;
                r.icon = module.Icon;
                r.collapsed = true;
                r.children = JArray.FromObject(children.ToArray());
                res.Add(r);
            }
            var caljson = DateTime.Now;

            var t1 = uexist - start;
            var t2 = ugetrols - uexist;
            var t3 = ugetbr - ugetrols;
            var t4 = caljson - ugetbr;
            return Ok(res);
        }

        // POST: /api/auth/gettoken/
        [HttpPost]
        [Route("api/auth/GetToken")]
        public IHttpActionResult GetToken([FromBody]JObject credentials)
        {
            if (credentials["username"] == null || credentials["password"] == null)
                return BadRequest();

            string username = credentials["username"].ToString().ToUpper();
            string password = credentials["password"].ToString();

            CustomUser user = _context.CustomUsers.FirstOrDefault(u => u.UserPrincipalName == username);

            //if(user==null)
            //     return Unauthorized();

            if (!activeDirectory.ActiveDirectoryAuthenticate(username, password))
                return Unauthorized();

            user.Token = validator.getToken(user);
            user.TokenCreatedAt = DateTime.Now;
            user.RefreshToken = validator.getRefreshToken(user);
            user.RefreshTokenCreatedAt = DateTime.Now;
            _context.SaveChanges();

            //HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            //response.Headers.Add("Id", user.Id.ToString());
            //response.Headers.Add("Token", user.Token);
            //response.Headers.Add("RefreshToken", user.RefreshToken);
            //return ResponseMessage(response);
            var rols = activeDirectory.getUserRols(user);
            var principalrol = rols.OrderByDescending(x => x.Level).FirstOrDefault();
            // var principalrol = rols.OrderBy(x => x.Level).FirstOrDefault();
            if (principalrol == null)
            {
                return Unauthorized();
            }

            dynamic respose = new JObject();
            respose.Id = user.Id;
            respose.Token = user.Token;
            respose.RefreshToken = user.RefreshToken;
            respose.ExpiresIn = validateauth.tokenLife;
            respose.RefreshExpiresIn = validateauth.refeshtokenLife;
            respose.AccessDefault = principalrol.Resource.Path;
            return Ok(respose);
        }

        // POST: /api/auth/RefreshToken/
        [HttpPost]
        [Route("api/auth/RefreshToken/")]
        public IHttpActionResult RefreshToken(JObject data)
        {

            IEnumerable<string> idlist;
            if (!Request.Headers.TryGetValues("id", out idlist))
                return BadRequest();
            if (data["RefreshToken"] == null)
                return BadRequest();

            int userid = 0;
            if (!Int32.TryParse(idlist.First(), out userid))
                return Unauthorized();
            string rt = data["RefreshToken"].ToString();
            CustomUser user = _context.CustomUsers.FirstOrDefault(u => u.Id == userid && u.RefreshToken == rt);
            if (user == null)
                return Unauthorized();
            if (user.RefreshTokenCreatedAt == null)
                return Unauthorized();

            int seconds = (int)DateTime.Now.Subtract(user.RefreshTokenCreatedAt.Value).TotalSeconds;

            if (seconds > validateauth.refeshtokenLife)
                return Unauthorized();

            user.Token = validator.getToken(user);
            user.TokenCreatedAt = DateTime.Now;

            _context.SaveChanges();

            dynamic respose = new JObject();
            respose.Token = user.Token;
            respose.ExpiresIn = validateauth.tokenLife;
            respose.RefreshExpiresIn = validateauth.refeshtokenLife - ((int)DateTime.Now.Subtract(user.RefreshTokenCreatedAt.Value).TotalSeconds);

            return Ok(respose);
        }

        [HttpGet]
        [Route("api/auth/Logout/")]
        public IHttpActionResult Logout()
        {
            IEnumerable<string> tokenlist;
            IEnumerable<string> idlist;
            if (!Request.Headers.TryGetValues("token", out tokenlist) || !Request.Headers.TryGetValues("id", out idlist))
                return Unauthorized();
            int userid = 0;

            if (!Int32.TryParse(idlist.First(), out userid))
                return Unauthorized();

            string token = tokenlist.First();
            CustomUser user = _context.CustomUsers.FirstOrDefault(u => u.Id == userid && u.Token == token);
            if (user == null)
                return Unauthorized();

            user.Token = null;
            user.TokenCreatedAt = null;
            user.RefreshToken = null;
            user.RefreshTokenCreatedAt = null;
            _context.SaveChanges();

            return Ok();
        }

        [HttpGet]
        [Route("api/auth/UserData")]
        public IHttpActionResult UserData()
        {
            var user = validateauth.getUser(Request).People.CUNI;
            string name = _context.Database.SqlQuery<string>("select \"FullName\" from " + CustomSchema.Schema + ".\"FullName\" where \"CUNI\" = '" + user + "'").FirstOrDefault();
            return Ok(name);
        }
    }
}
