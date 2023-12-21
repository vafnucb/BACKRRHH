using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UcbBack.Models;

namespace UcbBack.Controllers
{
    public class LevelController : ApiController
    {
        private ApplicationDbContext _context;

        public LevelController()
        {
            _context = new ApplicationDbContext();
        }

        // GET api/Level
        public IHttpActionResult Get()
        {
            return Ok(_context.Levels.ToList());
        }

        // GET api/Level/5
        public IHttpActionResult Get(int id)
        {
            Level levelInDB = null;

            levelInDB = _context.Levels.FirstOrDefault(d => d.Id == id);

            if (levelInDB == null)
                return NotFound();

            return Ok(levelInDB);
        }

        // POST api/Level
        [HttpPost]
        public IHttpActionResult Post([FromBody]Level level)
        {
            if (!ModelState.IsValid)
                return BadRequest();
            level.Id = Level.GetNextId(_context);
            _context.Levels.Add(level);
            _context.SaveChanges();
            return Created(new Uri(Request.RequestUri + "/" + level.Id), level);
        }

        // PUT api/Level/5
        [HttpPut]
        public IHttpActionResult Put(int id, [FromBody]Level level)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            Level levelInDB = _context.Levels.FirstOrDefault(d => d.Id == id);
            if (levelInDB == null)
                return NotFound();

            levelInDB.Cod = level.Cod;
            levelInDB.Category = level.Category;

            _context.SaveChanges();
            return Ok(levelInDB);
        }

        // DELETE api/Level/5
        [HttpDelete]
        public IHttpActionResult Delete(int id)
        {
            var levelInDB = _context.Levels.FirstOrDefault(d => d.Id == id);
            if (levelInDB == null)
                return NotFound();
            _context.Levels.Remove(levelInDB);
            _context.SaveChanges();
            return Ok();
        }
    }
}
