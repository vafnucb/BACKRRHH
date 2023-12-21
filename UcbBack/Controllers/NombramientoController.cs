using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UcbBack.Logic;
using UcbBack.Models;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using UcbBack.Models.Not_Mapped.ViewMoldes;

namespace UcbBack.Controllers
{
    public class NombramientoController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidateAuth auth;


        public NombramientoController()
        {
            _context = new ApplicationDbContext();
            auth = new ValidateAuth();
        }

        // GET api/Level
        public IHttpActionResult Get()
        {
           /* var query = "select * from " + CustomSchema.Schema + ".lastcontracts lc" +
                        " inner join " + CustomSchema.Schema + ".\"Position\" p" +
                        " on lc.\"PositionsId\" = p.\"Id\"" +
                        " where (lc.\"EndDate\" is null or lc.\"EndDate\"> current_date)" +
                        " and p.\"IsDesignated\" = true";
            * */
           var query = "select lc.\"Id\", pe.\"Document\", pe.\"CUNI\", " +
                       "fn.\"FullName\",\r\np.\"Name\" as \"Positions\", " +
                       "de.\"Name\" as \"Dependency\",\r\nde.\"Id\" as \"DependencyCod\"," +
                       "\r\nbr.\"Abr\" as \"Branches\", lc.\"StartDate\"," +
                       "\r\nlc.\"EndDateNombramiento\" as \"EndDate\"" +
                       "\r\nfrom " + CustomSchema.Schema + ".\"ContractDetail\" lc" +
                       "\r\ninner join " + CustomSchema.Schema + ".\"Position\" p" +
                       "\r\non lc.\"PositionsId\" = p.\"Id\"" +
                       "\r\ninner join " + CustomSchema.Schema + ".\"People\" pe" +
                       "\r\non pe.\"Id\" = lc.\"PeopleId\"" +
                       "\r\ninner join " + CustomSchema.Schema + ".\"FullName\" fn" +
                       "\r\non fn.\"PeopleId\" = pe.\"Id\"" +
                       "\r\ninner join " + CustomSchema.Schema + ".\"Dependency\" de" +
                       "\r\non de.\"Id\" = lc.\"DependencyId\"" +
                       "\r\ninner join " + CustomSchema.Schema + ".\"Branches\" br" +
                       "\r\non br.\"Id\" = lc.\"BranchesId\"" +
                       "\r\nwhere p.\"IsDesignated\" = true" +
                       "\r\norder by lc.\"Active\" desc, lc.\"EndDate\" asc";
            var rawresult = _context.Database.SqlQuery<ContractDetailViewModel>(query).ToList();

            var user = auth.getUser(Request);

            var res = auth.filerByRegional(rawresult.AsQueryable(), user);

            return Ok(res);
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
