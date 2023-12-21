using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UcbBack.Models;
using UcbBack.Models.Dist;

namespace UcbBack.Controllers
{
    public class GrupoContableController : ApiController
    {
        private ApplicationDbContext _context;

        public GrupoContableController()
        {
            _context = new ApplicationDbContext();
        }

        // GET api/GrupoContable
        public IHttpActionResult Get()
        {
            return Ok(_context.GrupoContables.ToList());
        }

        // GET api/GrupoContable/5
        public IHttpActionResult Get(int id)
        {
            GrupoContable grcoInDB = null;

            grcoInDB = _context.GrupoContables.FirstOrDefault(d => d.Id == id);

            if (grcoInDB == null)
                return NotFound();

            return Ok(grcoInDB);
        }

        // POST api/GrupoContable
        [HttpPost]
        public IHttpActionResult Post([FromBody]GrupoContable grupoContable)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            grupoContable.Id = GrupoContable.GetNextId(_context);
            _context.GrupoContables.Add(grupoContable);
            _context.SaveChanges();
            return Created(new Uri(Request.RequestUri + "/" + grupoContable.Id), grupoContable);
        }

        // PUT api/GrupoContable/5
        [HttpPut]
        public IHttpActionResult Put(int id, [FromBody]GrupoContable grupoContable)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            GrupoContable grcoInDB = _context.GrupoContables.FirstOrDefault(d => d.Id == id);
            if (grcoInDB == null)
                return NotFound();

            grcoInDB.Name = grupoContable.Name;
            grcoInDB.Description = grupoContable.Description;

            _context.SaveChanges();
            return Ok(grcoInDB);
        }

        // DELETE api/GrupoContable/5
        [HttpDelete]
        public IHttpActionResult Delete(int id)
        {
            var grcoInDB = _context.GrupoContables.FirstOrDefault(d => d.Id == id);
            if (grcoInDB == null)
                return NotFound();
            _context.GrupoContables.Remove(grcoInDB);
            _context.SaveChanges();
            return Ok();
        }
    }
}
