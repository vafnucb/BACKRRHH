using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UcbBack.Logic;
using UcbBack.Models;
using UcbBack.Models.Auth;
using System.Data.Entity;

namespace UcbBack.Controllers
{
    public class AccessController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidatePerson validator;

        public AccessController()
        {
            _context = new ApplicationDbContext();
            validator = new ValidatePerson(_context);
        }

        // GET api/Access
        public IHttpActionResult Get()
        {
            var res = _context.Accesses.Include(x => x.Resource).Select(x =>
                    new {x.Id, x.Description, x.Method, x.Path, x.Public, x.ResourceId, Resource = x.Resource.Name})
                .ToList();
            return Ok(res);
        }

        // GET api/Access/5
        public IHttpActionResult Get(int id)
        {
            Access accessInDB = null;

            accessInDB = _context.Accesses.FirstOrDefault(d => d.Id == id);

            if (accessInDB == null)
                return NotFound();

            return Ok(accessInDB);
        }

        // POST api/Access
        [HttpPost]
        public IHttpActionResult Post([FromBody]Access access)
        {
            if (!ModelState.IsValid)
                return BadRequest();
            access.Id = Access.GetNextId(_context);

            _context.Accesses.Add(access);
            _context.SaveChanges();
            return Created(new Uri(Request.RequestUri + "/" + access.Id), access);
        }


        // PUT api/Access/5
        [HttpPut]
        public IHttpActionResult Put(int id, [FromBody]Access access)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            Access accessInDB = _context.Accesses.FirstOrDefault(d => d.Id == id);
            if (accessInDB == null)
                return NotFound();
            accessInDB.Method = access.Method;
            accessInDB.Path = access.Path;
            accessInDB.Description = access.Description;
            accessInDB.ResourceId = access.ResourceId;
            accessInDB.Public = access.Public;
            _context.SaveChanges();
            return Ok(accessInDB);
        }

        // DELETE api/Access/5
        [HttpDelete]
        public IHttpActionResult Delete(int id)
        {
            var accessInDB = _context.Accesses.FirstOrDefault(d => d.Id == id);
            if (accessInDB == null)
                return NotFound();
            _context.Accesses.Remove(accessInDB);
            _context.SaveChanges();
            return Ok();
        }
    }
}
