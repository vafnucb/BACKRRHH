using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using UcbBack.Logic;
using UcbBack.Models;
using UcbBack.Models.Auth;
using System.Data.Entity;

namespace UcbBack.Controllers
{
    public class RolController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidatePerson validator;
        private ADClass activeDirectory;

        public RolController()
        {
            _context = new ApplicationDbContext();
            validator = new ValidatePerson(_context);
            activeDirectory = new ADClass();
        }

        // GET api/Rol
        public IHttpActionResult Get()
        {
            return Ok(_context.Rols.Include("Resource").Select(r => new { r.Id, r.Name, r.Level, r.ResourceId, Resource=r.Resource.Name,r.ADGroupName }).OrderBy(x=>x.Id).ToList());
        }

        // GET api/Rol/5
        public IHttpActionResult Get(int id)
        {
            Rol rolInDB = null;

            rolInDB = _context.Rols.FirstOrDefault(d => d.Id == id);

            if (rolInDB == null)
                return NotFound();

            return Ok(rolInDB);
        }

        // POST api/Rol
        [HttpPost]
        public IHttpActionResult Post([FromBody]Rol rol)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            rol.Id = Rol.GetNextId(_context);
            _context.Rols.Add(rol);
            _context.SaveChanges();
            activeDirectory.createGroup(rol.ADGroupName);

            return Created(new Uri(Request.RequestUri + "/" + rol.Id), rol);
        }



        // PUT api/Rol/5
        [HttpPut]
        public IHttpActionResult Put(int id, [FromBody]Rol rol)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            Rol rolInDB = _context.Rols.FirstOrDefault(d => d.Id == id);
            if (rolInDB == null)
                return NotFound();

            rolInDB.Name = rol.Name;
            rolInDB.Level = rol.Level;
            rolInDB.ResourceId = rol.ResourceId;
            rolInDB.ADGroupName = rol.ADGroupName;
            _context.SaveChanges();

            return Ok(rolInDB);
        }

        // DELETE api/Rol/5
        [HttpDelete]
        public IHttpActionResult Delete(int id)
        {
            var rolInDB = _context.Rols.FirstOrDefault(d => d.Id == id);

            if (rolInDB == null)
                return NotFound();

            _context.Rols.Remove(rolInDB);
            _context.SaveChanges();

            return Ok();
        }

        [HttpGet]
        [Route("api/rol/Access/{id}")]
        public IHttpActionResult GetAccess(int id)
        {
           var rha = _context.RolshaAccesses.Include(r=>r.Access).Where(r => r.Rolid == id).
                 Select(r => new
                 {
                     r.Access.Id,
                     r.Access.Method,
                     r.Access.Description,
                     r.Access.Path,
                     r.Access.Public
                 }).ToList();

            return Ok(rha);
        }

        [HttpDelete]
        [Route("api/rol/Access/{id}")]
        public IHttpActionResult DeleteAccess(int id, [FromUri]int AccessId)
        {
            var xss = AccessId;

            if (AccessId==0)
                return BadRequest();

            Rol rol = _context.Rols.FirstOrDefault(r => r.Id == id);
            Access access = _context.Accesses.FirstOrDefault(a => a.Id == AccessId);

            if (rol == null || access == null)
                return NotFound();

            RolhasAccess rha = _context.RolshaAccesses.FirstOrDefault(x => x.Accessid == AccessId && x.Rolid == id);

            _context.RolshaAccesses.Remove(rha);
            _context.SaveChanges();
            return Ok();
        }

        [HttpPost]
        [Route("api/rol/Access/{id}")]
        public IHttpActionResult AddAccess(int id,[FromBody]JObject credentials)
        {
            int accessid = 0;
            if (credentials["AccessId"] == null)
                return BadRequest();

            if (!Int32.TryParse(credentials["AccessId"].ToString(), out accessid))
                return BadRequest();

            Rol rol = _context.Rols.FirstOrDefault(r => r.Id == id);
            Access access = _context.Accesses.FirstOrDefault(a => a.Id == accessid);

            if (rol == null || access == null)
                return NotFound();
            RolhasAccess rha = _context.RolshaAccesses.FirstOrDefault(x => x.Accessid == accessid && x.Rolid == id);

            if (rha != null)
                return Ok("El usuario ya tiene este acceso!");

            RolhasAccess rolhasAccess = new RolhasAccess();
            rolhasAccess.Id = RolhasAccess.GetNextId(_context);
            rolhasAccess.Accessid = accessid;
            rolhasAccess.Rolid = id;
            _context.RolshaAccesses.Add(rolhasAccess);
            _context.SaveChanges();

            return Ok();
        }
    }
}
