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
    public class TipoEmpleadoDistController : ApiController
    {
         private ApplicationDbContext _context;

         public TipoEmpleadoDistController()
        {
            _context = new ApplicationDbContext();
        }

         // GET api/TipoEmpleadoDist
        public IHttpActionResult Get()
        {
            var tedlist = _context.TipoEmpleadoDists.Include(p => p.GrupoContable).ToList().Select(x => new { x.Id, x.Name, x.Description, GrupoContable=x.GrupoContable.Name }).OrderBy(x => x.Id);
            return Ok(tedlist);
        }

        // GET api/TipoEmpleadoDist/5
        public IHttpActionResult Get(int id)
        {
            TipoEmpleadoDist tedInDB = _context.TipoEmpleadoDists.FirstOrDefault(d => d.Id == id);

            if (tedInDB == null)
                return NotFound();

            return Ok(tedInDB);
        }

        // POST api/TipoEmpleadoDist
        [HttpPost]
        public IHttpActionResult Post([FromBody]TipoEmpleadoDist tipoEmpleadoDist)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            tipoEmpleadoDist.Id = TipoEmpleadoDist.GetNextId(_context);
            _context.TipoEmpleadoDists.Add(tipoEmpleadoDist);
            _context.SaveChanges();
            return Created(new Uri(Request.RequestUri + "/" + tipoEmpleadoDist.Id), tipoEmpleadoDist);
        }

        // PUT api/TipoEmpleadoDist/5
        [HttpPut]
        public IHttpActionResult Put(int id, [FromBody]TipoEmpleadoDist tipoEmpleadoDist)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            TipoEmpleadoDist tedInDB = _context.TipoEmpleadoDists.FirstOrDefault(d => d.Id == id);
            if (tedInDB == null)
                return NotFound();

            tedInDB.GrupoContableId = tipoEmpleadoDist.GrupoContableId;
            tedInDB.Name = tipoEmpleadoDist.Name;
            tedInDB.Description = tipoEmpleadoDist.Description;

            _context.SaveChanges();
            return Ok(tedInDB);
        }

        // DELETE api/TipoEmpleadoDist/5
        [HttpDelete]
        public IHttpActionResult Delete(int id)
        {
            var tedInDB = _context.TipoEmpleadoDists.FirstOrDefault(d => d.Id == id);
            if (tedInDB == null)
                return NotFound();
            _context.TipoEmpleadoDists.Remove(tedInDB);
            _context.SaveChanges();
            return Ok();
        }
    }
}
