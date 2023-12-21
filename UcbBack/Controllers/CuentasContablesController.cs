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
    public class CuentasContablesController : ApiController
    {
        private ApplicationDbContext _context;

        public CuentasContablesController()
        {
            _context = new ApplicationDbContext();
        }

        // GET api/CuentasContables
        public IHttpActionResult Get()
        {
            var deplist = _context.CuentaContables.Include(p => p.GrupoContable).Include(p => p.Branches).ToList().Select(x => new { x.Id, x.Name, x.Concept, Branches=x.Branches.Abr, x.Indicator, GrupoContable = x.GrupoContable.Name }).OrderBy(x => x.Id);
            return Ok(deplist);
        }

        // GET api/CuentasContables/5
        public IHttpActionResult Get(int id)
        {
            CuentaContable cucoInDB = null;

            cucoInDB = _context.CuentaContables.FirstOrDefault(d => d.Id == id);

            if (cucoInDB == null)
                return NotFound();

            return Ok(cucoInDB);
        }

        // POST api/CuentasContables
        [HttpPost]
        public IHttpActionResult Post([FromBody]CuentaContable cuentaContable)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            cuentaContable.Id = CuentaContable.GetNextId(_context);
            _context.CuentaContables.Add(cuentaContable);
            _context.SaveChanges();
            return Created(new Uri(Request.RequestUri + "/" + cuentaContable.Id), cuentaContable);
        }

        // PUT api/CuentasContables/5
        [HttpPut]
        public IHttpActionResult Put(int id, [FromBody]CuentaContable cuentaContable)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            CuentaContable cucoInDB = _context.CuentaContables.FirstOrDefault(d => d.Id == id);
            if (cucoInDB == null)
                return NotFound();

            cucoInDB.GrupoContableId = cuentaContable.GrupoContableId;
            cucoInDB.Name = cuentaContable.Name;
            cucoInDB.Concept = cuentaContable.Concept;
            cucoInDB.BranchesId = cuentaContable.BranchesId;
            cucoInDB.Indicator = cuentaContable.Indicator;

            _context.SaveChanges();
            return Ok(cucoInDB);
        }

        // DELETE api/CuentasContables/5
        [HttpDelete]
        public IHttpActionResult Delete(int id)
        {
            var cucoInDB = _context.CuentaContables.FirstOrDefault(d => d.Id == id);
            if (cucoInDB == null)
                return NotFound();
            _context.CuentaContables.Remove(cucoInDB);
            _context.SaveChanges();
            return Ok();
        }
    }
}
