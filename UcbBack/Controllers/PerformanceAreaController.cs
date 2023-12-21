using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UcbBack.Models;

namespace UcbBack.Controllers
{
    public class PerformanceAreaController : ApiController
    {
         private ApplicationDbContext _context;

        public PerformanceAreaController()
        {
            _context = new ApplicationDbContext();
        }

        // GET api/PerformanceArea
        public IHttpActionResult Get()
        {
            return Ok(_context.PerformanceAreas.ToList());
        }

        // GET api/PerformanceArea/5
        public IHttpActionResult Get(int id)
        {
            PerformanceArea performanceAreaInDB = null;

            performanceAreaInDB = _context.PerformanceAreas.FirstOrDefault(d => d.Id == id);

            if (performanceAreaInDB == null)
                return NotFound();

            return Ok(performanceAreaInDB);
        }

        // POST api/PerformanceArea
        [HttpPost]
        public IHttpActionResult Post([FromBody]PerformanceArea performanceArea)
        {
            if (!ModelState.IsValid)
                return BadRequest();
            performanceArea.Id = PerformanceArea.GetNextId(_context);
            _context.PerformanceAreas.Add(performanceArea);
            _context.SaveChanges();
            return Created(new Uri(Request.RequestUri + "/" + performanceArea.Id), performanceArea);
        }

        // PUT api/PerformanceArea/5
        [HttpPut]
        public IHttpActionResult Put(int id, [FromBody]PerformanceArea performanceArea)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            PerformanceArea performanceAreaInDB = _context.PerformanceAreas.FirstOrDefault(d => d.Id == id);
            if (performanceAreaInDB == null)
                return NotFound();

            performanceAreaInDB.Name = performanceArea.Name;


            _context.SaveChanges();
            return Ok(performanceAreaInDB);
        }

        // DELETE api/PerformanceArea/5
        [HttpDelete]
        public IHttpActionResult Delete(int id)
        {
            var performanceAreaInDB = _context.PerformanceAreas.FirstOrDefault(d => d.Id == id);
            if (performanceAreaInDB == null)
                return NotFound();
            _context.PerformanceAreas.Remove(performanceAreaInDB);
            _context.SaveChanges();
            return Ok();
        }
    }
}
