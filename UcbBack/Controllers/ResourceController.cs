using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UcbBack.Models;
using System.Data.Entity;


namespace UcbBack.Controllers
{
    public class ResourceController : ApiController
    {
        private ApplicationDbContext _context;

        public ResourceController()
        {
            _context = new ApplicationDbContext();
        }

        // GET api/Resource
        public IHttpActionResult Get()
        {
            return Ok(_context.Resources.Include(x=>x.Module).Select(x=>new{x.Id,x.Name,x.ModuleId,Module=x.Module.Name}).ToList());
        }

        // GET api/Resource/5
        public IHttpActionResult Get(int id)
        {
            Resource ResourceInDB = null;

            ResourceInDB = _context.Resources.FirstOrDefault(d => d.Id == id);

            if (ResourceInDB == null)
                return NotFound();

            return Ok(ResourceInDB);
        }

        // POST api/Resource
        [HttpPost]
        public IHttpActionResult Post([FromBody]Resource resource)
        {
            if (!ModelState.IsValid)
                return BadRequest();
            resource.Id = Resource.GetNextId(_context);

            _context.Resources.Add(resource);
            _context.SaveChanges();
            return Created(new Uri(Request.RequestUri + "/" + resource.Id), resource);
        }


        // PUT api/Resource/5
        [HttpPut]
        public IHttpActionResult Put(int id, [FromBody]Resource resource)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            Resource ResourceInDB = _context.Resources.FirstOrDefault(d => d.Id == id);
            if (ResourceInDB == null)
                return NotFound();
            ResourceInDB.Name = resource.Name;
            ResourceInDB.ModuleId = resource.ModuleId;
            _context.SaveChanges();
            return Ok(ResourceInDB);
        }

        // DELETE api/Resource/5
        [HttpDelete]
        public IHttpActionResult Delete(int id)
        {
            var ResourceInDB = _context.Resources.FirstOrDefault(d => d.Id == id);
            if (ResourceInDB == null)
                return NotFound();
            _context.Resources.Remove(ResourceInDB);
            _context.SaveChanges();
            return Ok();
        }
    }
}
