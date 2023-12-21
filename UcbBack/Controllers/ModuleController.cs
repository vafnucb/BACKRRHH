using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UcbBack.Logic;
using UcbBack.Models;
using UcbBack.Models.Auth;

namespace UcbBack.Controllers
{
    public class ModuleController : ApiController
    {
        private ApplicationDbContext _context;

        public ModuleController()
        {
            _context = new ApplicationDbContext();
        }

        // GET api/Module
        public IHttpActionResult Get()
        {
            return Ok(_context.Modules.ToList());
        }

        // GET api/Module/5
        public IHttpActionResult Get(int id)
        {
            Module moduleInDB = null;

            moduleInDB = _context.Modules.FirstOrDefault(d => d.Id == id);

            if (moduleInDB == null)
                return NotFound();

            return Ok(moduleInDB);
        }

        // POST api/Module
        [HttpPost]
        public IHttpActionResult Post([FromBody]Module module)
        {
            if (!ModelState.IsValid)
                return BadRequest();
            module.Id = Module.GetNextId(_context);

            _context.Modules.Add(module);
            _context.SaveChanges();
            return Created(new Uri(Request.RequestUri + "/" + module.Id), module);
        }


        // PUT api/Module/5
        [HttpPut]
        public IHttpActionResult Put(int id, [FromBody]Module module)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            Module moduleInDB = _context.Modules.FirstOrDefault(d => d.Id == id);
            if (moduleInDB == null)
                return NotFound();
            moduleInDB.Name = module.Name;
            moduleInDB.Icon = module.Icon;
            _context.SaveChanges();
            return Ok(moduleInDB);
        }

        // DELETE api/Module/5
        [HttpDelete]
        public IHttpActionResult Delete(int id)
        {
            var moduleInDB = _context.Modules.FirstOrDefault(d => d.Id == id);
            if (moduleInDB == null)
                return NotFound();
            _context.Modules.Remove(moduleInDB);
            _context.SaveChanges();
            return Ok();
        }
    }
}
