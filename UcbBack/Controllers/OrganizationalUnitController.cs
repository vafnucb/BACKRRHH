using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UcbBack.Models;

namespace UcbBack.Controllers
{
    public class OrganizationalUnitController : ApiController
    {
         private ApplicationDbContext _context;

         public OrganizationalUnitController()
        {
            _context = new ApplicationDbContext();
        }

        // GET api/OrganizationalUnit
        public IHttpActionResult Get()
        {
            var ou = _context.OrganizationalUnits.OrderBy(x => x.Cod).ToList();
            return Ok(ou);
        }

        // GET api/OrganizationalUnit/5
        public IHttpActionResult Get(int id)
        {
            OrganizationalUnit OUnitInDB = null;

            OUnitInDB = _context.OrganizationalUnits.FirstOrDefault(d => d.Id == id);

            if (OUnitInDB == null)
                return NotFound();

            return Ok(OUnitInDB);
        }

        // POST api/OrganizationalUnits
        [HttpPost]
        public IHttpActionResult Post([FromBody]OrganizationalUnit organizationalUnit)
        {
            if (!ModelState.IsValid)
                return BadRequest();
            organizationalUnit.Id = OrganizationalUnit.GetNextId(_context);
            _context.OrganizationalUnits.Add(organizationalUnit);
            _context.SaveChanges();
            return Created(new Uri(Request.RequestUri + "/" + organizationalUnit.Id), organizationalUnit);
        }

        // PUT api/OrganizationalUnit/5
        [HttpPut]
        public IHttpActionResult Put(int id, [FromBody]OrganizationalUnit organizationalUnit)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            OrganizationalUnit OUnitInDB = _context.OrganizationalUnits.FirstOrDefault(d => d.Id == id);
            if (OUnitInDB == null)
                return NotFound();

            OUnitInDB.Cod = organizationalUnit.Cod;
            OUnitInDB.Name = organizationalUnit.Name;
            OUnitInDB.Active = organizationalUnit.Active;

            _context.SaveChanges();
            return Ok(OUnitInDB);
        }

        // DELETE api/OrganizationalUnit/5
        [HttpDelete]
        public IHttpActionResult Delete(int id)
        {
            var OUnitInDB = _context.OrganizationalUnits.FirstOrDefault(d => d.Id == id);
            if (OUnitInDB == null)
                return NotFound();
            _context.OrganizationalUnits.Remove(OUnitInDB);
            _context.SaveChanges();
            return Ok();
        }
    }
}
