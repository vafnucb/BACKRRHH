using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UcbBack.Models;

namespace UcbBack.Controllers
{
    public class GestionController : ApiController
    {
        private ApplicationDbContext _context;

        public GestionController()
        {
            _context = new ApplicationDbContext();
        }

        // GET api/Gestion
        public IHttpActionResult Get()
        {
            return Ok(_context.Gestions.ToList());
        }

        // GET api/Gestion/5
        public IHttpActionResult Get(int id)
        {
            Gestion gestionInDB = null;

            gestionInDB = _context.Gestions.FirstOrDefault(d => d.Id == id);

            if (gestionInDB == null)
                return NotFound();

            return Ok(gestionInDB);
        }

        // POST api/Gestion
        [HttpPost]
        public IHttpActionResult Post([FromBody]Gestion gestion)
        {
            if (!ModelState.IsValid)
                return BadRequest();
            gestion.Id = Gestion.GetNextId(_context);
            _context.Gestions.Add(gestion);
            _context.SaveChanges();
            return Created(new Uri(Request.RequestUri + "/" + gestion.Id), gestion);
        }

        // PUT api/Gestion/5
        [HttpPut]
        public IHttpActionResult Put(int id, [FromBody]Gestion gestion)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            Gestion gestionInDB = _context.Gestions.FirstOrDefault(d => d.Id == id);
            if (gestionInDB == null)
                return NotFound();

            gestionInDB.Type = gestion.Type;
            gestionInDB.Name = gestion.Name;
            gestionInDB.StartDate = gestion.StartDate;
            gestionInDB.EndDate = gestion.EndDate;

            _context.SaveChanges();
            return Ok(gestionInDB);
        }

        // DELETE api/Gestion/5
        [HttpDelete]
        public IHttpActionResult Delete(int id)
        {
            var gestionInDB = _context.Gestions.FirstOrDefault(d => d.Id == id);
            if (gestionInDB == null)
                return NotFound();
            _context.Gestions.Remove(gestionInDB);
            _context.SaveChanges();
            return Ok();
        }
    }
}
